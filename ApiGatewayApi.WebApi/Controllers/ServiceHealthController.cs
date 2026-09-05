using System.Diagnostics;
using ApiGatewayApi.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiGatewayApi.WebApi.Controllers;

public class ServiceHealthItem
{
    public string ServiceKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Status { get; set; } = "UNKNOWN";
    public long LatencyMs { get; set; }
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

[ApiController]
[Authorize]
[Route("api/gateway/health")]
public class ServiceHealthController : ControllerBase
{
    private readonly IGatewayDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public ServiceHealthController(IGatewayDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("check-all")]
    public async Task<IActionResult> CheckAllServices()
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(3);
        var services = await _db.GatewayServices.Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();

        var tasks = services.Select(s => CheckAsync(client, s.Code, s.Name, s.BaseUrl, BuildHealthUrl(s.BaseUrl, s.HealthPath), s.Description ?? "", s.Icon ?? "api"));
        var results = await Task.WhenAll(tasks);
        var healthyCount = results.Count(r => r.Status == "HEALTHY");
        return Ok(new
        {
            TotalServices = results.Length,
            HealthyServices = healthyCount,
            OverallStatus = healthyCount == results.Length ? "HEALTHY" : (healthyCount > 0 ? "DEGRADED" : "UNHEALTHY"),
            CheckedAt = DateTime.UtcNow,
            Services = results
        });
    }

    private static string BuildHealthUrl(string baseUrl, string healthPath)
    {
        var path = string.IsNullOrWhiteSpace(healthPath) ? "/health" : healthPath;
        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        if (path.Contains("swagger", StringComparison.OrdinalIgnoreCase) || path == "/")
        {
            path = "/health";
        }

        return baseUrl.TrimEnd('/') + path;
    }

    private static async Task<ServiceHealthItem> CheckAsync(
        HttpClient client, string key, string name, string baseUrl, string healthUrl, string desc, string icon)
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
            var code = (int)response.StatusCode;
            if (code is >= 200 and < 300)
            {
                item.Status = "HEALTHY";
            }
            else
            {
                item.Status = "UNHEALTHY";
                item.ErrorMessage = $"HTTP {code} {response.ReasonPhrase}";
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
