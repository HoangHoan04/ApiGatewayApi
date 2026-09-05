using System.Diagnostics;
using System.Security.Claims;
using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.WebApi.Services;
using Yarp.ReverseProxy.Configuration;

namespace ApiGatewayApi.WebApi.Middlewares;

public class RequestTraceLoggerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITraceLogStore _traceStore;
    private readonly IRequestLogWriter _writer;

    public RequestTraceLoggerMiddleware(RequestDelegate next, ITraceLogStore traceStore, IRequestLogWriter writer)
    {
        _next = next;
        _traceStore = traceStore;
        _writer = writer;
    }

    public async Task InvokeAsync(HttpContext context, IProxyConfigProvider proxy)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
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
                       ?? context.User?.FindFirst("sub")?.Value;
            Guid? userId = Guid.TryParse(user, out var uid) ? uid : null;
            Guid? companyId = Guid.TryParse(context.User?.FindFirst("company_id")?.Value, out var cid) ? cid : null;
            var cluster = GatewayAuthGateMiddleware.MatchRoute(proxy, path)?.ClusterId;

            var item = new RequestTraceItem
            {
                CorrelationId = correlationId,
                Method = context.Request.Method,
                Path = path,
                QueryString = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null,
                ClientIp = clientIp,
                StatusCode = context.Response.StatusCode,
                DurationMs = stopwatch.ElapsedMilliseconds,
                TargetCluster = cluster,
                UserIdentity = user
            };
            _traceStore.Record(item);
            _writer.Enqueue(new RequestLogWrite
            {
                CorrelationId = correlationId,
                Method = context.Request.Method,
                Path = path,
                QueryString = item.QueryString,
                StatusCode = item.StatusCode,
                DurationMs = item.DurationMs,
                TargetCluster = cluster,
                ClientIp = clientIp,
                UserId = userId,
                CompanyId = companyId,
                RequestSize = context.Request.ContentLength
            });
        }
    }
}
