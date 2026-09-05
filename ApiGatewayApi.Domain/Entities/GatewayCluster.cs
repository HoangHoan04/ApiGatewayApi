using ApiGatewayApi.Domain.Common;
using ApiGatewayApi.Domain.Enums;

namespace ApiGatewayApi.Domain.Entities;

public class GatewayCluster : BaseEntity
{
    public Guid? ServiceId { get; set; }
    public string ClusterId { get; set; } = string.Empty;
    public LoadBalancingPolicy LoadBalancing { get; set; } = LoadBalancingPolicy.RoundRobin;
    public int TimeoutSeconds { get; set; } = 30;
    public bool CircuitBreakerEnabled { get; set; }
    public int CircuitBreakerFailures { get; set; } = 5;
    public bool IsActive { get; set; } = true;

    public virtual GatewayService? Service { get; set; }
    public virtual ICollection<GatewayDestination> Destinations { get; set; } = new List<GatewayDestination>();
    public virtual ICollection<GatewayRoute> Routes { get; set; } = new List<GatewayRoute>();
}
