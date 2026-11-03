using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class EmbeddingService : IEmbeddingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        ApplicationDbContext dbContext,
        IEmbeddingProvider embeddingProvider,
        ITelemetryService telemetryService,
        ILogger<EmbeddingService> logger)
    {
        _dbContext = dbContext;
        _embeddingProvider = embeddingProvider;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public async Task<DocumentEmbedding> GenerateEmbeddingAsync(
        Guid chunkId,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
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

            if (vector.Count == 0)
            {
                throw new InvalidOperationException("Embedding provider returned an empty vector.");
            }

            var embedding = new DocumentEmbedding
            {
                ChunkId = chunkId,
                EmbeddingVector = new Vector(vector.ToArray()),
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

            _telemetryService.TrackEmbeddingGeneration(
                resolvedModelName,
                stopwatch.Elapsed.TotalMilliseconds,
                embedding.VectorDimensions,
                true);

            return embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding generation failed for chunk {ChunkId}.", chunkId);
            _telemetryService.TrackEmbeddingGeneration(
                modelName ?? "unknown",
                stopwatch.Elapsed.TotalMilliseconds,
                0,
                false);
            throw;
        }
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
