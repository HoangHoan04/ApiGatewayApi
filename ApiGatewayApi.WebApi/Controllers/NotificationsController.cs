using Microsoft.AspNetCore.Mvc;

namespace ApiGatewayApi.WebApi.Controllers;

[ApiController]
[Route("api/notification")]
public class NotificationsController : ControllerBase
{
    [HttpGet("unread-count")]
    public IActionResult GetUnreadCount()
    {
        return Ok(0);
    }

    [HttpPost("pagination")]
    public IActionResult GetPagination([FromBody] object? filter)
    {
        return Ok(new
        {
            items = Array.Empty<object>(),
            totalCount = 0,
            pageIndex = 1,
            pageSize = 10,
            totalPages = 0,
            hasPreviousPage = false,
            hasNextPage = false
        });
    }

    [HttpPost("mark-read")]
    public IActionResult MarkRead([FromBody] object? request)
    {
        return Ok(true);
    }

    [HttpPost("mark-all-read")]
    public IActionResult MarkAllRead()
    {
        return Ok(0);
    }
}
