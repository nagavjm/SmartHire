using SmartHire.Entities;

namespace SmartHire.Services;

public record ResumeSummaryDto(
    int Id,
    string BlobName,
    string FileExtension,
    long SizeInBytes,
    IndexingStatus Status,
    DateTime BlobLastModifiedUtc,
    DateTime? LastIndexedAtUtc);

public record ResumeDetailDto(
    int Id,
    string BlobName,
    string BlobUrl,
    string ContainerName,
    string FileExtension,
    long SizeInBytes,
    IndexingStatus Status,
    DateTime BlobLastModifiedUtc,
    DateTime? LastIndexedAtUtc,
    string? FailureReason,
    string? CandidateName);

public interface IResumeQueryService
{
    Task<IReadOnlyList<ResumeSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ResumeDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
