namespace SmartHire.Infrastructure.TextExtraction;

/// <summary>
/// Splits extracted resume text into fixed-size, slightly overlapping chunks suitable for
/// embedding generation. Uses a simple character-based approximation of token count
/// (~4 characters per token) rather than a real tokenizer.
/// </summary>
public static class TextChunker
{
    private const int ChunkSizeChars = 3000; // ~750 tokens
    private const int OverlapChars = 300;

    public static IReadOnlyList<string> Chunk(string text)
    {
        var normalized = text.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return Array.Empty<string>();
        }

        if (normalized.Length <= ChunkSizeChars)
        {
            return new[] { normalized };
        }

        var chunks = new List<string>();
        var start = 0;
        while (start < normalized.Length)
        {
            var length = Math.Min(ChunkSizeChars, normalized.Length - start);
            chunks.Add(normalized.Substring(start, length));

            if (start + length >= normalized.Length)
            {
                break;
            }

            start += ChunkSizeChars - OverlapChars;
        }

        return chunks;
    }

    public static int EstimateTokenCount(string text) => Math.Max(1, text.Length / 4);
}
