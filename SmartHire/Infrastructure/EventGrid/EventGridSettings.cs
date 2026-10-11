namespace SmartHire.Infrastructure.EventGrid;

/// <summary>
/// Strongly-typed configuration options for Azure Event Grid topic publishing.
/// </summary>
public class EventGridSettings
{
    public const string SectionName = "EventGrid";

    /// <summary>
    /// The Azure Event Grid custom topic endpoint URL, e.g. https://<topic-name>.<region>-1.eventgrid.azure.net/api/events
    /// </summary>
    public string TopicEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The access key / shared secret for the custom topic. Stored in User Secrets or Azure Key Vault.
    /// </summary>
    public string TopicKey { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of delivery retry attempts upon transient HTTP failure.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before exponential backoff retry.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;
}

