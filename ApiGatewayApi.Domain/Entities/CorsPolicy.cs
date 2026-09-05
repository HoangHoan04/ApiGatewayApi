using ApiGatewayApi.Domain.Common;

namespace ApiGatewayApi.Domain.Entities;

public class CorsPolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string AllowedOriginsJson { get; set; } = "[]";
    public bool AllowCredentials { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public virtual ICollection<GatewayRoute> Routes { get; set; } = new List<GatewayRoute>();
}
