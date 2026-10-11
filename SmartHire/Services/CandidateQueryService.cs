using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartHire.Data;
using SmartHire.Infrastructure.EventGrid;

namespace SmartHire.Services;

public class CandidateQueryService : ICandidateQueryService
{
    private static readonly string[] LifecycleEventTypes =
    [
        CandidateLifecycleEventTypes.CandidateShortlisted,
        CandidateLifecycleEventTypes.VerificationCompleted,
        CandidateLifecycleEventTypes.VerificationFailed,
        CandidateLifecycleEventTypes.OfferReleased
    ];

    private readonly SmartHireDbContext _context;

    public CandidateQueryService(SmartHireDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CandidateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var candidates = await _context.CandidateProfiles
            .AsNoTracking()
            .OrderBy(c => c.CandidateName)
            .Select(c => new
            {
                c.Id,
                c.ResumeDocumentId,
                c.CandidateName,
                c.Email,
                c.Phone,
                c.Location,
                c.TotalExperienceYears
            })
            .ToListAsync(cancellationToken);

        var statusMap = await GetLatestStatusMapAsync(cancellationToken);

        return candidates
            .Select(c => new CandidateSummaryDto(
                c.Id,
                c.ResumeDocumentId,
                c.CandidateName,
                c.Email,
                c.Phone,
                c.Location,
                c.TotalExperienceYears,
                statusMap.GetValueOrDefault(c.Id, "New")))
            .ToList();
    }

    public async Task<CandidateDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var candidate = await _context.CandidateProfiles
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
                c.ResumeDocument != null ? c.ResumeDocument.BlobUrl : null,
                null))
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        var status = await GetCandidateStatusAsync(id, cancellationToken);
        return candidate with { Status = status };
    }

    private async Task<Dictionary<int, string>> GetLatestStatusMapAsync(CancellationToken cancellationToken)
    {
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => LifecycleEventTypes.Contains(a.EventType))
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var map = new Dictionary<int, string>();
        foreach (var log in logs)
        {
            if (string.IsNullOrWhiteSpace(log.Metadata))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(log.Metadata);
                if (doc.RootElement.TryGetProperty("candidateId", out var idProp) &&
                    int.TryParse(idProp.GetString(), out var candidateId))
                {
                    if (!map.ContainsKey(candidateId))
                    {
                        var status = doc.RootElement.TryGetProperty("status", out var statusProp)
                            ? statusProp.GetString()
                            : log.EventType;

                        map[candidateId] = status ?? "New";
                    }
                }
            }
            catch
            {
                // Silently ignore malformed JSON in legacy audit entries
            }
        }

        return map;
    }

    private async Task<string> GetCandidateStatusAsync(int candidateId, CancellationToken cancellationToken)
    {
        var log = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => LifecycleEventTypes.Contains(a.EventType) && a.Metadata != null && a.Metadata.Contains($"\"candidateId\":\"{candidateId}\""))
            .OrderByDescending(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (log is null || string.IsNullOrWhiteSpace(log.Metadata))
        {
            return "New";
        }

        try
        {
            using var doc = JsonDocument.Parse(log.Metadata);
            if (doc.RootElement.TryGetProperty("status", out var statusProp))
            {
                return statusProp.GetString() ?? "New";
            }
        }
        catch
        {
            return log.EventType;
        }

        return "New";
    }
}
