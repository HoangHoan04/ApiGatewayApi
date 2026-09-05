using System.Net.Http.Json;
using ApiGatewayApi.Domain.Enums;
using ApiGatewayApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiGatewayApi.Infrastructure.BackgroundWorkers;

public sealed class AlertEvaluatorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertEvaluatorWorker> _logger;

    public AlertEvaluatorWorker(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory http,
        IConfiguration configuration,
        ILogger<AlertEvaluatorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
                var rules = await db.AlertRules.Include(r => r.Service).Where(r => r.IsActive).ToListAsync(stoppingToken);
                foreach (var rule in rules)
                {
                    var window = DateTimeOffset.UtcNow.AddSeconds(-Math.Max(30, rule.WindowSeconds));
                    var logs = db.RequestLogs.Where(l => l.CreatedAt >= window);
                    if (rule.Service != null)
                    {
                        var clusterIds = await db.GatewayClusters
                            .Where(c => c.ServiceId == rule.ServiceId)
                            .Select(c => c.ClusterId)
                            .ToListAsync(stoppingToken);
                        logs = logs.Where(l => l.TargetCluster != null && clusterIds.Contains(l.TargetCluster));
                    }

                    var total = await logs.CountAsync(stoppingToken);
                    if (total == 0)
                    {
                        continue;
                    }

                    decimal value = rule.Metric switch
                    {
                        AlertMetric.ErrorRate => await logs.CountAsync(l => l.StatusCode >= 500, stoppingToken) * 100m / total,
                        AlertMetric.LatencyP95 => Percentile(await logs.Select(l => l.DurationMs).ToListAsync(stoppingToken), 0.95),
                        AlertMetric.RequestVolume => total,
                        AlertMetric.ServiceDown => await logs.CountAsync(l => l.StatusCode >= 500, stoppingToken) > 0 ? 1 : 0,
                        _ => 0
                    };

                    if (value >= rule.Threshold)
                    {
                        _logger.LogWarning("Gateway alert fired: {Name} metric={Metric} value={Value} threshold={Threshold}",
                            rule.Name, rule.Metric, value, rule.Threshold);
                        await NotifyHubAsync(rule.Name, rule.Metric.ToString(), value, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Alert evaluator failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task NotifyHubAsync(string name, string metric, decimal value, CancellationToken cancellationToken)
    {
        string? hub = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            hub = await db.GatewayServices
                .Where(s => s.Code == "integration-hub" && s.IsActive)
                .Select(s => s.BaseUrl)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch { }

        hub ??= _configuration["GatewaySettings:HubBaseUrl"] ?? "http://localhost:5104";
        if (string.IsNullOrWhiteSpace(hub))
        {
            return;
        }

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            await client.PostAsJsonAsync(
                $"{hub.TrimEnd('/')}/api/v1/sync/events",
                new
                {
                    eventType = "gateway.alert.fired",
                    sourceSystem = "API_GATEWAY",
                    payload = new { name, metric, value, at = DateTimeOffset.UtcNow }
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not publish gateway.alert.fired to Hub.");
        }
    }

    private static decimal Percentile(List<long> values, double p)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        var idx = Math.Clamp((int)Math.Ceiling(p * values.Count) - 1, 0, values.Count - 1);
        return values[idx];
    }
}
