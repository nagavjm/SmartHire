using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHire.Infrastructure.Auth;
using SmartHire.Services;

namespace SmartHire.Controllers;

[ApiController]
[Route("api/resumes")]
[Authorize(Policy = AuthServiceExtensions.RequireAppAccessPolicy)]
public class ResumesController : ControllerBase
{
    private readonly IResumeQueryService _resumeQueryService;

    public ResumesController(IResumeQueryService resumeQueryService)
    {
        _resumeQueryService = resumeQueryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var resumes = await _resumeQueryService.GetAllAsync(cancellationToken);
        return Ok(resumes);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var resume = await _resumeQueryService.GetByIdAsync(id, cancellationToken);
        return resume is null ? NotFound() : Ok(resume);
    }
}
