namespace ApiGatewayApi.Application.Common;

public static class GatewayPaths
{
    public static bool IsAnonymous(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/.well-known", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/oauth/", StringComparison.OrdinalIgnoreCase)
               || path.Equals("/oauth", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/oauth/", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/apps/public", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/apps/public", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/v1/webhooks/", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/v1/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/v1/auth/reset-password", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/v1/auth/accept-invite", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/v1/auth/2fa/verify", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/auth/refresh", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/auth/reset-password", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/auth/accept-invite", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/auth/2fa/verify", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/admin/auth/login", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/admin/auth/refresh", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/admin/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/admin/auth/reset-password", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/admin/auth/accept-invite", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/api/admin/auth/2fa/verify", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsControlPlane(string path) =>
        path.StartsWith("/api/gateway/", StringComparison.OrdinalIgnoreCase);
}
