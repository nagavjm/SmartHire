using SmartHire.Controllers;

namespace SmartHire.Services;

public record CandidateMatchDto(
    int? CandidateProfileId,
    int ResumeDocumentId,
    string CandidateName,
    double MatchScore,
    double? SkillMatchScore,
    double? ExperienceMatchScore,
    IReadOnlyList<string>? Strengths,
    IReadOnlyList<string>? MissingSkills,
    string? Recommendation,
    string? ResumeSummary,
    int Rank);

public record ScreeningOutcomeDto(
    int ScreeningRequestId,
    IReadOnlyList<CandidateMatchDto> Candidates);

/// <summary>
/// RAG orchestration: embed the job query, retrieve similar resumes via vector search,
/// and (for ranking) score each candidate with Azure OpenAI GPT.
/// </summary>
public interface IScreeningService
{
    /// <summary>Retrieval-only: embeds the query and returns the top vector-similarity matches (cheap, no GPT calls).</summary>
    Task<ScreeningOutcomeDto> SearchAsync(ScreeningSearchRequest request, int requestedByUserId, CancellationToken cancellationToken = default);

    /// <summary>Full RAG pipeline: retrieval + GPT-based scoring/ranking of the shortlisted candidates.</summary>
    Task<ScreeningOutcomeDto> RankAsync(ScreeningSearchRequest request, int requestedByUserId, CancellationToken cancellationToken = default);
}
