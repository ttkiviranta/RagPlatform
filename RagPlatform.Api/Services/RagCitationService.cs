using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Data;

namespace RagPlatform.Api.Services;

public sealed class RagCitationService : IRagCitationService
{
    private const int MaxExcerptCharacters = 500;
    private const int MaxSourceNameCharacters = 200;
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;

    public RagCitationService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
    }

    public async Task<IReadOnlyList<RagCitationDto>> CreateCitationsAsync(
        IReadOnlyList<VectorSearchResult> retrievedChunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retrievedChunks);
        if (retrievedChunks.Count == 0)
        {
            return Array.Empty<RagCitationDto>();
        }

        var currentUser = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
        var chunkIds = retrievedChunks
            .Select(result => result.ChunkId)
            .Distinct()
            .ToArray();
        var accessibleDocuments = _documentAccessService
            .ScopeDocuments(_dbContext.Documents.AsNoTracking(), currentUser);

        var persistedChunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk =>
                chunkIds.Contains(chunk.Id)
                && accessibleDocuments.Any(document => document.Id == chunk.DocumentId))
            .Select(chunk => new PersistedCitationSource(
                chunk.Id,
                chunk.DocumentId,
                chunk.Content,
                chunk.Document.FileName,
                chunk.Document.Title))
            .ToListAsync(cancellationToken);

        var persistedById = persistedChunks.ToDictionary(chunk => chunk.ChunkId);
        var citations = new List<RagCitationDto>(persistedChunks.Count);
        var processedChunkIds = new HashSet<Guid>();
        foreach (var retrievedChunk in retrievedChunks)
        {
            if (!processedChunkIds.Add(retrievedChunk.ChunkId))
            {
                continue;
            }

            if (!persistedById.TryGetValue(retrievedChunk.ChunkId, out var persistedChunk))
            {
                continue;
            }

            if (persistedChunk.DocumentId != retrievedChunk.DocumentId)
            {
                throw new InvalidOperationException(
                    "Retrieved chunk metadata does not match the persisted document relationship.");
            }

            var excerpt = CreateSafeExcerpt(persistedChunk.Content);
            if (excerpt.Length == 0)
            {
                continue;
            }

            var sourceName = CreateSafeSourceName(persistedChunk.FileName, persistedChunk.Title);
            citations.Add(new RagCitationDto
            {
                CitationNumber = citations.Count + 1,
                DocumentId = persistedChunk.DocumentId,
                ChunkId = persistedChunk.ChunkId,
                SourceName = sourceName,
                PageNumber = null,
                Excerpt = excerpt
            });
        }

        return citations;
    }

    private static string CreateSafeExcerpt(string content)
    {
        var normalized = Normalize(content);
        if (normalized.Length > MaxExcerptCharacters)
        {
            const string ellipsis = "...";
            var prefixLength = MaxExcerptCharacters - ellipsis.Length;
            if (char.IsHighSurrogate(normalized[prefixLength - 1])
                && char.IsLowSurrogate(normalized[prefixLength]))
            {
                prefixLength--;
            }

            normalized = normalized[..prefixLength].TrimEnd() + ellipsis;
        }

        return normalized;
    }

    private static string? CreateSafeSourceName(string fileName, string title)
    {
        var sourceName = Normalize(fileName);
        if (sourceName.Length == 0)
        {
            sourceName = Normalize(title);
        }

        if (sourceName.Length > MaxSourceNameCharacters)
        {
            sourceName = sourceName[..MaxSourceNameCharacters].TrimEnd();
        }

        return sourceName.Length == 0 ? null : sourceName;
    }

    private static string Normalize(string value)
    {
        var safeText = new string(value
            .Where(character => !char.IsControl(character) || char.IsWhiteSpace(character))
            .ToArray());
        return string.Join(' ', safeText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record PersistedCitationSource(
        Guid ChunkId,
        Guid DocumentId,
        string Content,
        string FileName,
        string Title);
}