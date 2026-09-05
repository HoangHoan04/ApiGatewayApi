using ApiGatewayApi.Domain.Common;
using ApiGatewayApi.Domain.Enums;

namespace ApiGatewayApi.Domain.Entities;

public class GatewayAlertRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? ServiceId { get; set; }
    public Guid? RouteId { get; set; }
    public AlertMetric Metric { get; set; } = AlertMetric.ErrorRate;
    public decimal Threshold { get; set; }
    public int WindowSeconds { get; set; } = 60;
    public string? WebhookUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public virtual GatewayService? Service { get; set; }
    public virtual GatewayRoute? Route { get; set; }
}
