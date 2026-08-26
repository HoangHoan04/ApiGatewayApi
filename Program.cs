using System.Text.Json.Serialization;
using ApiGatewayApi.Middlewares;
using ApiGatewayApi.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ITraceLogStore, InMemoryTraceLogStore>();

// 2. Add YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// 3. Configure CORS
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200", "http://localhost:4300", "http://localhost:4400", "http://localhost:4500", "http://localhost:8000" };

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

// 4. Middlewares Pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<RequestTraceLoggerMiddleware>();
app.UseMiddleware<GatewayExceptionMiddleware>();

app.UseCors("GatewayCorsPolicy");

// 5. Dynamic Swagger Aggregator & UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "0. API Gateway Management (:8000)");

    var downstreamSection = app.Configuration.GetSection("DownstreamServices");
    foreach (var child in downstreamSection.GetChildren())
    {
        var key = child.Key;
        var name = child["Name"] ?? key;
        var baseUrl = child["BaseUrl"] ?? "";
        c.SwaggerEndpoint($"/api/gateway/swagger-docs/proxy/{key}", $"{name} ({baseUrl})");
    }

    c.RoutePrefix = "swagger";
});

// 6. Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Gateway = "ERP Enterprise API Gateway",
    Version = "1.0.0",
    Timestamp = DateTime.UtcNow
}));

// 7. Map Management Controllers
app.MapControllers();

// 8. Map YARP Reverse Proxy
app.MapReverseProxy();

app.Run();
