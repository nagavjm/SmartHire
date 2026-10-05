using SmartHire.Entities;

namespace SmartHire.Services;

public record IndexingJobDto(
    int Id,
    IndexingJobStatus Status,
    int DocumentsDiscovered,
    int DocumentsIndexed,
    int DocumentsFailed,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorSummary);

/// <summary>
/// Discovers resumes in Blob Storage, tracks them as <see cref="ResumeDocument"/> rows, and
/// records each run as an <see cref="IndexingJob"/>.
/// NOTE: Text extraction (PDF/DOCX) and embedding generation are not yet implemented, so
/// discovered/changed resumes are tracked with IndexingStatus.Pending rather than Indexed.
/// </summary>
public interface IResumeSyncService
{
    /// <summary>Discovers new/modified resumes only (diffed via ContentHash).</summary>
    Task<IndexingJobDto> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Forces every resume in Blob Storage to be treated as changed and re-tracked.</summary>
    Task<IndexingJobDto> ReindexAsync(CancellationToken cancellationToken = default);

    Task<IndexingJobDto?> GetLatestStatusAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IndexingJobDto>> GetJobsAsync(CancellationToken cancellationToken = default);
}
