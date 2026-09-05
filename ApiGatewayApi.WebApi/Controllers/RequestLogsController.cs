using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiGatewayApi.WebApi.Controllers;

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
[Authorize]
[Route("api/gateway/logs")]
public class RequestLogsController : ControllerBase
{
    private readonly IGatewayDbContext _db;
    private readonly ITraceLogStore _traceStore;

    public RequestLogsController(IGatewayDbContext db, ITraceLogStore traceStore)
    {
        _db = db;
        _traceStore = traceStore;
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchLogs([FromBody] RequestLogSearchDto dto)
    {
        var query = _db.RequestLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(dto.Keyword))
        {
            var kw = dto.Keyword.Trim();
            query = query.Where(x => x.Path.Contains(kw) || x.CorrelationId.Contains(kw) || (x.ClientIp != null && x.ClientIp.Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(dto.Method))
        {
            query = query.Where(x => x.Method == dto.Method);
        }

        if (dto.StatusCode.HasValue)
        {
            query = query.Where(x => x.StatusCode == dto.StatusCode.Value);
        }

        if (!string.IsNullOrWhiteSpace(dto.TargetCluster))
        {
            query = query.Where(x => x.TargetCluster == dto.TargetCluster);
        }

        var total = await query.CountAsync();
        var pageIndex = Math.Max(1, dto.PageIndex);
        var pageSize = Math.Clamp(dto.PageSize, 1, 200);
        var rows = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = rows.Select(x => new RequestTraceItem
        {
            Id = x.Id.ToString("N"),
            CorrelationId = x.CorrelationId,
            Timestamp = x.CreatedAt.UtcDateTime,
            Method = x.Method,
            Path = x.Path,
            QueryString = x.QueryString,
            ClientIp = x.ClientIp ?? "",
            StatusCode = x.StatusCode,
            DurationMs = x.DurationMs,
            TargetCluster = x.TargetCluster,
            UserIdentity = x.UserId?.ToString()
        }).ToList();

        if (items.Count == 0)
        {
            items = _traceStore.GetRecent(pageSize).ToList();
            total = items.Count;
        }

        return Ok(new
        {
            Items = items,
            TotalCount = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)Math.Max(total, 1) / pageSize)
        });
    }

    [HttpPost("clear")]
    public IActionResult ClearLogs()
    {
        _traceStore.Clear();
        return Ok(new { Success = true, Message = "Đã xóa nhật ký RAM. Log PostgreSQL giữ theo TTL." });
    }
}
