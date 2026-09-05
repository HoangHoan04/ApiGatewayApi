using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.Application.DTOs;
using ApiGatewayApi.Domain.Entities;
using ApiGatewayApi.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Yarp.ReverseProxy.Configuration;

namespace ApiGatewayApi.Infrastructure.Services;

public sealed class GatewayControlService : IGatewayControlService
{
    private readonly IGatewayDbContext _db;
    private readonly IProxyConfigReloader _reloader;
    private readonly IProxyConfigProvider _proxy;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _redis;

    public GatewayControlService(
        IGatewayDbContext db,
        IProxyConfigReloader reloader,
        IProxyConfigProvider proxy,
        IHttpClientFactory http,
        IConfiguration configuration,
        IConnectionMultiplexer redis)
    {
        _db = db;
        _reloader = reloader;
        _proxy = proxy;
        _http = http;
        _configuration = configuration;
        _redis = redis;
    }

    public async Task<GatewaySnapshotDto> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        List<GatewayRoute> routes = await _db.GatewayRoutes.Include(r => r.Cluster).Include(r => r.RateLimitPolicy)
            .OrderBy(r => r.SortOrder).ToListAsync(cancellationToken);
        List<GatewayCluster> clusters = await _db.GatewayClusters.Include(c => c.Destinations)
            .OrderBy(c => c.ClusterId).ToListAsync(cancellationToken);
        List<GatewayService> services = await _db.GatewayServices.OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        List<RateLimitPolicy> rates = await _db.RateLimitPolicies.OrderBy(p => p.Name).ToListAsync(cancellationToken);
        List<IpRule> ips = await _db.IpRules.OrderBy(r => r.SortOrder).ToListAsync(cancellationToken);
        List<MaintenanceWindow> maint = await _db.MaintenanceWindows.Include(m => m.Service).OrderByDescending(m => m.StartsAt)
            .ToListAsync(cancellationToken);

        List<GatewayRouteDetailDto> routeDtos = routes.Select(MapRoute).ToList();
        List<GatewayClusterDetailDto> clusterDtos = clusters.Select(c => new GatewayClusterDetailDto
        {
            Id = c.Id,
            ClusterId = c.ClusterId,
            ServiceId = c.ServiceId,
            LoadBalancing = c.LoadBalancing.ToString(),
            TimeoutSeconds = c.TimeoutSeconds,
            CircuitBreakerEnabled = c.CircuitBreakerEnabled,
            IsActive = c.IsActive,
            Destinations = c.Destinations.Select(d => new GatewayDestinationDto
            {
                Id = d.Id,
                Name = d.Name,
                Address = d.Address,
                Weight = d.Weight,
                HealthStatus = d.HealthStatus.ToString(),
                IsActive = d.IsActive
            }).ToList()
        }).ToList();

        IProxyConfig activeProxy = _proxy.GetConfig();
        if (clusterDtos.Count == 0 && activeProxy.Clusters.Count > 0)
        {
            foreach (ClusterConfig pc in activeProxy.Clusters)
            {
                clusterDtos.Add(new GatewayClusterDetailDto
                {
                    ClusterId = pc.ClusterId,
                    LoadBalancing = pc.LoadBalancingPolicy ?? "RoundRobin",
                    TimeoutSeconds = (int)(pc.HttpRequest?.ActivityTimeout?.TotalSeconds ?? 30),
                    CircuitBreakerEnabled = true,
                    IsActive = true,
                    Destinations = pc.Destinations != null
                        ? pc.Destinations.Select(d => new GatewayDestinationDto
                        {
                            Name = d.Key,
                            Address = d.Value.Address,
                            Weight = 1,
                            HealthStatus = "Online",
                            IsActive = true
                        }).ToList()
                        : []
                });
            }
        }

        if (routeDtos.Count == 0 && activeProxy.Routes.Count > 0)
        {
            foreach (RouteConfig pr in activeProxy.Routes)
            {
                routeDtos.Add(new GatewayRouteDetailDto
                {
                    RouteId = pr.RouteId,
                    ClusterCode = pr.ClusterId ?? "",
                    PathMatch = pr.Match.Path ?? "",
                    Methods = pr.Match.Methods?.ToList() ?? ["ALL"],
                    AuthorizationPolicy = pr.AuthorizationPolicy ?? "Anonymous",
                    RateLimiterPolicy = pr.RateLimiterPolicy ?? "Default",
                    IsPublic = true,
                    IsActive = true
                });
            }
        }

        return new GatewaySnapshotDto
        {
            TotalRoutes = routeDtos.Count,
            TotalClusters = clusterDtos.Count,
            Routes = routeDtos,
            Clusters = clusterDtos,
            Services = services.Select(s => new GatewayServiceDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                BaseUrl = s.BaseUrl,
                HealthPath = s.HealthPath,
                Description = s.Description,
                Icon = s.Icon,
                IsActive = s.IsActive
            }).ToList(),
            RatePolicies = rates.Select(MapRate).ToList(),
            IpRules = ips.Select(MapIp).ToList(),
            Maintenance = maint.Select(m => new MaintenanceDto
            {
                Id = m.Id,
                ServiceId = m.ServiceId,
                ServiceCode = m.Service?.Code ?? "",
                Message = m.Message,
                StartsAt = m.StartsAt,
                EndsAt = m.EndsAt,
                IsActive = m.IsActive
            }).ToList()
        };
    }

    public async Task<GatewayRouteDetailDto> UpsertRouteAsync(UpsertRouteRequest request, CancellationToken cancellationToken = default)
    {
        GatewayCluster cluster = await _db.GatewayClusters.FirstOrDefaultAsync(
            c => c.ClusterId == request.ClusterId || c.Id.ToString() == request.ClusterId, cancellationToken)
            ?? throw new InvalidOperationException("Cluster không tồn tại.");

        GatewayRoute entity;
        if (request.Id.HasValue)
        {
            entity = await _db.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                     ?? throw new KeyNotFoundException("Route không tồn tại.");
        }
        else
        {
            entity = new GatewayRoute { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            _ = _db.GatewayRoutes.Add(entity);
        }

        entity.ClusterId = cluster.Id;
        entity.RouteId = request.RouteId.Trim();
        entity.PathMatch = request.PathMatch.Trim();
        entity.MethodsJson = request.Methods is { Count: > 0 } ? JsonSerializer.Serialize(request.Methods) : null;
        entity.AuthorizationPolicy = string.IsNullOrWhiteSpace(request.AuthorizationPolicy) ? "Bearer" : request.AuthorizationPolicy;
        entity.RateLimitPolicyId = request.RateLimitPolicyId;
        entity.TimeoutSeconds = request.TimeoutSeconds;
        entity.IsPublic = request.IsPublic || string.Equals(entity.AuthorizationPolicy, "Anonymous", StringComparison.OrdinalIgnoreCase);
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        entity.TransformsJson = request.TransformsJson;
        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        entity = await _db.GatewayRoutes.Include(r => r.Cluster).Include(r => r.RateLimitPolicy)
            .FirstAsync(r => r.Id == entity.Id, cancellationToken);
        return MapRoute(entity);
    }

    public async Task<bool> DeleteRouteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GatewayRoute? entity = await _db.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _ = _db.GatewayRoutes.Remove(entity);
        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetRouteActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        GatewayRoute? entity = await _db.GatewayRoutes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        entity.IsActive = isActive;
        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        return true;
    }

    public async Task<RateLimitPolicyDto> UpsertRatePolicyAsync(UpsertRateLimitRequest request, CancellationToken cancellationToken = default)
    {
        RateLimitPolicy entity;
        if (request.Id.HasValue)
        {
            entity = await _db.RateLimitPolicies.FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
                     ?? throw new KeyNotFoundException("Policy không tồn tại.");
        }
        else
        {
            entity = new RateLimitPolicy { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            _ = _db.RateLimitPolicies.Add(entity);
        }

        entity.Name = request.Name.Trim();
        entity.KeyType = request.KeyType;
        entity.RequestsPerMinute = request.RequestsPerMinute;
        entity.RequestsPerDay = request.RequestsPerDay;
        entity.Burst = request.Burst;
        entity.IsActive = request.IsActive;
        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        return MapRate(entity);
    }

    public async Task<bool> DeleteRatePolicyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        RateLimitPolicy? entity = await _db.RateLimitPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _ = _db.RateLimitPolicies.Remove(entity);
        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        return true;
    }

    public async Task<IpRuleDto> UpsertIpRuleAsync(UpsertIpRuleRequest request, CancellationToken cancellationToken = default)
    {
        IpRule entity;
        if (request.Id.HasValue)
        {
            entity = await _db.IpRules.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                     ?? throw new KeyNotFoundException("IP rule không tồn tại.");
        }
        else
        {
            entity = new IpRule { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            _ = _db.IpRules.Add(entity);
        }

        entity.RouteId = request.RouteId;
        entity.Action = request.Action;
        entity.Cidr = request.Cidr.Trim();
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        _ = await _db.SaveChangesAsync(cancellationToken);
        return MapIp(entity);
    }

    public async Task<bool> DeleteIpRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        IpRule? entity = await _db.IpRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _ = _db.IpRules.Remove(entity);
        _ = await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MaintenanceDto> UpsertMaintenanceAsync(UpsertMaintenanceRequest request, CancellationToken cancellationToken = default)
    {
        GatewayService service = await _db.GatewayServices.FirstOrDefaultAsync(s => s.Id == request.ServiceId, cancellationToken)
                      ?? throw new InvalidOperationException("Service không tồn tại.");
        MaintenanceWindow entity;
        if (request.Id.HasValue)
        {
            entity = await _db.MaintenanceWindows.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
                     ?? throw new KeyNotFoundException("Maintenance không tồn tại.");
        }
        else
        {
            entity = new MaintenanceWindow { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
            _ = _db.MaintenanceWindows.Add(entity);
        }

        entity.ServiceId = request.ServiceId;
        entity.Message = request.Message;
        entity.StartsAt = request.StartsAt;
        entity.EndsAt = request.EndsAt;
        entity.IsActive = request.IsActive;
        _ = await _db.SaveChangesAsync(cancellationToken);
        return new MaintenanceDto
        {
            Id = entity.Id,
            ServiceId = entity.ServiceId,
            ServiceCode = service.Code,
            Message = entity.Message,
            StartsAt = entity.StartsAt,
            EndsAt = entity.EndsAt,
            IsActive = entity.IsActive
        };
    }

    public async Task<bool> DeleteMaintenanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        MaintenanceWindow? entity = await _db.MaintenanceWindows.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _ = _db.MaintenanceWindows.Remove(entity);
        _ = await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<TestRouteResultDto> TestRouteAsync(TestRouteRequest request, CancellationToken cancellationToken = default)
    {
        string path = request.Path.StartsWith('/') ? request.Path : "/" + request.Path;
        IProxyConfig config = _proxy.GetConfig();
        RouteConfig? match = config.Routes.FirstOrDefault(r => PathMatches(r.Match.Path ?? "/", path));

        if (match == null)
        {
            return new TestRouteResultDto { Matched = false, Error = "Không khớp route nào." };
        }

        ClusterConfig? cluster = config.Clusters.FirstOrDefault(c => c.ClusterId == match.ClusterId);
        string? dest = cluster?.Destinations?.FirstOrDefault().Value?.Address;
        TestRouteResultDto result = new()
        {
            Matched = true,
            RouteId = match.RouteId,
            ClusterId = match.ClusterId,
            Destination = dest
        };

        if (string.IsNullOrWhiteSpace(dest))
        {
            result.Error = "Cluster chưa có destination.";
            return result;
        }

        HttpClient client = _http.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            using HttpRequestMessage message = new(new HttpMethod(request.Method), dest.TrimEnd('/') + "/health");
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using HttpResponseMessage response = await client.SendAsync(message, cancellationToken);
            sw.Stop();
            result.StatusCode = (int)response.StatusCode;
            result.LatencyMs = sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.LatencyMs = sw.ElapsedMilliseconds;
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<SecurityStatusDto> GetSecurityStatusAsync(CancellationToken cancellationToken = default)
    {
        string jwksUrl = _configuration["JwtSettings:JwksUrl"] ?? "http://localhost:5000/.well-known/jwks.json";
        SecurityStatusDto dto = new()
        {
            JwtEnabled = true,
            Issuer = _configuration["JwtSettings:Issuer"] ?? "",
            Audience = _configuration["JwtSettings:Audience"] ?? "",
            JwksUrl = jwksUrl,
            RedisConnected = _redis.IsConnected,
            AllowedOrigins = _configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()?.ToList() ?? [],
            ActiveIpRules = await _db.IpRules.CountAsync(r => r.IsActive, cancellationToken),
            ActiveRatePolicies = await _db.RateLimitPolicies.CountAsync(p => p.IsActive, cancellationToken),
            GlobalRpm = 200
        };

        try
        {
            HttpClient client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(3);
            using HttpResponseMessage response = await client.GetAsync(jwksUrl, cancellationToken);
            dto.JwksReachable = response.IsSuccessStatusCode;
            if (response.IsSuccessStatusCode)
            {
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (doc.RootElement.TryGetProperty("keys", out JsonElement keys))
                {
                    dto.JwksKeyCount = keys.GetArrayLength();
                }
            }
            else
            {
                dto.JwksError = $"HTTP {(int)response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            dto.JwksReachable = false;
            dto.JwksError = ex.Message;
        }

        return dto;
    }

    public async Task<GatewayServiceDto> UpsertServiceAsync(UpsertServiceRequest request, CancellationToken cancellationToken = default)
    {
        GatewayService entity;
        if (request.Id.HasValue)
        {
            entity = await _db.GatewayServices.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
                     ?? throw new KeyNotFoundException("Service không tồn tại.");
        }
        else
        {
            string code = request.Code.Trim().ToLowerInvariant();
            GatewayService? existing = await _db.GatewayServices.FirstOrDefaultAsync(s => s.Code == code, cancellationToken);
            if (existing is null)
            {
                entity = new GatewayService { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
                _ = _db.GatewayServices.Add(entity);
            }
            else
            {
                entity = existing;
            }
        }

        entity.Code = request.Code.Trim().ToLowerInvariant();
        entity.Name = request.Name.Trim();
        entity.BaseUrl = request.BaseUrl.Trim().TrimEnd('/');
        entity.HealthPath = string.IsNullOrWhiteSpace(request.HealthPath) ? "/health" : request.HealthPath;
        entity.Description = request.Description;
        entity.Icon = request.Icon;
        entity.IsActive = request.IsActive;
        _ = await _db.SaveChangesAsync(cancellationToken);
        return new GatewayServiceDto
        {
            Id = entity.Id,
            Code = entity.Code,
            Name = entity.Name,
            BaseUrl = entity.BaseUrl,
            HealthPath = entity.HealthPath,
            Description = entity.Description,
            Icon = entity.Icon,
            IsActive = entity.IsActive
        };
    }

    public async Task<bool> DeleteServiceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GatewayService? entity = await _db.GatewayServices.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        List<GatewayCluster> clusters = await _db.GatewayClusters.Where(c => c.ServiceId == id).ToListAsync(cancellationToken);
        foreach (GatewayCluster? c in clusters)
        {
            c.ServiceId = null;
        }

        List<MaintenanceWindow> maint = await _db.MaintenanceWindows.Where(m => m.ServiceId == id).ToListAsync(cancellationToken);
        if (maint.Any())
        {
            _db.MaintenanceWindows.RemoveRange(maint);
        }

        _ = _db.GatewayServices.Remove(entity);
        _ = await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GatewayClusterDetailDto> UpsertClusterAsync(UpsertClusterRequest request, CancellationToken cancellationToken = default)
    {
        GatewayCluster entity;
        if (request.Id.HasValue)
        {
            entity = await _db.GatewayClusters.Include(c => c.Destinations)
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Cluster không tồn tại.");
        }
        else
        {
            GatewayCluster? existing = await _db.GatewayClusters.Include(c => c.Destinations)
                .FirstOrDefaultAsync(c => c.ClusterId == request.ClusterId, cancellationToken);
            if (existing is null)
            {
                entity = new GatewayCluster { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
                _ = _db.GatewayClusters.Add(entity);
            }
            else
            {
                entity = existing;
            }
        }

        entity.ClusterId = request.ClusterId.Trim();
        entity.ServiceId = request.ServiceId;
        entity.TimeoutSeconds = Math.Max(5, request.TimeoutSeconds);
        entity.CircuitBreakerEnabled = request.CircuitBreakerEnabled;
        entity.IsActive = request.IsActive;
        if (Enum.TryParse<Domain.Enums.LoadBalancingPolicy>(request.LoadBalancing, true, out LoadBalancingPolicy lb))
        {
            entity.LoadBalancing = lb;
        }

        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        entity = await _db.GatewayClusters.Include(c => c.Destinations)
            .FirstAsync(c => c.Id == entity.Id, cancellationToken);
        return new GatewayClusterDetailDto
        {
            Id = entity.Id,
            ClusterId = entity.ClusterId,
            ServiceId = entity.ServiceId,
            LoadBalancing = entity.LoadBalancing.ToString(),
            TimeoutSeconds = entity.TimeoutSeconds,
            CircuitBreakerEnabled = entity.CircuitBreakerEnabled,
            IsActive = entity.IsActive,
            Destinations = entity.Destinations.Select(d => new GatewayDestinationDto
            {
                Id = d.Id,
                Name = d.Name,
                Address = d.Address,
                Weight = d.Weight,
                HealthStatus = d.HealthStatus.ToString(),
                IsActive = d.IsActive
            }).ToList()
        };
    }

    public async Task<GatewayDestinationDto> UpsertDestinationAsync(UpsertDestinationRequest request, CancellationToken cancellationToken = default)
    {
        GatewayCluster cluster = await _db.GatewayClusters.FirstOrDefaultAsync(
                          c => c.ClusterId == request.ClusterId || c.Id.ToString() == request.ClusterId, cancellationToken)
                      ?? throw new InvalidOperationException("Cluster không tồn tại.");

        GatewayDestination entity;
        if (request.Id.HasValue)
        {
            entity = await _db.GatewayDestinations.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
                     ?? throw new KeyNotFoundException("Destination không tồn tại.");
        }
        else
        {
            GatewayDestination? existing = await _db.GatewayDestinations.FirstOrDefaultAsync(
                d => d.ClusterId == cluster.Id && d.Name == request.Name, cancellationToken);
            if (existing is null)
            {
                entity = new GatewayDestination { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
                _ = _db.GatewayDestinations.Add(entity);
            }
            else
            {
                entity = existing;
            }
        }

        entity.ClusterId = cluster.Id;
        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? Guid.NewGuid().ToString("N")[..8] : request.Name.Trim();
        entity.Address = request.Address.Trim().TrimEnd('/');
        entity.Weight = Math.Max(1, request.Weight);
        entity.IsActive = request.IsActive;
        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        return new GatewayDestinationDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Address = entity.Address,
            Weight = entity.Weight,
            HealthStatus = entity.HealthStatus.ToString(),
            IsActive = entity.IsActive
        };
    }

    public async Task<bool> DeleteDestinationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GatewayDestination? entity = await _db.GatewayDestinations.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _ = _db.GatewayDestinations.Remove(entity);
        _ = await _db.SaveChangesAsync(cancellationToken);
        await _reloader.ReloadAsync(cancellationToken);
        return true;
    }

    private static GatewayRouteDetailDto MapRoute(GatewayRoute r)
    {
        return new()
        {
            Id = r.Id,
            RouteId = r.RouteId,
            ClusterId = r.ClusterId,
            ClusterCode = r.Cluster?.ClusterId ?? "",
            PathMatch = r.PathMatch,
            Methods = string.IsNullOrWhiteSpace(r.MethodsJson)
            ? ["ALL"]
            : (JsonSerializer.Deserialize<List<string>>(r.MethodsJson) ?? ["ALL"]),
            AuthorizationPolicy = r.IsPublic ? "Anonymous" : r.AuthorizationPolicy,
            RateLimiterPolicy = r.RateLimitPolicy?.Name ?? "Default",
            RateLimitPolicyId = r.RateLimitPolicyId,
            Timeout = r.TimeoutSeconds,
            IsPublic = r.IsPublic,
            IsActive = r.IsActive,
            SortOrder = r.SortOrder,
            TransformsJson = r.TransformsJson
        };
    }

    private static RateLimitPolicyDto MapRate(RateLimitPolicy p)
    {
        return new()
        {
            Id = p.Id,
            Name = p.Name,
            KeyType = p.KeyType,
            RequestsPerMinute = p.RequestsPerMinute,
            RequestsPerDay = p.RequestsPerDay,
            Burst = p.Burst,
            IsActive = p.IsActive
        };
    }

    private static IpRuleDto MapIp(IpRule r)
    {
        return new()
        {
            Id = r.Id,
            RouteId = r.RouteId,
            Action = r.Action,
            Cidr = r.Cidr,
            Description = r.Description,
            IsActive = r.IsActive,
            SortOrder = r.SortOrder
        };
    }

    private static bool PathMatches(string pattern, string path)
    {
        string prefix = pattern
            .Replace("/{**catch-all}", "", StringComparison.OrdinalIgnoreCase)
            .Replace("{**catch-all}", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/');
        return string.IsNullOrEmpty(prefix) || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
