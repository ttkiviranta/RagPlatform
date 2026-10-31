using System.Text.RegularExpressions;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class PlainTextProcessor : IPlainTextProcessor
{
    private readonly ILogger<PlainTextProcessor> _logger;

    public PlainTextProcessor(ILogger<PlainTextProcessor> logger)
    {
        _logger = logger;
    }

    public string DocumentType => "text/plain";

    public Task<DocumentMetadata> ProcessAsync(Document document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var normalizedContent = NormalizeContent(document.Content);

        var metadata = new DocumentMetadata
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            ContentType = string.IsNullOrWhiteSpace(document.ContentType) ? DocumentType : document.ContentType,
            CharacterCount = normalizedContent.Length,
            WordCount = CountWords(normalizedContent),
            LineCount = CountLines(normalizedContent),
            ProcessedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Processed plain text document {DocumentId} with {CharacterCount} characters, {WordCount} words, and {LineCount} lines.",
            document.Id,
            metadata.CharacterCount,
            metadata.WordCount,
            metadata.LineCount);

        return Task.FromResult(metadata);
    }

    public static string NormalizeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(content, @"\r\n?", "\n");
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        normalized = Regex.Replace(normalized, @"[ \t]*\n[ \t]*", "\n");

        return normalized.Trim();
    }

    public static int CountWords(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return Regex.Matches(content, @"\b\w+\b").Count;
    }

    public static int CountLines(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return 0;
        }

        return content.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
