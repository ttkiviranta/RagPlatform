using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Embeddings;

public interface IEmbeddingService
{
    Task<DocumentEmbedding> GenerateEmbeddingAsync(
        Guid chunkId,
        string? modelName = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentEmbedding>> GetEmbeddingsForChunkAsync(
        Guid chunkId,
        CancellationToken cancellationToken = default);

    Task<DocumentEmbedding?> GetByIdAsync(
        Guid embeddingId,
        CancellationToken cancellationToken = default);
}
