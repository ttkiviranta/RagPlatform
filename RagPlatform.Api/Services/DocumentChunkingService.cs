using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentChunkingService : IDocumentChunkingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly ILogger<DocumentChunkingService> _logger;

    public DocumentChunkingService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        ILogger<DocumentChunkingService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocumentChunk>> GenerateChunksAsync(
        Guid documentId,
        int chunkSize,
        int chunkOverlap,
        CancellationToken cancellationToken = default)
    {
        if (chunkSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be greater than zero.");
        }

        if (chunkOverlap < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkOverlap), "Chunk overlap cannot be negative.");
        }

        if (chunkOverlap >= chunkSize)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkOverlap), "Chunk overlap must be smaller than chunk size.");
        }

        var document = await _documentAccessService.GetAccessibleDocumentAsync(
            documentId,
            DocumentPermission.Write,
            cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException($"Document {documentId} was not found.");
        }

        if (string.IsNullOrWhiteSpace(document.Content))
        {
            throw new InvalidOperationException("Document content is empty and cannot be chunked.");
        }

        var existingChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync(cancellationToken);

        if (existingChunks.Count > 0)
        {
            _logger.LogInformation(
                "Document {DocumentId} already has {ChunkCount} chunks. Skipping duplicate generation.",
                documentId,
                existingChunks.Count);

            return existingChunks;
        }

        var generatedChunks = CreateChunks(document.Content, chunkSize, chunkOverlap);
        if (generatedChunks.Count == 0)
        {
            return Array.Empty<DocumentChunk>();
        }

        var chunks = generatedChunks
            .Select((content, index) => new DocumentChunk
            {
                DocumentId = documentId,
                ChunkIndex = index,
                Content = content,
                CharacterCount = content.Length,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        _dbContext.DocumentChunks.AddRange(chunks);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Generated {ChunkCount} chunks for document {DocumentId} with chunk size {ChunkSize} and overlap {ChunkOverlap}.",
            chunks.Count,
            documentId,
            chunkSize,
            chunkOverlap);

        return chunks;
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        if (await _documentAccessService.GetAccessibleDocumentAsync(
                documentId,
                DocumentPermission.Read,
                cancellationToken) is null)
        {
            return Array.Empty<DocumentChunk>();
        }

        return await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == documentId)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentChunk?> GetByIdAsync(Guid chunkId, CancellationToken cancellationToken = default)
    {
        var documentId = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.Id == chunkId)
            .Select(chunk => (Guid?)chunk.DocumentId)
            .FirstOrDefaultAsync(cancellationToken);
        if (documentId is null)
        {
            return null;
        }

        var document = await _documentAccessService.GetAccessibleDocumentAsync(
            documentId.Value,
            DocumentPermission.Read,
            cancellationToken);
        if (document is null)
        {
            return null;
        }

        return await _dbContext.DocumentChunks
            .AsNoTracking()
            .FirstOrDefaultAsync(chunk => chunk.Id == chunkId, cancellationToken);
    }

    private static IReadOnlyList<string> CreateChunks(string content, int chunkSize, int chunkOverlap)
    {
        var normalizedContent = NormalizeContent(content);
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return Array.Empty<string>();
        }

        var sentences = SplitIntoSentenceSegments(normalizedContent);
        if (sentences.Count == 0)
        {
            return CreateWordChunks(normalizedContent, chunkSize, chunkOverlap);
        }

        var chunks = new List<string>();
        var current = new StringBuilder();
        var overlapWords = new Queue<string>();

        foreach (var sentence in sentences)
        {
            var trimmedSentence = sentence.Trim();
            if (string.IsNullOrWhiteSpace(trimmedSentence))
            {
                continue;
            }

            var candidate = string.IsNullOrEmpty(current.ToString())
                ? trimmedSentence
                : $" {trimmedSentence}";

            if (current.Length + candidate.Length <= chunkSize)
            {
                current.Append(candidate);
                continue;
            }

            if (current.Length > 0)
            {
                var chunkText = current.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(chunkText);
                    overlapWords = new Queue<string>(chunkText.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(Math.Min(chunkOverlap, chunkSize)));
                }

                current.Clear();
            }

            if (trimmedSentence.Length <= chunkSize)
            {
                if (overlapWords.Count > 0)
                {
                    var overlapText = string.Join(' ', overlapWords);
                    var combined = string.IsNullOrWhiteSpace(overlapText)
                        ? trimmedSentence
                        : $"{overlapText} {trimmedSentence}";

                    current.Append(combined.Length <= chunkSize ? combined : trimmedSentence);
                }
                else
                {
                    current.Append(trimmedSentence);
                }

                continue;
            }

            var fallbackChunks = CreateWordChunks(trimmedSentence, chunkSize, chunkOverlap);
            for (var i = 0; i < fallbackChunks.Count; i++)
            {
                chunks.Add(fallbackChunks[i]);
            }
        }

        if (current.Length > 0)
        {
            var finalChunk = current.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        return chunks.Select(chunk => chunk.Trim())
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
            .ToList();
    }

    private static IReadOnlyList<string> CreateWordChunks(string content, int chunkSize, int chunkOverlap)
    {
        var words = Regex.Matches(content, @"\S+")
            .Select(match => match.Value)
            .ToList();

        if (words.Count == 0)
        {
            return Array.Empty<string>();
        }

        var chunks = new List<string>();
        var index = 0;

        while (index < words.Count)
        {
            var windowSize = Math.Min(chunkSize, words.Count - index);
            var chunkWords = words.Skip(index).Take(windowSize).ToList();
            if (chunkWords.Count == 0)
            {
                break;
            }

            chunks.Add(string.Join(' ', chunkWords));

            if (index + windowSize >= words.Count)
            {
                break;
            }

            var step = Math.Max(1, chunkSize - chunkOverlap);
            index += step;
        }

        return chunks;
    }

    private static List<string> SplitIntoSentenceSegments(string content)
    {
        var segments = Regex.Split(content, @"(?<=[.!?])\s+")
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .Select(segment => segment.Trim())
            .ToList();

        return segments.Count > 0 ? segments : new List<string> { content.Trim() };
    }

    private static string NormalizeContent(string content)
    {
        var normalized = Regex.Replace(content, @"\r\n?", "\n");
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
        normalized = Regex.Replace(normalized, @"[ \t]*\n[ \t]*", "\n");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s+([,.!?;:])", "$1");
        return normalized.Trim();
    }
}
