using System.Text.Json;
using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;

namespace ApiGatewayApi.Infrastructure.Proxy;

public sealed class DbProxyConfigProvider : IProxyConfigProvider, IProxyConfigReloader
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbProxyConfigProvider> _logger;
    private volatile InMemoryProxyConfig _config;

    public DbProxyConfigProvider(
        IServiceScopeFactory scopeFactory,
        ILogger<DbProxyConfigProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = Load();
    }

    public IProxyConfig GetConfig() => _config;

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        var next = Load();
        var previous = _config;
        _config = next;
        previous.SignalChange();
        return Task.CompletedTask;
    }

    private InMemoryProxyConfig Load()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            var configuration = scope.ServiceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();

            var clusters = db.GatewayClusters
                .AsNoTracking()
                .Include(c => c.Destinations)
                .Include(c => c.Service)
                .Where(c => c.IsActive)
                .ToList();

            var routes = db.GatewayRoutes
                .AsNoTracking()
                .Include(r => r.Cluster)
                .Include(r => r.RateLimitPolicy)
                .Where(r => r.IsActive && r.Cluster != null && r.Cluster.IsActive)
                .OrderBy(r => r.SortOrder)
                .ToList();

            var clusterConfigs = clusters.Select(ToCluster).ToList();
            var routeConfigs = routes.Select(ToRoute).Where(r => r != null).Cast<RouteConfig>().ToList();

            EnsureFallbackAuth(clusterConfigs, routeConfigs, configuration, _logger);

            return new InMemoryProxyConfig(routeConfigs, clusterConfigs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải cấu hình định tuyến từ GatewayDb.");
            return new InMemoryProxyConfig(Array.Empty<RouteConfig>(), Array.Empty<ClusterConfig>());
        }
    }

    private static void EnsureFallbackAuth(
        List<ClusterConfig> clusterConfigs,
        List<RouteConfig> routeConfigs,
        Microsoft.Extensions.Configuration.IConfiguration? configuration,
        ILogger logger)
    {
        var rawAuthority = configuration?["JwtSettings:Authority"] ?? "http://localhost:5000";
        var authority = rawAuthority.TrimEnd('/');

        var hasAuthCluster = clusterConfigs.Any(c =>
            c.ClusterId.Equals("auth-cluster", StringComparison.OrdinalIgnoreCase) ||
            c.ClusterId.Equals("auth", StringComparison.OrdinalIgnoreCase));

        if (!hasAuthCluster)
        {
            logger.LogInformation("Nạp fallback cluster cho Auth: auth-cluster -> {Authority}", authority);
            clusterConfigs.Add(new ClusterConfig
            {
                ClusterId = "auth-cluster",
                LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
                HttpRequest = new ForwarderRequestConfig
                {
                    ActivityTimeout = TimeSpan.FromSeconds(30)
                },
                Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                {
                    ["auth-fallback"] = new DestinationConfig
                    {
                        Address = authority + "/",
                        Metadata = new Dictionary<string, string> { ["weight"] = "1" }
                    }
                }
            });
        }

        var targetClusterId = hasAuthCluster
            ? clusterConfigs.First(c => c.ClusterId.Contains("auth", StringComparison.OrdinalIgnoreCase)).ClusterId
            : "auth-cluster";

        var fallbackPatterns = new (string pattern, string routeId)[]
        {
            ("/api/admin/{**catch-all}", "fallback-auth-admin"),
            ("/api/auth/{**catch-all}", "fallback-auth-api"),
            ("/api/oauth/{**catch-all}", "fallback-auth-oauth"),
            ("/oauth/{**catch-all}", "fallback-auth-oauth-root"),
            ("/.well-known/{**catch-all}", "fallback-auth-wellknown"),
            ("/api/administrative/{**catch-all}", "fallback-auth-administrative"),
            ("/api/apps/public/{**catch-all}", "fallback-auth-apps-public"),
            ("/apps/public/{**catch-all}", "fallback-auth-public"),
            ("/api/v1/auth/{**catch-all}", "fallback-auth-v1")
        };

        foreach (var (pattern, routeId) in fallbackPatterns)
        {
            var routeExists = routeConfigs.Any(r =>
                string.Equals(r.Match.Path, pattern, StringComparison.OrdinalIgnoreCase));

            if (!routeExists)
            {
                routeConfigs.Add(new RouteConfig
                {
                    RouteId = routeId,
                    ClusterId = targetClusterId,
                    Match = new RouteMatch
                    {
                        Path = pattern
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        ["isPublic"] = "true",
                        ["isFallback"] = "true"
                    }
                });
            }
        }
    }

    private static ClusterConfig ToCluster(Domain.Entities.GatewayCluster cluster)
    {
        var destinations = cluster.Destinations
            .Where(d => d.IsActive)
            .ToDictionary(
                d => d.Name,
                d => new DestinationConfig
                {
                    Address = d.Address.TrimEnd('/') + "/",
                    Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["weight"] = Math.Max(1, d.Weight).ToString()
                    }
                });

        var healthPath = cluster.Service?.HealthPath;
        if (string.IsNullOrWhiteSpace(healthPath) ||
            healthPath.Contains("swagger", StringComparison.OrdinalIgnoreCase) ||
            healthPath.Contains("negotiate", StringComparison.OrdinalIgnoreCase) ||
            healthPath == "/")
        {
            healthPath = "/health";
        }

        var isDevelopment = string.Equals(
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            "Development",
            StringComparison.OrdinalIgnoreCase);

        return new ClusterConfig
        {
            ClusterId = cluster.ClusterId,
            Destinations = destinations,
            LoadBalancingPolicy = cluster.LoadBalancing switch
            {
                Domain.Enums.LoadBalancingPolicy.LeastRequests => LoadBalancingPolicies.LeastRequests,
                Domain.Enums.LoadBalancingPolicy.Weighted => LoadBalancingPolicies.PowerOfTwoChoices,
                _ => LoadBalancingPolicies.RoundRobin
            },
            HttpRequest = new ForwarderRequestConfig
            {
                ActivityTimeout = TimeSpan.FromSeconds(Math.Max(5, cluster.TimeoutSeconds))
            },
            HealthCheck = cluster.CircuitBreakerEnabled
                ? new HealthCheckConfig
                {
                    Active = new ActiveHealthCheckConfig
                    {
                        Enabled = !isDevelopment,
                        Interval = TimeSpan.FromSeconds(15),
                        Timeout = TimeSpan.FromSeconds(3),
                        Path = healthPath,
                        Policy = "ConsecutiveFailures"
                    },
                    Passive = new PassiveHealthCheckConfig
                    {
                        Enabled = true,
                        Policy = "TransportFailureRate",
                        ReactivationPeriod = TimeSpan.FromSeconds(30)
                    },
                    AvailableDestinationsPolicy = HealthCheckConstants.AvailableDestinations.HealthyAndUnknown
                }
                : null,
            SessionAffinity = cluster.ClusterId.Contains("notif", StringComparison.OrdinalIgnoreCase)
                ? new SessionAffinityConfig
                {
                    Enabled = true,
                    Policy = "Cookie",
                    FailurePolicy = "Redistribute",
                    AffinityKeyName = $".Yarp.Affinity.{cluster.ClusterId}"
                }
                : null
        };
    }

    private static RouteConfig? ToRoute(Domain.Entities.GatewayRoute route)
    {
        if (route.Cluster == null)
        {
            return null;
        }

        var methods = ParseStringList(route.MethodsJson);
        var transforms = ParseTransforms(route.TransformsJson);
        var metadata = new Dictionary<string, string>
        {
            ["isPublic"] = route.IsPublic ? "true" : "false"
        };
        if (route.RateLimitPolicy != null)
        {
            metadata["ratePolicy"] = route.RateLimitPolicy.Name;
            if (route.RateLimitPolicy.RequestsPerMinute.HasValue)
            {
                metadata["rpm"] = route.RateLimitPolicy.RequestsPerMinute.Value.ToString();
            }

            metadata["rateKey"] = route.RateLimitPolicy.KeyType.ToString();
        }

        return new RouteConfig
        {
            RouteId = route.RouteId,
            ClusterId = route.Cluster.ClusterId,
            Match = new RouteMatch
            {
                Path = route.PathMatch,
                Methods = methods.Count == 0 ? null : methods
            },
            Transforms = transforms,
            Timeout = route.TimeoutSeconds.HasValue ? TimeSpan.FromSeconds(route.TimeoutSeconds.Value) : null,
            Metadata = metadata
        };
    }

    private static List<string> ParseStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<Dictionary<string, string>>? ParseTransforms(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var list = new List<Dictionary<string, string>>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in item.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
                }

                if (dict.Count > 0)
                {
                    list.Add(dict);
                }
            }

            return list.Count == 0 ? null : list;
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class InMemoryProxyConfig : IProxyConfig
{
    private readonly CancellationTokenSource _cts = new();

    public InMemoryProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    public void SignalChange()
    {
        _cts.Cancel();
    }
}
