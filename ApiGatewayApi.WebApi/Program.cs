using System.Text.Json.Serialization;
using ApiGatewayApi.Application;
using ApiGatewayApi.Infrastructure;
using ApiGatewayApi.Infrastructure.Persistence;
using ApiGatewayApi.WebApi.Middlewares;
using ApiGatewayApi.WebApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ERP Enterprise API Gateway",
        Version = "v1",
        Description = "Cổng truyền kết nối và điều phối yêu cầu tập trung cho Hệ sinh thái Microservices ERP"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT Bearer token: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton<ITraceLogStore, InMemoryTraceLogStore>();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

var jwt = builder.Configuration.GetSection("JwtSettings");
var issuer = jwt["Issuer"] ?? "https://auth.company.com";
var audience = jwt["Audience"] ?? "erp-ecosystem";
var authority = (jwt["Authority"] ?? "http://localhost:5000").TrimEnd('/');

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.MetadataAddress = $"{authority}/.well-known/openid-configuration";
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = JwtRegisteredClaimNames.Sub
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Query.TryGetValue("access_token", out var token) &&
                    string.IsNullOrEmpty(context.Token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddReverseProxy()
    .ConfigureHttpClient((_, handler) =>
    {
        if (handler is SocketsHttpHandler sockets)
        {
            sockets.ConnectTimeout = TimeSpan.FromSeconds(15);
            sockets.PooledConnectionLifetime = TimeSpan.FromMinutes(5);
        }
    });
builder.Services.AddSingleton<Yarp.ReverseProxy.Forwarder.IForwarderHttpClientFactory,
    ApiGatewayApi.Infrastructure.Proxy.IdempotentRetryForwarderHttpClientFactory>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("api-gateway"))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
        t.AddHttpClientInstrumentation();
        var otlp = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(otlp))
        {
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlp));
        }
    });

var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "http://localhost:4300", "http://localhost:4400", "http://localhost:4500", "http://localhost:4600", "http://localhost:8000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("GatewayCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

await DatabaseBootstrap.InitializeDatabaseAsync(app.Services);
await app.Services.GetRequiredService<ApiGatewayApi.Application.Common.Interfaces.IProxyConfigReloader>().ReloadAsync();
_ = app.Services.GetRequiredService<IConnectionMultiplexer>();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SpoofedHeaderMiddleware>();
app.UseMiddleware<RequestTraceLoggerMiddleware>();
app.UseMiddleware<GatewayExceptionMiddleware>();
app.UseCors("GatewayCorsPolicy");
app.UseAuthentication();
app.UseMiddleware<IdentityForwardMiddleware>();
app.UseMiddleware<GatewayAuthGateMiddleware>();
app.UseMiddleware<IpRuleMiddleware>();
app.UseMiddleware<RedisRateLimitMiddleware>();
app.UseMiddleware<MaintenanceMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "0. API Gateway Management (:8000)");
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            var services = db.GatewayServices.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToList();
            foreach (var s in services)
            {
                c.SwaggerEndpoint($"/api/gateway/swagger-docs/proxy/{s.Code}", $"{s.Name} ({s.BaseUrl})");
            }
        }
        catch { }

        c.RoutePrefix = "swagger";
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "Live" }));
app.MapGet("/health/ready", async (GatewayDbContext db, IConnectionMultiplexer redis) =>
{
    var dbOk = await db.Database.CanConnectAsync();
    var redisOk = redis.IsConnected;
    var ready = dbOk && (redisOk || !app.Environment.IsProduction());
    return Results.Json(new
    {
        status = ready ? "Ready" : "Degraded",
        db = dbOk,
        redis = redisOk
    }, statusCode: ready ? 200 : 503);
});

app.MapControllers();
app.MapReverseProxy();

app.Run();
