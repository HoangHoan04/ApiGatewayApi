using ApiGatewayApi.Application.Common.Interfaces;
using ApiGatewayApi.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiGatewayApi.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/gateway")]
public class GatewayControlController : ControllerBase
{
    private readonly IGatewayControlService _control;

    public GatewayControlController(IGatewayControlService control)
    {
        _control = control;
    }

    [HttpPost("snapshot")]
    public async Task<ActionResult<GatewaySnapshotDto>> Snapshot() =>
        Ok(await _control.GetSnapshotAsync());

    [HttpPost("routes/upsert")]
    public async Task<ActionResult<GatewayRouteDetailDto>> UpsertRoute([FromBody] UpsertRouteRequest request) =>
        Ok(await _control.UpsertRouteAsync(request));

    [HttpPost("routes/delete")]
    public async Task<IActionResult> DeleteRoute([FromBody] IdRequest request) =>
        Ok(new { success = await _control.DeleteRouteAsync(request.Id) });

    [HttpPost("routes/set-active")]
    public async Task<IActionResult> SetRouteActive([FromBody] SetActiveRequest request) =>
        Ok(new { success = await _control.SetRouteActiveAsync(request.Id, request.IsActive) });

    [HttpPost("routes/test")]
    public async Task<ActionResult<TestRouteResultDto>> TestRoute([FromBody] TestRouteRequest request) =>
        Ok(await _control.TestRouteAsync(request));

    [HttpPost("rate-policies/upsert")]
    public async Task<ActionResult<RateLimitPolicyDto>> UpsertRate([FromBody] UpsertRateLimitRequest request) =>
        Ok(await _control.UpsertRatePolicyAsync(request));

    [HttpPost("rate-policies/delete")]
    public async Task<IActionResult> DeleteRate([FromBody] IdRequest request) =>
        Ok(new { success = await _control.DeleteRatePolicyAsync(request.Id) });

    [HttpPost("ip-rules/upsert")]
    public async Task<ActionResult<IpRuleDto>> UpsertIp([FromBody] UpsertIpRuleRequest request) =>
        Ok(await _control.UpsertIpRuleAsync(request));

    [HttpPost("ip-rules/delete")]
    public async Task<IActionResult> DeleteIp([FromBody] IdRequest request) =>
        Ok(new { success = await _control.DeleteIpRuleAsync(request.Id) });

    [HttpPost("maintenance/upsert")]
    public async Task<ActionResult<MaintenanceDto>> UpsertMaintenance([FromBody] UpsertMaintenanceRequest request) =>
        Ok(await _control.UpsertMaintenanceAsync(request));

    [HttpPost("maintenance/delete")]
    public async Task<IActionResult> DeleteMaintenance([FromBody] IdRequest request) =>
        Ok(new { success = await _control.DeleteMaintenanceAsync(request.Id) });

    [AllowAnonymous]
    [HttpPost("security/status")]
    [HttpGet("security/status")]
    public async Task<ActionResult<SecurityStatusDto>> Security() =>
        Ok(await _control.GetSecurityStatusAsync());

    [HttpPost("services/upsert")]
    public async Task<ActionResult<GatewayServiceDto>> UpsertService([FromBody] UpsertServiceRequest request) =>
        Ok(await _control.UpsertServiceAsync(request));

    [HttpPost("services/delete")]
    public async Task<IActionResult> DeleteService([FromBody] IdRequest request) =>
        Ok(new { success = await _control.DeleteServiceAsync(request.Id) });

    [HttpPost("clusters/upsert")]
    public async Task<ActionResult<GatewayClusterDetailDto>> UpsertCluster([FromBody] UpsertClusterRequest request) =>
        Ok(await _control.UpsertClusterAsync(request));

    [HttpPost("destinations/upsert")]
    public async Task<ActionResult<GatewayDestinationDto>> UpsertDestination([FromBody] UpsertDestinationRequest request) =>
        Ok(await _control.UpsertDestinationAsync(request));

    [HttpPost("destinations/delete")]
    public async Task<IActionResult> DeleteDestination([FromBody] IdRequest request) =>
        Ok(new { success = await _control.DeleteDestinationAsync(request.Id) });
}

public class IdRequest
{
    public Guid Id { get; set; }
}

public class SetActiveRequest
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}
