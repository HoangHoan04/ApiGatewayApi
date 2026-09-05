using ApiGatewayApi.Domain.Common;
using ApiGatewayApi.Domain.Enums;

namespace ApiGatewayApi.Domain.Entities;

public class RateLimitPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public RateLimitKeyType KeyType { get; set; } = RateLimitKeyType.Ip;
    public int? RequestsPerMinute { get; set; }
    public int? RequestsPerDay { get; set; }
    public int Burst { get; set; } = 20;
    public bool IsActive { get; set; } = true;

    public virtual ICollection<GatewayRoute> Routes { get; set; } = new List<GatewayRoute>();
}
