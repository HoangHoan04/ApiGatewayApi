using ApiGatewayApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiGatewayApi.Infrastructure.BackgroundWorkers;

public sealed class LogRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogRetentionWorker> _logger;

    public LogRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<LogRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var days = int.TryParse(_configuration["GatewaySettings:RequestLogRetentionDays"], out var d) ? d : 30;
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
                var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(7, days));
                var old = await db.RequestLogs.Where(x => x.CreatedAt < cutoff).Take(5000).ToListAsync(stoppingToken);
                if (old.Count > 0)
                {
                    db.RequestLogs.RemoveRange(old);
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Deleted {Count} expired request logs.", old.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request log TTL job failed.");
            }

            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
