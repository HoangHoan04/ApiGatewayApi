using System.Net;
using System.Text.Json;

namespace ApiGatewayApi.Middlewares;

public class GatewayExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayExceptionMiddleware> _logger;

    public GatewayExceptionMiddleware(RequestDelegate next, ILogger<GatewayExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ApiGateway] Lỗi không xử lý được khi chuyển tiếp request: {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)HttpStatusCode.BadGateway;

        var response = new
        {
            type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.3",
            title = "Lỗi kết nối Cổng Gateway (Bad Gateway)",
            status = context.Response.StatusCode,
            detail = "Không thể kết nối hoặc dịch vụ đích tạm thời không phản hồi. Vui lòng thử lại sau.",
            instance = context.Request.Path.Value,
            correlationId = context.Items["X-Correlation-Id"]?.ToString() ?? Guid.NewGuid().ToString("N")
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
