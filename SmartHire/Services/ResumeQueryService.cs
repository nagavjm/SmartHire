using Microsoft.EntityFrameworkCore;
using SmartHire.Data;

namespace SmartHire.Services;

public class ResumeQueryService : IResumeQueryService
{
    private readonly SmartHireDbContext _context;

    public ResumeQueryService(SmartHireDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ResumeSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ResumeDocuments
            .AsNoTracking()
            .OrderByDescending(r => r.BlobLastModifiedUtc)
            .Select(r => new ResumeSummaryDto(
                r.Id,
                r.BlobName,
                r.FileExtension,
                r.SizeInBytes,
                r.Status,
                r.BlobLastModifiedUtc,
                r.LastIndexedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResumeDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.ResumeDocuments
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ResumeDetailDto(
                r.Id,
                r.BlobName,
                r.BlobUrl,
                r.ContainerName,
                r.FileExtension,
                r.SizeInBytes,
                r.Status,
                r.BlobLastModifiedUtc,
                r.LastIndexedAtUtc,
                r.FailureReason,
                r.CandidateProfile != null ? r.CandidateProfile.CandidateName : null))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
