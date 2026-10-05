namespace SmartHire.Infrastructure.TextExtraction;

/// <summary>
/// Extracts plain text from resume files (PDF/DOCX) for chunking and embedding.
/// </summary>
public interface IResumeTextExtractor
{
    /// <summary>Returns true if the extractor supports the given file extension (".pdf" or ".docx").</summary>
    bool CanExtract(string fileExtension);

    Task<string> ExtractTextAsync(Stream fileStream, string fileExtension, CancellationToken cancellationToken = default);
}
