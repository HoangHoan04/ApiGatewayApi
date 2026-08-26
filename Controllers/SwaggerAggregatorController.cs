using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ApiGatewayApi.Controllers;

public class SwaggerDocServiceItem
{
    public string ServiceKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string SwaggerUrl { get; set; } = string.Empty;
    public string ProxyDocUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Status { get; set; } = "UNKNOWN"; // HEALTHY, UNHEALTHY, UNKNOWN
    public double LatencyMs { get; set; }
}

[ApiController]
[Route("api/gateway/swagger-docs")]
public class SwaggerAggregatorController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public SwaggerAggregatorController(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Lấy danh sách tài liệu OpenAPI của toàn bộ Microservices được cấu hình cùng trạng thái hoạt động thực tế
    /// </summary>
    [HttpPost("list")]
    public async Task<IActionResult> GetSwaggerDocsList()
    {
        var servicesSection = _config.GetSection("DownstreamServices");
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(3);

        var items = new List<SwaggerDocServiceItem>
        {
            new()
            {
                ServiceKey = "Gateway",
                Name = "API Gateway Management",
                BaseUrl = "http://localhost:8000",
                SwaggerUrl = "http://localhost:8000/swagger/v1/swagger.json",
                ProxyDocUrl = "/swagger/v1/swagger.json",
                Description = "Endpoints quản lý bảng định tuyến YARP, giám sát lưu lượng và nhật ký truy vết.",
                Icon = "gateway",
                Status = "HEALTHY",
                LatencyMs = 0.5
            }
        };

        var tasks = servicesSection.GetChildren().Select(async child =>
        {
            var key = child.Key;
            var name = child["Name"] ?? key;
            var baseUrl = (child["BaseUrl"] ?? string.Empty).TrimEnd('/');
            var desc = child["Description"] ?? string.Empty;
            var icon = child["Icon"] ?? "api";
            var healthPath = child["HealthEndpoint"] ?? "/swagger/v1/swagger.json";

            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            var status = "UNHEALTHY";
            double latency = 0;

            try
            {
                var healthUrl = healthPath.StartsWith("http") ? healthPath : $"{baseUrl}{healthPath}";
                var sw = Stopwatch.StartNew();
                var res = await client.GetAsync(healthUrl);
                sw.Stop();
                latency = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
                if (res.IsSuccessStatusCode)
                {
                    status = "HEALTHY";
                }
            }
            catch
            {
                status = "UNHEALTHY";
            }

            return new SwaggerDocServiceItem
            {
                ServiceKey = key,
                Name = name,
                BaseUrl = baseUrl,
                SwaggerUrl = $"{baseUrl}/swagger/v1/swagger.json",
                ProxyDocUrl = $"/api/gateway/swagger-docs/proxy/{key}",
                Description = desc,
                Icon = icon,
                Status = status,
                LatencyMs = latency
            };
        });

        var results = await Task.WhenAll(tasks);
        foreach (var res in results)
        {
            if (res != null) items.Add(res);
        }

        return Ok(items);
    }

    /// <summary>
    /// Proxy OpenAPI JSON từ downstream microservice để tránh lỗi CORS và chuẩn hóa Server URL
    /// </summary>
    [HttpGet("proxy/{serviceKey}")]
    public async Task<IActionResult> ProxySwaggerJson(string serviceKey)
    {
        var serviceSection = _config.GetSection($"DownstreamServices:{serviceKey}");
        if (!serviceSection.Exists())
        {
            return NotFound(new { message = $"Không tìm thấy dịch vụ '{serviceKey}' trong cấu hình DownstreamServices." });
        }

        var baseUrl = (serviceSection["BaseUrl"] ?? "http://localhost:5000").TrimEnd('/');
        var swaggerJsonUrl = $"{baseUrl}/swagger/v1/swagger.json";

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            var response = await client.GetAsync(swaggerJsonUrl);
            if (!response.IsSuccessStatusCode)
            {
                var fallbackResponse = await client.GetAsync($"{baseUrl}/swagger.json");
                if (!fallbackResponse.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        message = $"Không thể tải OpenAPI Swagger từ dịch vụ '{serviceKey}' tại {swaggerJsonUrl}. Mã lỗi: {(int)response.StatusCode}"
                    });
                }
                response = fallbackResponse;
            }

            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(502, new
            {
                message = $"Lỗi kết nối tới Swagger của dịch vụ '{serviceKey}' ({swaggerJsonUrl}): {ex.Message}"
            });
        }
    }
}
