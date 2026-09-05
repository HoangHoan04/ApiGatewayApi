using ApiGatewayApi.Domain.Common;

namespace ApiGatewayApi.Domain.Entities;

public class RequestLog : ImmutableLogEntity
{
    public string CorrelationId { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string? TargetCluster { get; set; }
    public string? ClientIp { get; set; }
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public long? RequestSize { get; set; }
    public long? ResponseSize { get; set; }
}
