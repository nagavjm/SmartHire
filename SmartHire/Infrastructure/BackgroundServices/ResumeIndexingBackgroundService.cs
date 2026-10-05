using SmartHire.Entities;
using SmartHire.Infrastructure.BlobStorage;
using SmartHire.Infrastructure.OpenAi;
using SmartHire.Infrastructure.Search;
using SmartHire.Infrastructure.TextExtraction;
using SmartHire.Repositories;
using SmartHire.Services;

namespace SmartHire.Infrastructure.BackgroundServices;

/// <summary>
/// Periodically picks up resumes tracked as <see cref="IndexingStatus.Pending"/> (discovered by
/// <see cref="IResumeSyncService"/>), extracts text (PDF/DOCX), chunks it, generates embeddings
/// via Azure OpenAI, and upserts vectors into Azure AI Search.
/// </summary>
public class ResumeIndexingBackgroundService : BackgroundService
{
    private readonly ILogger<ResumeIndexingBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(15);

    public ResumeIndexingBackgroundService(
        ILogger<ResumeIndexingBackgroundService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIndexingCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resume indexing cycle failed");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task RunIndexingCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();
        var blobStorageService = provider.GetRequiredService<IBlobStorageService>();
        var textExtractor = provider.GetRequiredService<IResumeTextExtractor>();
        var openAiService = provider.GetRequiredService<IOpenAiService>();
        var vectorSearchService = provider.GetRequiredService<IVectorSearchService>();

        var pendingDocuments = await unitOfWork.ResumeDocuments.FindAsync(
            d => d.Status == IndexingStatus.Pending, cancellationToken);

        if (pendingDocuments.Count == 0)
        {
            _logger.LogInformation("No pending resumes to index");
            return;
        }

        _logger.LogInformation("Starting resume indexing cycle for {Count} pending resumes", pendingDocuments.Count);
        await vectorSearchService.EnsureIndexExistsAsync(cancellationToken);

        var indexed = 0;
        var failed = 0;

        foreach (var document in pendingDocuments)
        {
            try
            {
                await IndexDocumentAsync(document, unitOfWork, blobStorageService, textExtractor, openAiService, vectorSearchService, cancellationToken);
                indexed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index resume {BlobName}", document.BlobName);
                document.Status = IndexingStatus.Failed;
                document.FailureReason = ex.Message;
                unitOfWork.ResumeDocuments.Update(document);
                failed++;
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Resume indexing cycle complete: {Indexed} indexed, {Failed} failed", indexed, failed);
    }

    private static async Task IndexDocumentAsync(
        ResumeDocument document,
        IUnitOfWork unitOfWork,
        IBlobStorageService blobStorageService,
        IResumeTextExtractor textExtractor,
        IOpenAiService openAiService,
        IVectorSearchService vectorSearchService,
        CancellationToken cancellationToken)
    {
        if (!textExtractor.CanExtract(document.FileExtension))
        {
            throw new NotSupportedException($"No text extractor registered for '{document.FileExtension}'.");
        }

        await using var blobStream = await blobStorageService.DownloadResumeAsync(document.BlobName, cancellationToken);
        var text = await textExtractor.ExtractTextAsync(blobStream, document.FileExtension, cancellationToken);
        var chunks = TextChunker.Chunk(text);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("No extractable text found in resume.");
        }

        var candidateName = InferCandidateName(text, document.BlobName);
        var searchDocuments = new List<ResumeSearchDocument>();

        var existingChunks = await unitOfWork.ResumeChunks.FindAsync(c => c.ResumeDocumentId == document.Id, cancellationToken);
        foreach (var existingChunk in existingChunks)
        {
            unitOfWork.ResumeChunks.Remove(existingChunk);
        }

        for (var i = 0; i < chunks.Count; i++)
        {
            var chunkContent = chunks[i];
            var embeddingVector = await openAiService.GenerateEmbeddingAsync(chunkContent, cancellationToken);

            var chunk = new ResumeChunk
            {
                ResumeDocumentId = document.Id,
                ChunkIndex = i,
                Content = chunkContent,
                TokenCount = TextChunker.EstimateTokenCount(chunkContent)
            };
            await unitOfWork.ResumeChunks.AddAsync(chunk, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken); // assign chunk.Id

            var searchDocId = $"{document.Id}-{chunk.Id}";
            var embedding = new ResumeEmbedding
            {
                ResumeChunkId = chunk.Id,
                SearchIndexDocumentId = searchDocId,
                EmbeddingModel = "text-embedding-3-large",
                VectorDimensions = embeddingVector.Length
            };
            await unitOfWork.ResumeEmbeddings.AddAsync(embedding, cancellationToken);

            searchDocuments.Add(new ResumeSearchDocument(
                Id: searchDocId,
                ResumeDocumentId: document.Id.ToString(),
                CandidateName: candidateName,
                Content: chunkContent,
                ContentVector: embeddingVector,
                BlobUrl: document.BlobUrl));
        }

        await vectorSearchService.UpsertDocumentsAsync(searchDocuments, cancellationToken);

        var existingProfile = (await unitOfWork.CandidateProfiles.FindAsync(c => c.ResumeDocumentId == document.Id, cancellationToken))
            .FirstOrDefault();
        if (existingProfile is null)
        {
            await unitOfWork.CandidateProfiles.AddAsync(new CandidateProfile
            {
                ResumeDocumentId = document.Id,
                CandidateName = candidateName
            }, cancellationToken);
        }

        document.Status = IndexingStatus.Indexed;
        document.LastIndexedAtUtc = DateTime.UtcNow;
        document.FailureReason = null;
        unitOfWork.ResumeDocuments.Update(document);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Best-effort candidate name inference: the first non-empty line of the resume text,
    /// falling back to the blob file name. Full NER-based extraction is handled by GPT
    /// analysis later during screening, not here.
    /// </summary>
    private static string InferCandidateName(string text, string blobName)
    {
        var firstLine = text
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.Length is > 0 and < 80);

        return !string.IsNullOrWhiteSpace(firstLine) ? firstLine : Path.GetFileNameWithoutExtension(blobName);
    }
}
