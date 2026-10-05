using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHire.Infrastructure.Auth;
using SmartHire.Services;

namespace SmartHire.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize(Policy = AuthServiceExtensions.RequireAppAccessPolicy)]
public class SyncController : ControllerBase
{
    private readonly IResumeSyncService _resumeSyncService;

    public SyncController(IResumeSyncService resumeSyncService)
    {
        _resumeSyncService = resumeSyncService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        var job = await _resumeSyncService.StartAsync(cancellationToken);
        return Ok(job);
    }

    [HttpPost("reindex")]
    public async Task<IActionResult> Reindex(CancellationToken cancellationToken)
    {
        var job = await _resumeSyncService.ReindexAsync(cancellationToken);
        return Ok(job);
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var job = await _resumeSyncService.GetLatestStatusAsync(cancellationToken);
        return job is null ? NotFound() : Ok(job);
    }

    [HttpGet("jobs")]
    public async Task<IActionResult> Jobs(CancellationToken cancellationToken)
    {
        var jobs = await _resumeSyncService.GetJobsAsync(cancellationToken);
        return Ok(jobs);
    }
}
