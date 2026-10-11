using SmartHire.Infrastructure.EventGrid;

namespace SmartHire.Services;

public class CandidateLifecycleActionRequest
{
    public string Action { get; set; } = string.Empty;
    public string? JobId { get; set; }
}

public record CandidateLifecycleActionResult(
    bool Success,
    string EventType,
    string Status,
    CandidateLifecycleEventPayload? Payload,
    string Message);

/// <summary>
/// Service coordinating candidate lifecycle transitions and publishing events to Azure Event Grid.
/// </summary>
public interface ICandidateLifecycleService
{
    Task<CandidateLifecycleActionResult> ExecuteActionAsync(
        int candidateId,
        string action,
        string? jobId = null,
        CancellationToken cancellationToken = default);
}

