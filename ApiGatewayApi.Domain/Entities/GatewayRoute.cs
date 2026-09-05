using ApiGatewayApi.Domain.Common;

namespace ApiGatewayApi.Domain.Entities;

public class GatewayRoute : BaseEntity
{
    public Guid ClusterId { get; set; }
    public Guid? RateLimitPolicyId { get; set; }
    public Guid? CorsPolicyId { get; set; }
    public string RouteId { get; set; } = string.Empty;
    public string PathMatch { get; set; } = string.Empty;
    public string? MethodsJson { get; set; }
    public string? TransformsJson { get; set; }
    public string AuthorizationPolicy { get; set; } = "Bearer";
    public int? TimeoutSeconds { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublic { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual GatewayCluster? Cluster { get; set; }
    public virtual RateLimitPolicy? RateLimitPolicy { get; set; }
    public virtual CorsPolicy? CorsPolicy { get; set; }
}
