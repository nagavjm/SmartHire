using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHire.Infrastructure.Auth;
using SmartHire.Services;

namespace SmartHire.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AuthServiceExtensions.RequireAppAccessPolicy)]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogQueryService _auditLogQueryService;

    public AuditLogsController(IAuditLogQueryService auditLogQueryService)
    {
        _auditLogQueryService = auditLogQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int take, CancellationToken cancellationToken)
    {
        var logs = await _auditLogQueryService.GetRecentAsync(take <= 0 ? 100 : take, cancellationToken);
        return Ok(logs);
    }
}
