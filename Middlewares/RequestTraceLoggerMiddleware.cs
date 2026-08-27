using System.Diagnostics;
using System.Security.Claims;
using ApiGatewayApi.Services;

namespace ApiGatewayApi.Middlewares;

public class RequestTraceLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITraceLogStore _traceStore;

    public RequestTraceLoggerMiddleware(RequestDelegate next, ITraceLogStore traceStore)
    {
        _next = next;
        _traceStore = traceStore;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/gateway/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.Items["X-Correlation-Id"]?.ToString() ?? Guid.NewGuid().ToString("N");
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var user = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User?.FindFirst("sub")?.Value
                ?? context.User?.FindFirst("email")?.Value;

            _traceStore.Record(new RequestTraceItem
            {
                CorrelationId = correlationId,
                Method = context.Request.Method,
                Path = path,
                QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                ClientIp = clientIp,
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                TargetCluster = context.Features.Get<Yarp.ReverseProxy.Model.IReverseProxyFeature>()?.Route?.Cluster?.ClusterId,
                UserIdentity = user
            });
        }
    }
}
