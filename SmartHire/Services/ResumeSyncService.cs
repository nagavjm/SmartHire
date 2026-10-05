using SmartHire.Entities;
using SmartHire.Infrastructure.BlobStorage;
using SmartHire.Repositories;

namespace SmartHire.Services;

/// <summary>
/// Discovers resumes in Blob Storage and tracks them as <see cref="ResumeDocument"/> rows.
/// Text extraction/embedding is handled separately by <c>ResumeIndexingBackgroundService</c>
/// once PDF/DOCX parsing is implemented; this service only keeps the document inventory and
/// job history up to date.
/// </summary>
public class ResumeSyncService : IResumeSyncService
{
    private const string DefaultContainerName = "resumes";

    private readonly IBlobStorageService _blobStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public ResumeSyncService(IBlobStorageService blobStorageService, IUnitOfWork unitOfWork)
    {
        _blobStorageService = blobStorageService;
        _unitOfWork = unitOfWork;
    }

    public Task<IndexingJobDto> StartAsync(CancellationToken cancellationToken = default) =>
        RunSyncAsync(forceReindex: false, cancellationToken);

    public Task<IndexingJobDto> ReindexAsync(CancellationToken cancellationToken = default) =>
        RunSyncAsync(forceReindex: true, cancellationToken);

    public async Task<IndexingJobDto?> GetLatestStatusAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _unitOfWork.IndexingJobs.GetAllAsync(cancellationToken);
        var latest = jobs.OrderByDescending(j => j.StartedAtUtc).FirstOrDefault();
        return latest is null ? null : ToDto(latest);
    }

    public async Task<IReadOnlyList<IndexingJobDto>> GetJobsAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _unitOfWork.IndexingJobs.GetAllAsync(cancellationToken);
        return jobs
            .OrderByDescending(j => j.StartedAtUtc)
            .Select(ToDto)
            .ToList();
    }

    private async Task<IndexingJobDto> RunSyncAsync(bool forceReindex, CancellationToken cancellationToken)
    {
        var job = new IndexingJob
        {
            Status = IndexingJobStatus.Running,
            StartedAtUtc = DateTime.UtcNow
        };
        await _unitOfWork.IndexingJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var blobResumes = await _blobStorageService.ListResumesAsync(cancellationToken);
            job.DocumentsDiscovered = blobResumes.Count;

            var existingDocuments = (await _unitOfWork.ResumeDocuments.GetAllAsync(cancellationToken))
                .ToDictionary(d => d.BlobName, StringComparer.OrdinalIgnoreCase);

            var tracked = 0;
            var failed = 0;

            foreach (var blobResume in blobResumes)
            {
                try
                {
                    if (existingDocuments.TryGetValue(blobResume.BlobName, out var existing))
                    {
                        var changed = forceReindex || existing.ContentHash != blobResume.ContentHash;
                        if (changed)
                        {
                            existing.ContentHash = blobResume.ContentHash;
                            existing.SizeInBytes = blobResume.SizeInBytes;
                            existing.BlobUrl = blobResume.BlobUrl;
                            existing.BlobLastModifiedUtc = blobResume.LastModifiedUtc.UtcDateTime;
                            existing.Status = IndexingStatus.Pending;
                            existing.FailureReason = null;
                            _unitOfWork.ResumeDocuments.Update(existing);
                            tracked++;
                        }
                    }
                    else
                    {
                        var document = new ResumeDocument
                        {
                            BlobName = blobResume.BlobName,
                            BlobUrl = blobResume.BlobUrl,
                            ContainerName = DefaultContainerName,
                            FileExtension = Path.GetExtension(blobResume.BlobName).ToLowerInvariant(),
                            SizeInBytes = blobResume.SizeInBytes,
                            ContentHash = blobResume.ContentHash,
                            BlobLastModifiedUtc = blobResume.LastModifiedUtc.UtcDateTime,
                            Status = IndexingStatus.Pending
                        };
                        await _unitOfWork.ResumeDocuments.AddAsync(document, cancellationToken);
                        tracked++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            job.DocumentsIndexed = tracked;
            job.DocumentsFailed = failed;
            job.Status = failed > 0 ? IndexingJobStatus.CompletedWithErrors : IndexingJobStatus.Completed;
            job.CompletedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            job.Status = IndexingJobStatus.Failed;
            job.ErrorSummary = ex.Message;
            job.CompletedAtUtc = DateTime.UtcNow;
        }

        _unitOfWork.IndexingJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(job);
    }

    private static IndexingJobDto ToDto(IndexingJob job) => new(
        job.Id,
        job.Status,
        job.DocumentsDiscovered,
        job.DocumentsIndexed,
        job.DocumentsFailed,
        job.StartedAtUtc,
        job.CompletedAtUtc,
        job.ErrorSummary);
}
