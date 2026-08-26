using ApiGatewayApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiGatewayApi.Controllers;

public class RequestLogSearchDto
{
    public string? Keyword { get; set; }
    public string? Method { get; set; }
    public int? StatusCode { get; set; }
    public string? TargetCluster { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

[ApiController]
[Route("api/gateway/logs")]
public class RequestLogsController : ControllerBase
{
    private readonly ITraceLogStore _traceStore;

    public RequestLogsController(ITraceLogStore traceStore)
    {
        _traceStore = traceStore;
    }

    [HttpPost("search")]
    public IActionResult SearchLogs([FromBody] RequestLogSearchDto dto)
    {
        var logs = _traceStore.GetRecent(500);

        if (!string.IsNullOrWhiteSpace(dto.Keyword))
        {
            var kw = dto.Keyword.Trim();
            logs = logs.Where(x => 
                x.Path.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.CorrelationId.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                x.ClientIp.Contains(kw, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(dto.Method))
        {
            logs = logs.Where(x => x.Method.Equals(dto.Method, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (dto.StatusCode.HasValue)
        {
            logs = logs.Where(x => x.StatusCode == dto.StatusCode.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(dto.TargetCluster))
        {
            logs = logs.Where(x => string.Equals(x.TargetCluster, dto.TargetCluster, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var total = logs.Count;
        var paged = logs
            .Skip((dto.PageIndex - 1) * dto.PageSize)
            .Take(dto.PageSize)
            .ToList();

        return Ok(new
        {
            Items = paged,
            TotalCount = total,
            dto.PageIndex,
            dto.PageSize,
            TotalPages = (int)Math.Ceiling((double)total / dto.PageSize)
        });
    }

    [HttpPost("clear")]
    public IActionResult ClearLogs()
    {
        _traceStore.Clear();
        return Ok(new { Success = true, Message = "Đã xóa toàn bộ nhật ký truy vết yêu cầu." });
    }
}
