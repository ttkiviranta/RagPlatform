using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Chunking;

public interface IDocumentChunkingService
{
    Task<IReadOnlyList<DocumentChunk>> GenerateChunksAsync(
        Guid documentId,
        int chunkSize,
        int chunkOverlap,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GenerateChunksForImportAsync(
        Document validatedDocument,
        int chunkSize,
        int chunkOverlap,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> GetChunksForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<DocumentChunk?> GetByIdAsync(
        Guid chunkId,
        CancellationToken cancellationToken = default);
}
