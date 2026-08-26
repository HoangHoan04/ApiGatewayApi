using System.Collections.Concurrent;

namespace ApiGatewayApi.Services;

public record RequestTraceItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string CorrelationId { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string? QueryString { get; init; }
    public string ClientIp { get; init; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? TargetCluster { get; set; }
    public string? UserIdentity { get; set; }
}

public interface ITraceLogStore
{
    void Record(RequestTraceItem item);
    IReadOnlyList<RequestTraceItem> GetRecent(int count = 100);
    (int TotalRequests, double SuccessRate, double AvgLatencyMs, Dictionary<string, int> StatusDistribution) GetMetrics();
    void Clear();
}

public class InMemoryTraceLogStore : ITraceLogStore
{
    private readonly ConcurrentQueue<RequestTraceItem> _queue = new();
    private const int MaxItems = 500;

    public void Record(RequestTraceItem item)
    {
        _queue.Enqueue(item);
        while (_queue.Count > MaxItems && _queue.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<RequestTraceItem> GetRecent(int count = 100)
    {
        return _queue.Reverse().Take(count).ToList();
    }

    public (int TotalRequests, double SuccessRate, double AvgLatencyMs, Dictionary<string, int> StatusDistribution) GetMetrics()
    {
        var items = _queue.ToList();
        var total = items.Count;
        if (total == 0)
        {
            return (0, 100.0, 0.0, new Dictionary<string, int> { ["2xx"] = 0, ["4xx"] = 0, ["5xx"] = 0 });
        }

        var successCount = items.Count(x => x.StatusCode is >= 200 and < 400);
        var successRate = Math.Round((double)successCount / total * 100, 1);
        var avgLatency = Math.Round(items.Average(x => x.DurationMs), 1);

        var distribution = new Dictionary<string, int>
        {
            ["2xx"] = items.Count(x => x.StatusCode is >= 200 and < 300),
            ["3xx"] = items.Count(x => x.StatusCode is >= 300 and < 400),
            ["4xx"] = items.Count(x => x.StatusCode is >= 400 and < 500),
            ["5xx"] = items.Count(x => x.StatusCode >= 500)
        };

        return (total, successRate, avgLatency, distribution);
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}
