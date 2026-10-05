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

    public CandidatesController(ICandidateQueryService candidateQueryService)
    {
        _candidateQueryService = candidateQueryService;
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
}
