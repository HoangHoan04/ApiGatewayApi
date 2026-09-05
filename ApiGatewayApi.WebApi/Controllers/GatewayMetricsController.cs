using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiGatewayApi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/gateway/metrics")]
public class GatewayMetricsController : ControllerBase
{
    private readonly ITraceLogStore _traceStore;
    private readonly IGatewayDbContext _db;

    public GatewayMetricsController(ITraceLogStore traceStore, IGatewayDbContext db)
    {
        _traceStore = traceStore;
        _db = db;
    }

    [HttpPost("stats")]
    public async Task<IActionResult> GetStats()
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-15);
        var rows = await _db.RequestLogs
            .Where(l => l.CreatedAt >= since)
            .Select(l => new { l.StatusCode, l.DurationMs, l.Path, l.CreatedAt })
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var timeline = new List<object>();
        for (int i = 14; i >= 0; i--)
        {
            var bucketStart = now.AddMinutes(-i - 1);
            var bucketEnd = now.AddMinutes(-i);
            var bucketRows = rows.Where(r => r.CreatedAt >= bucketStart && r.CreatedAt < bucketEnd).ToList();
            timeline.Add(new
            {
                time = bucketEnd.ToLocalTime().ToString("HH:mm"),
                requests = bucketRows.Count,
                avgLatencyMs = bucketRows.Count > 0 ? Math.Round(bucketRows.Average(x => x.DurationMs), 1) : 0
            });
        }

        if (rows.Count == 0)
        {
            var (ramTotal, ramSuccess, ramAvg, ramDist) = _traceStore.GetMetrics();
            return Ok(new
            {
                TotalRequests = ramTotal,
                SuccessRate = ramSuccess,
                AvgLatencyMs = ramAvg,
                P95LatencyMs = ramAvg,
                ErrorRate = 0,
                StatusDistribution = ramDist,
                TopSlowRoutes = Array.Empty<object>(),
                Timeline = timeline,
                UptimeSeconds = (DateTime.UtcNow - ProcessStartTime).TotalSeconds,
                MemoryUsageMb = Math.Round((double)GC.GetTotalMemory(false) / (1024 * 1024), 2),
                ServerTime = DateTime.UtcNow,
                WindowMinutes = 15,
                Source = "memory"
            });
        }

        var total = rows.Count;
        var ok = rows.Count(r => r.StatusCode is >= 200 and < 400);
        var err5 = rows.Count(r => r.StatusCode >= 500);
        var durations = rows.Select(r => r.DurationMs).OrderBy(x => x).ToList();
        var dist = new Dictionary<string, int>
        {
            ["2xx"] = rows.Count(r => r.StatusCode is >= 200 and < 300),
            ["3xx"] = rows.Count(r => r.StatusCode is >= 300 and < 400),
            ["4xx"] = rows.Count(r => r.StatusCode is >= 400 and < 500),
            ["5xx"] = err5
        };
        var topSlow = rows
            .GroupBy(r => r.Path)
            .Select(g => new
            {
                path = g.Key,
                count = g.Count(),
                avgMs = Math.Round(g.Average(x => x.DurationMs), 1),
                p95Ms = Percentile(g.Select(x => x.DurationMs).ToList(), 0.95)
            })
            .OrderByDescending(x => x.p95Ms)
            .Take(8)
            .ToList();

        return Ok(new
        {
            TotalRequests = total,
            SuccessRate = Math.Round(ok * 100.0 / total, 2),
            AvgLatencyMs = Math.Round(durations.Average(), 1),
            P95LatencyMs = Percentile(durations, 0.95),
            ErrorRate = Math.Round(err5 * 100.0 / total, 2),
            StatusDistribution = dist,
            TopSlowRoutes = topSlow,
            Timeline = timeline,
            UptimeSeconds = (DateTime.UtcNow - ProcessStartTime).TotalSeconds,
            MemoryUsageMb = Math.Round((double)GC.GetTotalMemory(false) / (1024 * 1024), 2),
            ServerTime = DateTime.UtcNow,
            WindowMinutes = 15,
            Source = "postgres"
        });
    }

    private static long Percentile(List<long> values, double p)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        values.Sort();
        var idx = Math.Clamp((int)Math.Ceiling(p * values.Count) - 1, 0, values.Count - 1);
        return values[idx];
    }

    private static readonly DateTime ProcessStartTime = DateTime.UtcNow;
}
