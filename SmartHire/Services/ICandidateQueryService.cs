namespace SmartHire.Services;

public record CandidateSummaryDto(
    int Id,
    int ResumeDocumentId,
    string CandidateName,
    string? Email,
    string? Phone,
    string? Location,
    double? TotalExperienceYears);

public record CandidateDetailDto(
    int Id,
    int ResumeDocumentId,
    string CandidateName,
    string? Email,
    string? Phone,
    string? Location,
    double? TotalExperienceYears,
    string? Skills,
    string? ResumeSummary,
    string? BlobUrl);

public interface ICandidateQueryService
{
    Task<IReadOnlyList<CandidateSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CandidateDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
