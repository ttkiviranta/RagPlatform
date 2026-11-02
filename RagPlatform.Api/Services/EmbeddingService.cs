using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        ApplicationDbContext dbContext,
        IEmbeddingProvider embeddingProvider,
        ILogger<EmbeddingService> logger)
    {
        _dbContext = dbContext;
        _embeddingProvider = embeddingProvider;
        _logger = logger;
    }

    public async Task<DocumentEmbedding> GenerateEmbeddingAsync(
        Guid chunkId,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        var chunk = await _dbContext.DocumentChunks
            .FirstOrDefaultAsync(chunk => chunk.Id == chunkId, cancellationToken);

        if (chunk is null)
        {
            throw new KeyNotFoundException($"Chunk {chunkId} was not found.");
        }

        var resolvedModelName = string.IsNullOrWhiteSpace(modelName)
            ? "placeholder-embedding-model"
            : modelName.Trim();

        var vector = await _embeddingProvider.GenerateEmbeddingAsync(
            chunk.Content,
            resolvedModelName,
            cancellationToken);

        var embedding = new DocumentEmbedding
        {
            ChunkId = chunkId,
            VectorDimensions = vector.Count,
            ModelName = resolvedModelName,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DocumentEmbeddings.Add(embedding);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Generated embedding for chunk {ChunkId} using model {ModelName} with {VectorDimensions} dimensions.",
            chunkId,
            resolvedModelName,
            embedding.VectorDimensions);

        return embedding;
    }

    public async Task<IReadOnlyList<DocumentEmbedding>> GetEmbeddingsForChunkAsync(
        Guid chunkId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DocumentEmbeddings
            .AsNoTracking()
            .Where(embedding => embedding.ChunkId == chunkId)
            .OrderBy(embedding => embedding.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentEmbedding?> GetByIdAsync(
        Guid embeddingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.DocumentEmbeddings
            .AsNoTracking()
            .FirstOrDefaultAsync(embedding => embedding.Id == embeddingId, cancellationToken);
    }
}
