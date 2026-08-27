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
    public string Status { get; set; } = "UNKNOWN";
    public double LatencyMs { get; set; }
}

[ApiController]
[Route("api/gateway/swagger-docs")]
public class SwaggerAggregatorController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly string[] DefaultProbePaths =
    {
        "/swagger/v1/swagger.json",
        "/swagger/index.html",
        "/.well-known/jwks.json",
        "/health",
        "/swagger.json",
        "/"
    };

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
        client.Timeout = TimeSpan.FromSeconds(2.5);

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
            var configuredHealthPath = child["HealthEndpoint"];

            if (string.IsNullOrWhiteSpace(baseUrl)) return null;

            var (status, latency) = await CheckServiceHealthAsync(client, baseUrl, configuredHealthPath);

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

    private static async Task<(string Status, double LatencyMs)> CheckServiceHealthAsync(
        HttpClient client,
        string baseUrl,
        string? configuredHealthPath)
    {
        var candidatePaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(configuredHealthPath))
        {
            candidatePaths.Add(configuredHealthPath);
        }
        foreach (var p in DefaultProbePaths)
        {
            if (!candidatePaths.Contains(p))
            {
                candidatePaths.Add(p);
            }
        }

        foreach (var path in candidatePaths)
        {
            try
            {
                var targetUrl = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? path
                    : $"{baseUrl}{path}";

                var sw = Stopwatch.StartNew();
                using var res = await client.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();

                var latency = Math.Round(sw.Elapsed.TotalMilliseconds, 1);
                var statusCode = (int)res.StatusCode;
                if (statusCode < 500)
                {
                    return ("HEALTHY", latency);
                }
            }
            catch
            {
                //! Try next probe candidate path
            }
        }

        return ("UNHEALTHY", 0);
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
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        var candidateUrls = new[]
        {
            $"{baseUrl}/swagger/v1/swagger.json",
            $"{baseUrl}/swagger.json",
            $"{baseUrl}/v1/swagger.json",
            $"{baseUrl}/api/v1/swagger.json"
        };

        foreach (var url in candidateUrls)
        {
            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return Content(content, "application/json");
                }
            }
            catch { }
        }

        return StatusCode(502, new
        {
            message = $"Không thể tải OpenAPI Swagger từ dịch vụ '{serviceKey}' tại {baseUrl}."
        });
    }
}
