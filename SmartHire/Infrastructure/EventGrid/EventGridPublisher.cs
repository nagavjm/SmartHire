using System.Text.Json;
using Azure;
using Azure.Messaging.EventGrid;
using Microsoft.Extensions.Options;

namespace SmartHire.Infrastructure.EventGrid;

/// <summary>
/// Production-ready Azure Event Grid event publisher with built-in retry,
/// exponential backoff, and structured logging.
/// </summary>
public class EventGridPublisher : IEventGridPublisher
{
    private readonly EventGridSettings _settings;
    private readonly ILogger<EventGridPublisher> _logger;
    private readonly EventGridPublisherClient? _client;

    public EventGridPublisher(
        IOptions<EventGridSettings> options,
        ILogger<EventGridPublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.TopicEndpoint) && !string.IsNullOrWhiteSpace(_settings.TopicKey))
        {
            try
            {
                var endpoint = new Uri(_settings.TopicEndpoint);
                var credential = new AzureKeyCredential(_settings.TopicKey);
                _client = new EventGridPublisherClient(endpoint, credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize EventGridPublisherClient with endpoint {Endpoint}", _settings.TopicEndpoint);
            }
        }
        else
        {
            _logger.LogWarning("EventGrid is not configured. TopicEndpoint or TopicKey is missing. Event publication will be logged and skipped.");
        }
    }

    /// <inheritdoc />
    public Task<bool> PublishCandidateLifecycleEventAsync(
        CandidateLifecycleEventPayload payload,
        CancellationToken cancellationToken = default)
    {
        var subject = $"smarthire/candidate/{payload.CandidateId}";
        return PublishEventAsync(payload.EventType, subject, payload, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> PublishEventAsync<T>(
        string eventType,
        string subject,
        T data,
        CancellationToken cancellationToken = default)
    {
        var jsonPayload = JsonSerializer.Serialize(data);

        if (_client is null)
        {
            _logger.LogWarning(
                "[SIMULATION] Azure Event Grid client not configured. Skipping remote publication. EventType: {EventType}, Subject: {Subject}, Payload: {Payload}",
                eventType, subject, jsonPayload);
            return true;
        }

        var eventGridEvent = new EventGridEvent(
            subject: subject,
            eventType: eventType,
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(data));

        var maxRetries = Math.Max(1, _settings.MaxRetries);
        var baseDelayMs = Math.Max(100, _settings.RetryDelayMilliseconds);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Publishing Event Grid event {EventType} (Attempt {Attempt}/{MaxRetries}) to {Subject} with Payload: {Payload}",
                    eventType, attempt, maxRetries, subject, jsonPayload);

                var response = await _client.SendEventAsync(eventGridEvent, cancellationToken);

                if (response.Status >= 200 && response.Status < 300)
                {
                    _logger.LogInformation(
                        "Event Grid event {EventType} published successfully. StatusCode: {StatusCode}, Subject: {Subject}, Payload: {Payload}",
                        eventType, response.Status, subject, jsonPayload);
                    return true;
                }

                _logger.LogWarning(
                    "Event Grid send returned non-success status code {StatusCode} on attempt {Attempt}/{MaxRetries} for {Subject}",
                    response.Status, attempt, maxRetries, subject);
            }
            catch (Exception ex) when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                _logger.LogWarning(ex,
                    "Failed to publish Event Grid event {EventType} on attempt {Attempt}/{MaxRetries}. Retrying in {DelayMs}ms... Subject: {Subject}, Payload: {Payload}",
                    eventType, attempt, maxRetries, delay.TotalMilliseconds, subject, jsonPayload);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Event Grid publication permanently failed for EventType: {EventType}, Subject: {Subject}, Payload: {Payload} after {Attempt} attempts.",
                    eventType, subject, jsonPayload, attempt);
                return false;
            }
        }

        return false;
    }
}

