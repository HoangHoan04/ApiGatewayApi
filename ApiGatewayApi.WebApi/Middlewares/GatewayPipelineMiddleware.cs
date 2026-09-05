using System.Net;
using System.Security.Claims;
using ApiGatewayApi.Application.Common;
using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Yarp.ReverseProxy.Configuration;

namespace ApiGatewayApi.WebApi.Middlewares;

public sealed class SpoofedHeaderMiddleware
{
    private static readonly string[] Strip =
    {
        "X-User-Id", "X-Company-Id", "X-Forwarded-User", "X-Actor-Id"
    };

    private readonly RequestDelegate _next;

    public SpoofedHeaderMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        foreach (var header in Strip)
        {
            context.Request.Headers.Remove(header);
        }

        return _next(context);
    }
}

public sealed class IdentityForwardMiddleware
{
    private readonly RequestDelegate _next;

    public IdentityForwardMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? context.User.FindFirstValue("sub");
            var company = context.User.FindFirstValue("company_id");
            if (!string.IsNullOrWhiteSpace(sub))
            {
                context.Request.Headers["X-User-Id"] = sub;
            }

            if (!string.IsNullOrWhiteSpace(company))
            {
                context.Request.Headers["X-Company-Id"] = company;
            }
        }

        var correlation = context.Items["X-Correlation-Id"]?.ToString();
        if (!string.IsNullOrWhiteSpace(correlation))
        {
            context.Request.Headers["X-Correlation-Id"] = correlation;
        }

        var ip = context.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(ip))
        {
            context.Request.Headers["X-Forwarded-For"] = ip;
        }

        return _next(context);
    }
}

public sealed class GatewayAuthGateMiddleware
{
    private readonly RequestDelegate _next;

    public GatewayAuthGateMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IProxyConfigProvider proxy)
    {
        var path = context.Request.Path.Value ?? "";
        if (GatewayPaths.IsAnonymous(path) || GatewayPaths.IsControlPlane(path))
        {
            await _next(context);
            return;
        }

        if (IsPublicRoute(proxy, path) || context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { success = false, message = "Thiếu hoặc JWT không hợp lệ." });
    }

    private static bool IsPublicRoute(IProxyConfigProvider proxy, string path)
    {
        var route = MatchRoute(proxy, path);
        return string.Equals(route?.Metadata?.GetValueOrDefault("isPublic"), "true", StringComparison.OrdinalIgnoreCase);
    }

    internal static RouteConfig? MatchRoute(IProxyConfigProvider proxy, string path)
    {
        RouteConfig? best = null;
        var bestLen = -1;
        foreach (var route in proxy.GetConfig().Routes)
        {
            var pattern = route.Match.Path ?? "/";
            var prefix = pattern
                .Replace("/{**catch-all}", "", StringComparison.OrdinalIgnoreCase)
                .Replace("{**catch-all}", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');
            var matches = string.IsNullOrEmpty(prefix) || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            if (!matches)
            {
                continue;
            }

            var len = prefix.Length;
            if (len > bestLen)
            {
                best = route;
                bestLen = len;
            }
        }

        return best;
    }
}

public sealed class IpRuleMiddleware
{
    private readonly RequestDelegate _next;

    public IpRuleMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IGatewayDbContext db)
    {
        var ip = context.Connection.RemoteIpAddress;
        if (ip == null)
        {
            await _next(context);
            return;
        }

        var rules = await db.IpRules.Where(r => r.IsActive).OrderBy(r => r.SortOrder).ToListAsync(context.RequestAborted);
        if (rules.Count == 0)
        {
            await _next(context);
            return;
        }

        var matchedAllow = false;
        var hasAllow = rules.Any(r => r.Action == IpRuleAction.Allow);
        foreach (var rule in rules)
        {
            if (!CidrMatch(ip, rule.Cidr))
            {
                continue;
            }

            if (rule.Action == IpRuleAction.Deny)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { success = false, message = "IP bị chặn." });
                return;
            }

            matchedAllow = true;
        }

        if (hasAllow && !matchedAllow)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { success = false, message = "IP không nằm trong allow-list." });
            return;
        }

        await _next(context);
    }

    private static bool CidrMatch(IPAddress ip, string cidr)
    {
        if (string.IsNullOrWhiteSpace(cidr))
        {
            return false;
        }

        if (!cidr.Contains('/'))
        {
            return IPAddress.TryParse(cidr, out var exact) && exact.Equals(ip);
        }

        var parts = cidr.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefix))
        {
            return false;
        }

        if (ip.AddressFamily != network.AddressFamily)
        {
            ip = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
            if (ip.AddressFamily != network.AddressFamily)
            {
                return false;
            }
        }

        var ipBytes = ip.GetAddressBytes();
        var netBytes = network.GetAddressBytes();
        if (ipBytes.Length != netBytes.Length)
        {
            return false;
        }

        var fullBytes = prefix / 8;
        var remain = prefix % 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (ipBytes[i] != netBytes[i])
            {
                return false;
            }
        }

        if (remain == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remain));
        return (ipBytes[fullBytes] & mask) == (netBytes[fullBytes] & mask);
    }
}

public sealed class RedisRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RedisRateLimitMiddleware> _logger;

    public RedisRateLimitMiddleware(RequestDelegate next, ILogger<RedisRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRedisRateLimit limiter, IProxyConfigProvider proxy)
    {
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var route = GatewayAuthGateMiddleware.MatchRoute(proxy, path);
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? context.User.FindFirstValue("sub")
                   ?? "anon";
        var rpm = 200;
        if (int.TryParse(route?.Metadata?.GetValueOrDefault("rpm"), out var routeRpm) && routeRpm > 0)
        {
            rpm = routeRpm;
        }

        var window = TimeSpan.FromMinutes(1);
        var routeId = route?.RouteId ?? path;
        var okGlobal = await limiter.TryAcquireAsync($"gw:rl:ip:{ip}", rpm, window, context.RequestAborted);
        var okRoute = await limiter.TryAcquireAsync($"gw:rl:route:{routeId}", Math.Max(rpm * 3, 600), window, context.RequestAborted);
        var okUser = await limiter.TryAcquireAsync($"gw:rl:user:{user}", 120, window, context.RequestAborted);
        if (!okGlobal || !okRoute || !okUser)
        {
            _logger.LogWarning("Rate limited {Path} ip={Ip}", path, ip);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = "60";
            await context.Response.WriteAsJsonAsync(new { success = false, message = "Quá nhiều yêu cầu. Thử lại sau." });
            return;
        }

        await _next(context);
    }
}

public sealed class MaintenanceMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IGatewayDbContext db, IProxyConfigProvider proxy)
    {
        var path = context.Request.Path.Value ?? "";
        var route = GatewayAuthGateMiddleware.MatchRoute(proxy, path);
        var clusterId = route?.ClusterId;
        if (string.IsNullOrWhiteSpace(clusterId))
        {
            await _next(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var window = await (
            from m in db.MaintenanceWindows.Include(x => x.Service)
            join c in db.GatewayClusters on m.ServiceId equals c.ServiceId
            where m.IsActive && c.ClusterId == clusterId && m.StartsAt <= now && (m.EndsAt == null || m.EndsAt > now)
            select m).FirstOrDefaultAsync(context.RequestAborted);

        if (window != null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { success = false, message = window.Message, maintenance = true });
            return;
        }

        await _next(context);
    }
}
