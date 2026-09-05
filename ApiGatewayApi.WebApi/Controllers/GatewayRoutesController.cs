using ApiGatewayApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGatewayApi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/gateway/routes")]
public class GatewayRoutesController : ControllerBase
{
    private readonly IGatewayControlService _control;

    public GatewayRoutesController(IGatewayControlService control)
    {
        _control = control;
    }

    [HttpPost("list")]
    public async Task<IActionResult> GetRoutes()
    {
        var snap = await _control.GetSnapshotAsync();
        return Ok(new
        {
            TotalRoutes = snap.TotalRoutes,
            TotalClusters = snap.TotalClusters,
            Routes = snap.Routes.Select(r => new
            {
                r.Id,
                r.RouteId,
                ClusterId = r.ClusterCode,
                ClusterPk = r.ClusterId,
                r.PathMatch,
                r.Methods,
                r.AuthorizationPolicy,
                r.RateLimiterPolicy,
                r.RateLimitPolicyId,
                r.Timeout,
                r.IsPublic,
                r.IsActive,
                r.SortOrder,
                r.TransformsJson
            }),
            Clusters = snap.Clusters.Select(c => new
            {
                c.Id,
                c.ClusterId,
                c.ServiceId,
                c.TimeoutSeconds,
                c.CircuitBreakerEnabled,
                c.IsActive,
                Destinations = c.Destinations.Select(d => new
                {
                    d.Id,
                    d.Name,
                    d.Address,
                    d.Weight,
                    Health = d.HealthStatus,
                    d.IsActive
                }).ToList()
            }),
            snap.Services,
            snap.RatePolicies,
            snap.IpRules,
            snap.Maintenance
        });
    }
}
