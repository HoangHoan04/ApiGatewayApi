using ApiGatewayApi.Domain.Common;
using ApiGatewayApi.Domain.Enums;

namespace ApiGatewayApi.Domain.Entities;

public class IpRule : BaseEntity
{
    public Guid? RouteId { get; set; }
    public IpRuleAction Action { get; set; } = IpRuleAction.Deny;
    public string Cidr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public virtual GatewayRoute? Route { get; set; }
}
