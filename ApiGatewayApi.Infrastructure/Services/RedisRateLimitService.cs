using ApiGatewayApi.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ApiGatewayApi.Infrastructure.Services;

public sealed class RedisRateLimitService : IRedisRateLimit
{
    private const string IncrExpireLua = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
          redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimitService> _logger;

    public RedisRateLimitService(IConnectionMultiplexer redis, ILogger<RedisRateLimitService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(string key, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default)
    {
        try
        {
            var windowSeconds = Math.Max(1, (long)window.TotalSeconds);
            var windowId = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / windowSeconds;
            var redisKey = $"{key}:{windowId}";
            var count = (long)await _redis.GetDatabase().ScriptEvaluateAsync(
                IncrExpireLua,
                new RedisKey[] { redisKey },
                new RedisValue[] { windowSeconds });
            return count <= permitLimit;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis rate limit failed for {Key}; allowing request.", key);
            return true;
        }
    }
}
