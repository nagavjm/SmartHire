using SmartHire.Controllers;
using SmartHire.Entities;
using SmartHire.Infrastructure.OpenAi;
using SmartHire.Infrastructure.Search;
using SmartHire.Repositories;

namespace SmartHire.Services;

public class ScreeningService : IScreeningService
{
    private const int SearchTopK = 10;
    private const int RankShortlistSize = 5;

    private readonly IOpenAiService _openAiService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IUnitOfWork _unitOfWork;

    public ScreeningService(IOpenAiService openAiService, IVectorSearchService vectorSearchService, IUnitOfWork unitOfWork)
    {
        _openAiService = openAiService;
        _vectorSearchService = vectorSearchService;
        _unitOfWork = unitOfWork;
    }

    public async Task<ScreeningOutcomeDto> SearchAsync(ScreeningSearchRequest request, int requestedByUserId, CancellationToken cancellationToken = default)
    {
        var screeningRequest = await CreateScreeningRequestAsync(request, requestedByUserId, cancellationToken);

        var queryVector = await _openAiService.GenerateEmbeddingAsync(BuildQueryText(request), cancellationToken);
        var hits = await _vectorSearchService.SearchAsync(queryVector, SearchTopK, cancellationToken);

        var candidates = new List<CandidateMatchDto>();
        var rank = 1;
        foreach (var hit in hits)
        {
            if (!int.TryParse(hit.ResumeDocumentId, out var resumeDocumentId))
            {
                continue;
            }

            var candidateProfile = (await _unitOfWork.CandidateProfiles
                .FindAsync(c => c.ResumeDocumentId == resumeDocumentId, cancellationToken))
                .FirstOrDefault();

            candidates.Add(new CandidateMatchDto(
                CandidateProfileId: candidateProfile?.Id,
                ResumeDocumentId: resumeDocumentId,
                CandidateName: candidateProfile?.CandidateName ?? "Unknown",
                MatchScore: Math.Round(hit.Score * 100, 2),
                SkillMatchScore: null,
                ExperienceMatchScore: null,
                Strengths: null,
                MissingSkills: null,
                Recommendation: null,
                ResumeSummary: candidateProfile?.ResumeSummary,
                Rank: rank++));
        }

        return new ScreeningOutcomeDto(screeningRequest.Id, candidates);
    }

    public async Task<ScreeningOutcomeDto> RankAsync(ScreeningSearchRequest request, int requestedByUserId, CancellationToken cancellationToken = default)
    {
        var screeningRequest = await CreateScreeningRequestAsync(request, requestedByUserId, cancellationToken);

        var queryVector = await _openAiService.GenerateEmbeddingAsync(BuildQueryText(request), cancellationToken);
        var hits = await _vectorSearchService.SearchAsync(queryVector, RankShortlistSize, cancellationToken);

        var scored = new List<(CandidateProfile? Profile, int ResumeDocumentId, CandidateAnalysisResult Analysis)>();

        foreach (var hit in hits)
        {
            if (!int.TryParse(hit.ResumeDocumentId, out var resumeDocumentId))
            {
                continue;
            }

            var candidateProfile = (await _unitOfWork.CandidateProfiles
                .FindAsync(c => c.ResumeDocumentId == resumeDocumentId, cancellationToken))
                .FirstOrDefault();

            var analysis = await _openAiService.AnalyzeCandidateAsync(
                request.JobDescription,
                request.RequiredSkills,
                hit.Content,
                cancellationToken);

            scored.Add((candidateProfile, resumeDocumentId, analysis));
        }

        var ranked = scored
            .OrderByDescending(s => s.Analysis.MatchPercentage)
            .ToList();

        var candidates = new List<CandidateMatchDto>();
        var rank = 1;
        foreach (var (profile, resumeDocumentId, analysis) in ranked)
        {
            var screeningResult = new ScreeningResult
            {
                ScreeningRequestId = screeningRequest.Id,
                CandidateProfileId = profile?.Id ?? 0,
                MatchPercentage = analysis.MatchPercentage,
                SkillMatchScore = analysis.SkillMatchScore,
                ExperienceMatchScore = analysis.ExperienceMatchScore,
                Strengths = string.Join(", ", analysis.Strengths),
                MissingSkills = string.Join(", ", analysis.MissingSkills),
                AiRecommendation = analysis.Recommendation,
                Rank = rank
            };

            if (profile is not null)
            {
                await _unitOfWork.ScreeningResults.AddAsync(screeningResult, cancellationToken);
            }

            candidates.Add(new CandidateMatchDto(
                CandidateProfileId: profile?.Id,
                ResumeDocumentId: resumeDocumentId,
                CandidateName: profile?.CandidateName ?? "Unknown",
                MatchScore: analysis.MatchPercentage,
                SkillMatchScore: analysis.SkillMatchScore,
                ExperienceMatchScore: analysis.ExperienceMatchScore,
                Strengths: analysis.Strengths,
                MissingSkills: analysis.MissingSkills,
                Recommendation: analysis.Recommendation,
                ResumeSummary: analysis.ResumeSummary,
                Rank: rank));

            rank++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ScreeningOutcomeDto(screeningRequest.Id, candidates);
    }

    private async Task<ScreeningRequest> CreateScreeningRequestAsync(ScreeningSearchRequest request, int requestedByUserId, CancellationToken cancellationToken)
    {
        var screeningRequest = new ScreeningRequest
        {
            RequestedByUserId = requestedByUserId,
            JobTitle = request.JobTitle,
            JobDescription = request.JobDescription,
            RequiredSkills = request.RequiredSkills,
            MinimumExperienceYears = request.MinimumExperienceYears,
            PreferredExperienceYears = request.PreferredExperienceYears,
            Location = request.Location
        };

        await _unitOfWork.ScreeningRequests.AddAsync(screeningRequest, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return screeningRequest;
    }

    private static string BuildQueryText(ScreeningSearchRequest request) =>
        $"Job Title: {request.JobTitle}\nJob Description: {request.JobDescription}\nRequired Skills: {request.RequiredSkills}";
}
