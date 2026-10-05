using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using SmartHire.Infrastructure.Options;

namespace SmartHire.Infrastructure.OpenAi;

/// <summary>
/// Azure OpenAI backed implementation for embeddings and GPT candidate analysis.
/// </summary>
public class AzureOpenAiService : IOpenAiService
{
    private readonly AzureOpenAiOptions _options;
    private readonly AzureOpenAIClient _client;
    private readonly ILogger<AzureOpenAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AzureOpenAiService(IOptions<AzureOpenAiOptions> options, ILogger<AzureOpenAiService> logger)
    {
        _options = options.Value;
        _logger = logger;
        var endpoint = new Uri(_options.Endpoint);

        _client = _options.UseManagedIdentity
            ? new AzureOpenAIClient(endpoint, new DefaultAzureCredential())
            : new AzureOpenAIClient(endpoint, new AzureKeyCredential(_options.ApiKey ?? string.Empty));
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var embeddingClient = _client.GetEmbeddingClient(_options.EmbeddingDeploymentName);

        var result = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);

        return result.Value.ToFloats().ToArray();
    }

    public async Task<CandidateAnalysisResult> AnalyzeCandidateAsync(
        string jobDescription,
        string requiredSkills,
        string resumeContent,
        CancellationToken cancellationToken = default)
    {
        var chatClient = _client.GetChatClient(_options.ChatDeploymentName);

        var systemPrompt =
            "You are an expert technical recruiter assistant. Compare the candidate resume against the " +
            "job description and required skills, then respond with ONLY a single JSON object (no markdown, " +
            "no extra text) matching exactly this shape: " +
            "{\"matchPercentage\": number (0-100), \"skillMatchScore\": number (0-100), " +
            "\"experienceMatchScore\": number (0-100), \"strengths\": string[], \"missingSkills\": string[], " +
            "\"recommendation\": string, \"resumeSummary\": string}.";

        var userPrompt =
            $"Job Description:\n{jobDescription}\n\n" +
            $"Required Skills:\n{requiredSkills}\n\n" +
            $"Candidate Resume:\n{resumeContent}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var chatOptions = new ChatCompletionOptions
        {
            Temperature = 0.2f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var completion = await chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
        var rawJson = completion.Value.Content[0].Text;

        try
        {
            var parsed = JsonSerializer.Deserialize<CandidateAnalysisJson>(rawJson, JsonOptions)
                ?? throw new InvalidOperationException("Empty analysis response from Azure OpenAI.");

            return new CandidateAnalysisResult(
                parsed.MatchPercentage,
                parsed.SkillMatchScore,
                parsed.ExperienceMatchScore,
                parsed.Strengths ?? [],
                parsed.MissingSkills ?? [],
                parsed.Recommendation ?? string.Empty,
                parsed.ResumeSummary ?? string.Empty);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Azure OpenAI candidate analysis response: {RawJson}", rawJson);
            throw new InvalidOperationException("Azure OpenAI returned an unparseable candidate analysis response.", ex);
        }
    }

    private sealed class CandidateAnalysisJson
    {
        [JsonPropertyName("matchPercentage")]
        public double MatchPercentage { get; set; }

        [JsonPropertyName("skillMatchScore")]
        public double SkillMatchScore { get; set; }

        [JsonPropertyName("experienceMatchScore")]
        public double ExperienceMatchScore { get; set; }

        [JsonPropertyName("strengths")]
        public List<string>? Strengths { get; set; }

        [JsonPropertyName("missingSkills")]
        public List<string>? MissingSkills { get; set; }

        [JsonPropertyName("recommendation")]
        public string? Recommendation { get; set; }

        [JsonPropertyName("resumeSummary")]
        public string? ResumeSummary { get; set; }
    }
}
