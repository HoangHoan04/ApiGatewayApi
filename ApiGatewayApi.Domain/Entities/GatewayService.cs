using ApiGatewayApi.Domain.Common;

namespace ApiGatewayApi.Domain.Entities;

public class GatewayService : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string HealthPath { get; set; } = "/health";
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public virtual ICollection<GatewayCluster> Clusters { get; set; } = new List<GatewayCluster>();
}
