using ApiGatewayApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiGatewayApi.Controllers;

[ApiController]
[Route("api/gateway/metrics")]
public class GatewayMetricsController : ControllerBase
{
    private readonly ITraceLogStore _traceStore;

    public GatewayMetricsController(ITraceLogStore traceStore)
    {
        _traceStore = traceStore;
    }

    [HttpPost("stats")]
    public IActionResult GetStats()
    {
        var (total, successRate, avgLatency, distribution) = _traceStore.GetMetrics();

        return Ok(new
        {
            TotalRequests = total,
            SuccessRate = successRate,
            AvgLatencyMs = avgLatency,
            StatusDistribution = distribution,
            UptimeSeconds = (DateTime.UtcNow - ProcessStartTime).TotalSeconds,
            MemoryUsageMb = Math.Round((double)GC.GetTotalMemory(false) / (1024 * 1024), 2),
            ServerTime = DateTime.UtcNow
        });
    }

    private static readonly DateTime ProcessStartTime = DateTime.UtcNow;
}
