namespace SmartHire.Infrastructure.Options;

/// <summary>
/// Configuration for the existing Azure Blob Storage account that already contains resumes.
/// </summary>
public class AzureBlobStorageOptions
{
    public const string SectionName = "AzureBlobStorage";

    public string? ConnectionString { get; set; }
    public string StorageAccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = string.Empty;
    public bool UseManagedIdentity { get; set; } = false;
}

/// <summary>
/// Configuration for Azure AI Search vector index used for RAG retrieval.
/// </summary>
public class AzureAiSearchOptions
{
    public const string SectionName = "AzureAiSearch";

    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string IndexName { get; set; } = "resume-index";
    public bool UseManagedIdentity { get; set; } = false;

    /// <summary>
    /// Must match the embedding dimensions produced by AzureOpenAiOptions.EmbeddingDeploymentName
    /// (e.g. text-embedding-3-large = 3072, text-embedding-3-small = 1536).
    /// </summary>
    public int VectorDimensions { get; set; } = 3072;
}

/// <summary>
/// Configuration for Azure OpenAI used for embeddings and GPT-based analysis.
/// </summary>
public class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public string EmbeddingDeploymentName { get; set; } = "text-embedding-3-large";
    public string ChatDeploymentName { get; set; } = "gpt-4o";
    public bool UseManagedIdentity { get; set; } = false;
}
