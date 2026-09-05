using ApiGatewayApi.Domain.Common;

namespace ApiGatewayApi.Domain.Entities;

public class MaintenanceWindow : BaseEntity
{
    public Guid ServiceId { get; set; }
    public string Message { get; set; } = "Service đang bảo trì.";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual GatewayService? Service { get; set; }
}
