using ApiGatewayApi.Domain.Common;
using ApiGatewayApi.Domain.Enums;

namespace ApiGatewayApi.Domain.Entities;

public class GatewayDestination : BaseEntity
{
    public Guid ClusterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Weight { get; set; } = 1;
    public DestinationHealthStatus HealthStatus { get; set; } = DestinationHealthStatus.Unknown;
    public DateTimeOffset? LastHealthAt { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual GatewayCluster? Cluster { get; set; }
}
