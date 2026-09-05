using ApiGatewayApi.Application.DTOs;

namespace ApiGatewayApi.Application.Common.Interfaces;

public interface IProxyConfigReloader
{
    Task ReloadAsync(CancellationToken cancellationToken = default);
}

public interface IRequestLogWriter
{
    void Enqueue(RequestLogWrite item);
}

public sealed class RequestLogWrite
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string? QueryString { get; init; }
    public int StatusCode { get; init; }
    public long DurationMs { get; init; }
    public string? TargetCluster { get; init; }
    public string? ClientIp { get; init; }
    public Guid? UserId { get; init; }
    public Guid? CompanyId { get; init; }
    public long? RequestSize { get; init; }
}

public interface IRedisRateLimit
{
    Task<bool> TryAcquireAsync(string key, int permitLimit, TimeSpan window, CancellationToken cancellationToken = default);
}

public interface IGatewayControlService
{
    Task<GatewaySnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task<GatewayRouteDetailDto> UpsertRouteAsync(UpsertRouteRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteRouteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> SetRouteActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<RateLimitPolicyDto> UpsertRatePolicyAsync(UpsertRateLimitRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteRatePolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IpRuleDto> UpsertIpRuleAsync(UpsertIpRuleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteIpRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<MaintenanceDto> UpsertMaintenanceAsync(UpsertMaintenanceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteMaintenanceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TestRouteResultDto> TestRouteAsync(TestRouteRequest request, CancellationToken cancellationToken = default);
    Task<SecurityStatusDto> GetSecurityStatusAsync(CancellationToken cancellationToken = default);
    Task<GatewayServiceDto> UpsertServiceAsync(UpsertServiceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GatewayClusterDetailDto> UpsertClusterAsync(UpsertClusterRequest request, CancellationToken cancellationToken = default);
    Task<GatewayDestinationDto> UpsertDestinationAsync(UpsertDestinationRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteDestinationAsync(Guid id, CancellationToken cancellationToken = default);
}
