using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHire.Data;
using SmartHire.Infrastructure.Auth;
using SmartHire.Services;

namespace SmartHire.Controllers;

public class ScreeningSearchRequest
{
    public string JobTitle { get; set; } = string.Empty;
    public string JobDescription { get; set; } = string.Empty;
    public string RequiredSkills { get; set; } = string.Empty;
    public int MinimumExperienceYears { get; set; }
    public int? PreferredExperienceYears { get; set; }
    public string? Location { get; set; }
}

[ApiController]
[Route("api/screening")]
[Authorize(Policy = AuthServiceExtensions.RequireAppAccessPolicy)]
public class ScreeningController : ControllerBase
{
    private readonly IScreeningService _screeningService;
    private readonly SmartHireDbContext _db;

    public ScreeningController(IScreeningService screeningService, SmartHireDbContext db)
    {
        _screeningService = screeningService;
        _db = db;
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] ScreeningSearchRequest request, CancellationToken cancellationToken)
    {
        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _screeningService.SearchAsync(request, userId.Value, cancellationToken);
        return Ok(result);
    }

    [HttpPost("rank")]
    public async Task<IActionResult> Rank([FromBody] ScreeningSearchRequest request, CancellationToken cancellationToken)
    {
        var userId = await ResolveCurrentUserIdAsync(cancellationToken);
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _screeningService.RankAsync(request, userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Resolves the current local SmartHire User.Id. For local-login sessions, NameIdentifier
    /// already holds the local id. For AuthBridge OIDC sessions, NameIdentifier holds the OIDC
    /// "sub" claim, so the local user is looked up by SubjectId instead.
    /// </summary>
    private async Task<int?> ResolveCurrentUserIdAsync(CancellationToken cancellationToken)
    {
        var nameIdentifier = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(nameIdentifier))
        {
            return null;
        }

        if (int.TryParse(nameIdentifier, out var localUserId))
        {
            return localUserId;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.SubjectId == nameIdentifier, cancellationToken);
        return user?.Id;
    }
}
