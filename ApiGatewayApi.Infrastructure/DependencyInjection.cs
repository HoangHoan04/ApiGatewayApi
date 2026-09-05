using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.Infrastructure.BackgroundWorkers;
using ApiGatewayApi.Infrastructure.Persistence;
using ApiGatewayApi.Infrastructure.Proxy;
using ApiGatewayApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Yarp.ReverseProxy.Configuration;

namespace ApiGatewayApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        services.AddDbContext<GatewayDbContext>(options =>
        {
            options.UseNpgsql(connectionString, b =>
                b.MigrationsAssembly(typeof(GatewayDbContext).Assembly.FullName));
            options.ConfigureWarnings(w => w.Ignore(
                RelationalEventId.PendingModelChangesWarning,
                CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<IGatewayDbContext>(provider => provider.GetRequiredService<GatewayDbContext>());

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisConnection = configuration.GetConnectionString("Redis")
                ?? configuration["Redis:Connection"]
                ?? "localhost:6379";
            var options = ConfigurationOptions.Parse(redisConnection, true);
            options.AbortOnConnectFail = false;
            var mux = ConnectionMultiplexer.Connect(options);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Redis");
            if (!mux.IsConnected)
            {
                var env = sp.GetRequiredService<IHostEnvironment>();
                if (env.IsProduction())
                {
                    throw new InvalidOperationException($"Cannot connect to Redis at '{redisConnection}'.");
                }

                logger.LogWarning("Redis is not connected at {Connection}. Rate limit will retry/fail-open.", redisConnection);
            }

            return mux;
        });

        services.AddSingleton<IRedisRateLimit, RedisRateLimitService>();
        services.AddSingleton<RequestLogWriter>();
        services.AddSingleton<IRequestLogWriter>(sp => sp.GetRequiredService<RequestLogWriter>());
        services.AddHostedService(sp => sp.GetRequiredService<RequestLogWriter>());
        services.AddHostedService<LogRetentionWorker>();
        services.AddHostedService<AlertEvaluatorWorker>();

        services.AddSingleton<DbProxyConfigProvider>();
        services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DbProxyConfigProvider>());
        services.AddSingleton<IProxyConfigReloader>(sp => sp.GetRequiredService<DbProxyConfigProvider>());
        services.AddScoped<IGatewayControlService, GatewayControlService>();

        return services;
    }
}
