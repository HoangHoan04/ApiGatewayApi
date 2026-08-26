using Microsoft.AspNetCore.Mvc;
using Yarp.ReverseProxy.Configuration;

namespace ApiGatewayApi.Controllers;

[ApiController]
[Route("api/gateway/routes")]
public class GatewayRoutesController : ControllerBase
{
    private readonly IProxyConfigProvider _proxyConfigProvider;

    public GatewayRoutesController(IProxyConfigProvider proxyConfigProvider)
    {
        _proxyConfigProvider = proxyConfigProvider;
    }

    [HttpPost("list")]
    public IActionResult GetRoutes()
    {
        var config = _proxyConfigProvider.GetConfig();
        
        var routes = config.Routes.Select(r => new
        {
            RouteId = r.RouteId,
            ClusterId = r.ClusterId,
            PathMatch = r.Match.Path,
            Methods = r.Match.Methods ?? new List<string> { "ALL" },
            AuthorizationPolicy = r.AuthorizationPolicy ?? "Anonymous",
            RateLimiterPolicy = r.RateLimiterPolicy ?? "Default",
            Timeout = r.Timeout?.TotalSeconds
        }).ToList();

        var clusters = config.Clusters.Select(c => new
        {
            ClusterId = c.ClusterId,
            Destinations = c.Destinations?.Select(d => new
            {
                Name = d.Key,
                Address = d.Value.Address,
                Health = d.Value.Health
            }).ToList() ?? new()
        }).ToList();

        return Ok(new
        {
            TotalRoutes = routes.Count,
            TotalClusters = clusters.Count,
            Routes = routes,
            Clusters = clusters
        });
    }
}
