using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using SmartHire.Infrastructure.Options;

namespace SmartHire.Infrastructure.Search;

/// <summary>
/// Azure AI Search backed vector search implementation.
/// </summary>
public class AzureAiSearchService : IVectorSearchService
{
    private const string VectorSearchProfileName = "resume-vector-profile";
    private const string VectorSearchAlgorithmName = "resume-hnsw-algorithm";
    private const string VectorFieldName = "ContentVector";

    private readonly AzureAiSearchOptions _options;
    private readonly SearchIndexClient _indexClient;
    private readonly SearchClient _searchClient;

    public AzureAiSearchService(IOptions<AzureAiSearchOptions> options)
    {
        _options = options.Value;

        var endpoint = new Uri(_options.Endpoint);

        if (_options.UseManagedIdentity)
        {
            var credential = new DefaultAzureCredential();
            _indexClient = new SearchIndexClient(endpoint, credential);
            _searchClient = new SearchClient(endpoint, _options.IndexName, credential);
        }
        else
        {
            var credential = new AzureKeyCredential(_options.ApiKey ?? string.Empty);
            _indexClient = new SearchIndexClient(endpoint, credential);
            _searchClient = new SearchClient(endpoint, _options.IndexName, credential);
        }
    }

    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _indexClient.GetIndexAsync(_options.IndexName, cancellationToken);
            return;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Index does not exist yet - create it below.
        }

        var index = new SearchIndex(_options.IndexName)
        {
            Fields =
            {
                new SimpleField("Id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SimpleField("ResumeDocumentId", SearchFieldDataType.String) { IsFilterable = true },
                new SearchableField("CandidateName") { IsFilterable = true },
                new SearchableField("Content"),
                new SimpleField("BlobUrl", SearchFieldDataType.String),
                new VectorSearchField(VectorFieldName, _options.VectorDimensions, VectorSearchProfileName)
            },
            VectorSearch = new VectorSearch
            {
                Algorithms = { new HnswAlgorithmConfiguration(VectorSearchAlgorithmName) },
                Profiles =
                {
                    new VectorSearchProfile(VectorSearchProfileName, VectorSearchAlgorithmName)
                }
            }
        };

        await _indexClient.CreateOrUpdateIndexAsync(index, cancellationToken: cancellationToken);
    }

    public async Task UpsertDocumentsAsync(IEnumerable<ResumeSearchDocument> documents, CancellationToken cancellationToken = default)
    {
        var searchDocuments = documents
            .Select(doc => new SearchDocument
            {
                ["Id"] = doc.Id,
                ["ResumeDocumentId"] = doc.ResumeDocumentId,
                ["CandidateName"] = doc.CandidateName,
                ["Content"] = doc.Content,
                ["BlobUrl"] = doc.BlobUrl,
                [VectorFieldName] = doc.ContentVector
            })
            .ToList();

        if (searchDocuments.Count == 0)
        {
            return;
        }

        var batch = IndexDocumentsBatch.MergeOrUpload(searchDocuments);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(float[] queryVector, int topK = 10, CancellationToken cancellationToken = default)
    {
        var searchOptions = new SearchOptions
        {
            VectorSearch = new VectorSearchOptions
            {
                Queries = { new VectorizedQuery(queryVector) { KNearestNeighborsCount = topK, Fields = { VectorFieldName } } }
            },
            Size = topK,
            Select = { "ResumeDocumentId", "Content", "BlobUrl" }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(searchText: null, searchOptions, cancellationToken);

        var hits = new List<VectorSearchHit>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var doc = result.Document;
            hits.Add(new VectorSearchHit(
                ResumeDocumentId: doc.GetString("ResumeDocumentId") ?? string.Empty,
                Content: doc.GetString("Content") ?? string.Empty,
                BlobUrl: doc.GetString("BlobUrl") ?? string.Empty,
                Score: result.Score ?? 0d));
        }

        return hits;
    }
}
