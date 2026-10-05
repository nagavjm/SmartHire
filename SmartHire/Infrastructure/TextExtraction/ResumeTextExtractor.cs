using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;

namespace SmartHire.Infrastructure.TextExtraction;

/// <summary>
/// Extracts plain text from PDF (via PdfPig) and DOCX (via DocumentFormat.OpenXml) resume files.
/// </summary>
public class ResumeTextExtractor : IResumeTextExtractor
{
    private static readonly string[] SupportedExtensions = { ".pdf", ".docx" };

    public bool CanExtract(string fileExtension) =>
        SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(Stream fileStream, string fileExtension, CancellationToken cancellationToken = default)
    {
        return fileExtension.ToLowerInvariant() switch
        {
            ".pdf" => Task.FromResult(ExtractPdfText(fileStream)),
            ".docx" => Task.FromResult(ExtractDocxText(fileStream)),
            _ => throw new NotSupportedException($"Unsupported resume file extension '{fileExtension}'.")
        };
    }

    private static string ExtractPdfText(Stream fileStream)
    {
        using var document = PdfDocument.Open(fileStream);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string ExtractDocxText(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, isEditable: false);
        var body = document.MainDocumentPart?.Document?.Body;
        return body?.InnerText ?? string.Empty;
    }
}
