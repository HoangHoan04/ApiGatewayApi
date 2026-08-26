using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiGatewayApi.Controllers;

public class ServiceHealthItem
{
    public string ServiceKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Status { get; set; } = "UNKNOWN"; // HEALTHY, UNHEALTHY, STANDBY
    public long LatencyMs { get; set; }
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

[ApiController]
[Route("api/gateway/health")]
public class ServiceHealthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public ServiceHealthController(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("check-all")]
    public async Task<IActionResult> CheckAllServices()
    {
        var servicesSection = _config.GetSection("DownstreamServices");
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(3);

        var tasks = new List<Task<ServiceHealthItem>>();

        foreach (var child in servicesSection.GetChildren())
        {
            var key = child.Key;
            var name = child["Name"] ?? key;
            var baseUrl = child["BaseUrl"] ?? "http://localhost:5000";
            var healthUrl = child["HealthEndpoint"] ?? baseUrl;
            var desc = child["Description"] ?? string.Empty;
            var icon = child["Icon"] ?? "api";

            tasks.Add(CheckSingleServiceAsync(client, key, name, baseUrl, healthUrl, desc, icon));
        }

        var results = await Task.WhenAll(tasks);

        var healthyCount = results.Count(r => r.Status == "HEALTHY");
        var totalCount = results.Length;

        return Ok(new
        {
            TotalServices = totalCount,
            HealthyServices = healthyCount,
            OverallStatus = healthyCount == totalCount ? "HEALTHY" : (healthyCount > 0 ? "DEGRADED" : "UNHEALTHY"),
            CheckedAt = DateTime.UtcNow,
            Services = results
        });
    }

    private static async Task<ServiceHealthItem> CheckSingleServiceAsync(
        HttpClient client, 
        string key, 
        string name, 
        string baseUrl, 
        string healthUrl, 
        string desc, 
        string icon)
    {
        var item = new ServiceHealthItem
        {
            ServiceKey = key,
            Name = name,
            BaseUrl = baseUrl,
            Description = desc,
            Icon = icon,
            LastCheckedAt = DateTime.UtcNow
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await client.GetAsync(healthUrl);
            sw.Stop();
            item.LatencyMs = sw.ElapsedMilliseconds;

            // Any response code below 500 means the server process is alive and responding
            if ((int)response.StatusCode < 500)
            {
                item.Status = "HEALTHY";
            }
            else
            {
                item.Status = "UNHEALTHY";
                item.ErrorMessage = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            item.LatencyMs = sw.ElapsedMilliseconds;
            item.Status = "UNHEALTHY";
            item.ErrorMessage = ex.InnerException?.Message ?? ex.Message;
        }

        return item;
    }
}
