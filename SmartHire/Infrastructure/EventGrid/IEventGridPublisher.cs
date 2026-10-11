namespace SmartHire.Infrastructure.EventGrid;

/// <summary>
/// Service abstraction for publishing domain events to Azure Event Grid.
/// </summary>
public interface IEventGridPublisher
{
    /// <summary>
    /// Publishes a strongly-typed candidate lifecycle event to Azure Event Grid.
    /// </summary>
    /// <param name="payload">The candidate lifecycle event data payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if publication succeeded; otherwise false.</returns>
    Task<bool> PublishCandidateLifecycleEventAsync(CandidateLifecycleEventPayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic publish method for any custom event schema to Azure Event Grid.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    /// <param name="eventType">Event type name.</param>
    /// <param name="subject">Event subject string (e.g. smarthire/candidate/{id}).</param>
    /// <param name="data">Payload object.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if publication succeeded; otherwise false.</returns>
    Task<bool> PublishEventAsync<T>(string eventType, string subject, T data, CancellationToken cancellationToken = default);
}

