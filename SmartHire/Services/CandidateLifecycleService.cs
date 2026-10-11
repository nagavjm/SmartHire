using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartHire.Data;
using SmartHire.Entities;
using SmartHire.Infrastructure.EventGrid;

namespace SmartHire.Services;

/// <summary>
/// Service implementing candidate lifecycle state transitions and publishing
/// corresponding Azure Event Grid events for AuthBridge consumption.
/// </summary>
public class CandidateLifecycleService : ICandidateLifecycleService
{
    private readonly SmartHireDbContext _context;
    private readonly IEventGridPublisher _eventGridPublisher;
    private readonly ILogger<CandidateLifecycleService> _logger;

    public CandidateLifecycleService(
        SmartHireDbContext context,
        IEventGridPublisher eventGridPublisher,
        ILogger<CandidateLifecycleService> logger)
    {
        _context = context;
        _eventGridPublisher = eventGridPublisher;
        _logger = logger;
    }

    public async Task<CandidateLifecycleActionResult> ExecuteActionAsync(
        int candidateId,
        string action,
        string? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _context.CandidateProfiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == candidateId, cancellationToken);
        if (candidate is null)
        {
            _logger.LogWarning("Candidate {CandidateId} not found for lifecycle action {Action}", candidateId, action);
            return new CandidateLifecycleActionResult(false, action, string.Empty, null, $"Candidate with ID {candidateId} was not found.");
        }

        var (isValid, normalizedEventType, status) = MapActionToEventAndStatus(action);
        if (!isValid)
        {
            _logger.LogWarning("Unsupported lifecycle action {Action} requested for Candidate {CandidateId}", action, candidateId);
            return new CandidateLifecycleActionResult(false, action, string.Empty, null, $"Unsupported candidate lifecycle action '{action}'.");
        }

        // Build standard Event Grid payload
        var payload = new CandidateLifecycleEventPayload
        {
            CandidateId = candidate.Id.ToString(),
            CandidateName = candidate.CandidateName,
            Email = candidate.Email ?? string.Empty,
            JobId = !string.IsNullOrWhiteSpace(jobId) ? jobId : "JOB-DEFAULT",
            EventDate = DateTime.UtcNow,
            EventType = normalizedEventType,
            Status = status
        };

        // Publish to Azure Event Grid
        var publishSuccess = await _eventGridPublisher.PublishCandidateLifecycleEventAsync(payload, cancellationToken);

        // Record persistent audit log entry
        try
        {
            _context.AuditLogs.Add(new AuditLog
            {
                EventType = normalizedEventType,
                Description = $"Candidate '{candidate.CandidateName}' (ID {candidate.Id}) transitioned to '{status}'. Event Grid publish: {(publishSuccess ? "Success" : "Failed")}",
                Metadata = JsonSerializer.Serialize(payload),
                CreatedAtUtc = DateTime.UtcNow
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for candidate lifecycle action {Action} on Candidate {CandidateId}", normalizedEventType, candidateId);
        }

        var message = publishSuccess
            ? $"Action '{normalizedEventType}' executed and event published to Azure Event Grid successfully."
            : $"Action '{normalizedEventType}' recorded, but Azure Event Grid publication failed (see server logs).";

        return new CandidateLifecycleActionResult(publishSuccess, normalizedEventType, status, payload, message);
    }

    private static (bool IsValid, string EventType, string Status) MapActionToEventAndStatus(string action)
    {
        return action.Trim().ToLowerInvariant() switch
        {
            "candidateshortlisted" or "shortlist" or "shortlisted" =>
                (true, CandidateLifecycleEventTypes.CandidateShortlisted, CandidateLifecycleStatuses.Shortlisted),

            "verificationcompleted" or "verify_completed" or "completed" =>
                (true, CandidateLifecycleEventTypes.VerificationCompleted, CandidateLifecycleStatuses.Completed),

            "verificationfailed" or "verify_failed" or "failed" =>
                (true, CandidateLifecycleEventTypes.VerificationFailed, CandidateLifecycleStatuses.Failed),

            "offerreleased" or "release_offer" or "offer" =>
                (true, CandidateLifecycleEventTypes.OfferReleased, CandidateLifecycleStatuses.OfferReleased),

            _ => (false, action, action)
        };
    }
}

