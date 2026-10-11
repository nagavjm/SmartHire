using System.Text.Json.Serialization;

namespace SmartHire.Infrastructure.EventGrid;

/// <summary>
/// Supported candidate lifecycle event types published to Azure Event Grid.
/// </summary>
public static class CandidateLifecycleEventTypes
{
    public const string CandidateShortlisted = "CandidateShortlisted";
    public const string VerificationCompleted = "VerificationCompleted";
    public const string VerificationFailed = "VerificationFailed";
    public const string OfferReleased = "OfferReleased";
}

/// <summary>
/// Status values corresponding to each candidate lifecycle event.
/// </summary>
public static class CandidateLifecycleStatuses
{
    public const string Shortlisted = "Shortlisted";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string OfferReleased = "OfferReleased";
}

/// <summary>
/// Standard schema payload published to Azure Event Grid for candidate actions.
/// </summary>
public class CandidateLifecycleEventPayload
{
    [JsonPropertyName("candidateId")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("candidateName")]
    public string CandidateName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("jobId")]
    public string JobId { get; set; } = string.Empty;

    [JsonPropertyName("eventDate")]
    public DateTime EventDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

