namespace ApiGatewayApi.Domain.Enums;

public enum DestinationHealthStatus
{
    Unknown = 0,
    Healthy = 1,
    Unhealthy = 2
}

public enum IpRuleAction
{
    Allow = 1,
    Deny = 2
}

public enum RateLimitKeyType
{
    Ip = 1,
    User = 2,
    ApiKey = 3,
    Route = 4,
    Global = 5
}

public enum AlertMetric
{
    ErrorRate = 1,
    LatencyP95 = 2,
    ServiceDown = 3,
    RequestVolume = 4
}

public enum LoadBalancingPolicy
{
    RoundRobin = 1,
    LeastRequests = 2,
    Weighted = 3
}
