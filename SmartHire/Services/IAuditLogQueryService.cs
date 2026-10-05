namespace SmartHire.Services;

public record AuditLogDto(
    int Id,
    int? UserId,
    string EventType,
    string? Description,
    string? Metadata,
    DateTime CreatedAtUtc);

public interface IAuditLogQueryService
{
    Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int take = 100, CancellationToken cancellationToken = default);
}
