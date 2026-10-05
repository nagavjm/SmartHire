using Microsoft.EntityFrameworkCore;
using SmartHire.Data;

namespace SmartHire.Services;

public class AuditLogQueryService : IAuditLogQueryService
{
    private readonly SmartHireDbContext _context;

    public AuditLogQueryService(SmartHireDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        return await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(take)
            .Select(a => new AuditLogDto(
                a.Id,
                a.UserId,
                a.EventType,
                a.Description,
                a.Metadata,
                a.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }
}
