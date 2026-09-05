using ApiGatewayApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiGatewayApi.Application.Common.Interfaces;

public interface IGatewayDbContext
{
    DbSet<GatewayService> GatewayServices { get; }
    DbSet<GatewayCluster> GatewayClusters { get; }
    DbSet<GatewayDestination> GatewayDestinations { get; }
    DbSet<GatewayRoute> GatewayRoutes { get; }
    DbSet<RateLimitPolicy> RateLimitPolicies { get; }
    DbSet<IpRule> IpRules { get; }
    DbSet<CorsPolicy> CorsPolicies { get; }
    DbSet<RequestLog> RequestLogs { get; }
    DbSet<GatewayAlertRule> AlertRules { get; }
    DbSet<MaintenanceWindow> MaintenanceWindows { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
