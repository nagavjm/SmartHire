using Microsoft.EntityFrameworkCore;
using SmartHire.Data;

namespace SmartHire.Services;

public class CandidateQueryService : ICandidateQueryService
{
    private readonly SmartHireDbContext _context;

    public CandidateQueryService(SmartHireDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CandidateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CandidateProfiles
            .AsNoTracking()
            .OrderBy(c => c.CandidateName)
            .Select(c => new CandidateSummaryDto(
                c.Id,
                c.ResumeDocumentId,
                c.CandidateName,
                c.Email,
                c.Phone,
                c.Location,
                c.TotalExperienceYears))
            .ToListAsync(cancellationToken);
    }

    public async Task<CandidateDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.CandidateProfiles
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CandidateDetailDto(
                c.Id,
                c.ResumeDocumentId,
                c.CandidateName,
                c.Email,
                c.Phone,
                c.Location,
                c.TotalExperienceYears,
                c.Skills,
                c.ResumeSummary,
                c.ResumeDocument != null ? c.ResumeDocument.BlobUrl : null))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
