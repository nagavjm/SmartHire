using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHire.Infrastructure.Auth;
using SmartHire.Services;

namespace SmartHire.Controllers;

[ApiController]
[Route("api/candidates")]
[Authorize(Policy = AuthServiceExtensions.RequireAppAccessPolicy)]
public class CandidatesController : ControllerBase
{
    private readonly ICandidateQueryService _candidateQueryService;
    private readonly ICandidateLifecycleService _candidateLifecycleService;

    public CandidatesController(
        ICandidateQueryService candidateQueryService,
        ICandidateLifecycleService candidateLifecycleService)
    {
        _candidateQueryService = candidateQueryService;
        _candidateLifecycleService = candidateLifecycleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var candidates = await _candidateQueryService.GetAllAsync(cancellationToken);
        return Ok(candidates);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var candidate = await _candidateQueryService.GetByIdAsync(id, cancellationToken);
        return candidate is null ? NotFound() : Ok(candidate);
    }

    /// <summary>
    /// Executes a candidate lifecycle transition and publishes the event to Azure Event Grid.
    /// </summary>
    [HttpPost("{id:int}/lifecycle-action")]
    public async Task<IActionResult> TriggerLifecycleAction(
        int id,
        [FromBody] CandidateLifecycleActionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _candidateLifecycleService.ExecuteActionAsync(id, request.Action, request.JobId, cancellationToken);
        if (!result.Success && string.IsNullOrEmpty(result.Status))
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("{id:int}/shortlist")]
    public Task<IActionResult> Shortlist(int id, [FromQuery] string? jobId, CancellationToken ct) =>
        TriggerLifecycleAction(id, new CandidateLifecycleActionRequest { Action = "CandidateShortlisted", JobId = jobId }, ct);

    [HttpPost("{id:int}/verification-completed")]
    public Task<IActionResult> VerificationCompleted(int id, [FromQuery] string? jobId, CancellationToken ct) =>
        TriggerLifecycleAction(id, new CandidateLifecycleActionRequest { Action = "VerificationCompleted", JobId = jobId }, ct);

    [HttpPost("{id:int}/verification-failed")]
    public Task<IActionResult> VerificationFailed(int id, [FromQuery] string? jobId, CancellationToken ct) =>
        TriggerLifecycleAction(id, new CandidateLifecycleActionRequest { Action = "VerificationFailed", JobId = jobId }, ct);

    [HttpPost("{id:int}/offer-released")]
    public Task<IActionResult> OfferReleased(int id, [FromQuery] string? jobId, CancellationToken ct) =>
        TriggerLifecycleAction(id, new CandidateLifecycleActionRequest { Action = "OfferReleased", JobId = jobId }, ct);
}
