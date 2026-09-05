using ApiGatewayApi.Domain.Enums;

namespace ApiGatewayApi.Application.DTOs;

public class GatewaySnapshotDto
{
    public int TotalRoutes { get; set; }
    public int TotalClusters { get; set; }
    public List<GatewayRouteDetailDto> Routes { get; set; } = new();
    public List<GatewayClusterDetailDto> Clusters { get; set; } = new();
    public List<GatewayServiceDto> Services { get; set; } = new();
    public List<RateLimitPolicyDto> RatePolicies { get; set; } = new();
    public List<IpRuleDto> IpRules { get; set; } = new();
    public List<MaintenanceDto> Maintenance { get; set; } = new();
}

public class GatewayServiceDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string HealthPath { get; set; } = "/health";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
}

public class GatewayClusterDetailDto
{
    public Guid Id { get; set; }
    public string ClusterId { get; set; } = string.Empty;
    public Guid? ServiceId { get; set; }
    public string LoadBalancing { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public bool CircuitBreakerEnabled { get; set; }
    public bool IsActive { get; set; }
    public List<GatewayDestinationDto> Destinations { get; set; } = new();
}

public class GatewayDestinationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Weight { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class GatewayRouteDetailDto
{
    public Guid Id { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public Guid ClusterId { get; set; }
    public string ClusterCode { get; set; } = string.Empty;
    public string PathMatch { get; set; } = string.Empty;
    public List<string> Methods { get; set; } = new();
    public string AuthorizationPolicy { get; set; } = "Bearer";
    public string? RateLimiterPolicy { get; set; }
    public Guid? RateLimitPolicyId { get; set; }
    public int? Timeout { get; set; }
    public bool IsPublic { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? TransformsJson { get; set; }
}

public class RateLimitPolicyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RateLimitKeyType KeyType { get; set; }
    public int? RequestsPerMinute { get; set; }
    public int? RequestsPerDay { get; set; }
    public int Burst { get; set; }
    public bool IsActive { get; set; }
}

public class IpRuleDto
{
    public Guid Id { get; set; }
    public Guid? RouteId { get; set; }
    public IpRuleAction Action { get; set; }
    public string Cidr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class MaintenanceDto
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsActive { get; set; }
}

public class UpsertRouteRequest
{
    public Guid? Id { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public string ClusterId { get; set; } = string.Empty;
    public string PathMatch { get; set; } = string.Empty;
    public List<string>? Methods { get; set; }
    public string AuthorizationPolicy { get; set; } = "Bearer";
    public Guid? RateLimitPolicyId { get; set; }
    public int? TimeoutSeconds { get; set; }
    public bool IsPublic { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? TransformsJson { get; set; }
}

public class UpsertRateLimitRequest
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RateLimitKeyType KeyType { get; set; } = RateLimitKeyType.Ip;
    public int? RequestsPerMinute { get; set; }
    public int? RequestsPerDay { get; set; }
    public int Burst { get; set; } = 20;
    public bool IsActive { get; set; } = true;
}

public class UpsertIpRuleRequest
{
    public Guid? Id { get; set; }
    public Guid? RouteId { get; set; }
    public IpRuleAction Action { get; set; } = IpRuleAction.Deny;
    public string Cidr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpsertMaintenanceRequest
{
    public Guid? Id { get; set; }
    public Guid ServiceId { get; set; }
    public string Message { get; set; } = "Service đang bảo trì.";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TestRouteRequest
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = "/health";
    public string? TargetCluster { get; set; }
}

public class TestRouteResultDto
{
    public bool Matched { get; set; }
    public string? RouteId { get; set; }
    public string? ClusterId { get; set; }
    public string? Destination { get; set; }
    public int? StatusCode { get; set; }
    public long LatencyMs { get; set; }
    public string? Error { get; set; }
}

public class SecurityStatusDto
{
    public bool JwtEnabled { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string JwksUrl { get; set; } = string.Empty;
    public bool JwksReachable { get; set; }
    public int JwksKeyCount { get; set; }
    public string? JwksError { get; set; }
    public bool RedisConnected { get; set; }
    public List<string> AllowedOrigins { get; set; } = new();
    public int ActiveIpRules { get; set; }
    public int ActiveRatePolicies { get; set; }
    public int GlobalRpm { get; set; }
}

public class UpsertServiceRequest
{
    public Guid? Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string HealthPath { get; set; } = "/health";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpsertClusterRequest
{
    public Guid? Id { get; set; }
    public string ClusterId { get; set; } = string.Empty;
    public Guid? ServiceId { get; set; }
    public string? LoadBalancing { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public bool CircuitBreakerEnabled { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class UpsertDestinationRequest
{
    public Guid? Id { get; set; }
    public string ClusterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}
