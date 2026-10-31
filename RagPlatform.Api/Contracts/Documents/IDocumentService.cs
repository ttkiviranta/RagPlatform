using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Documents;

public interface IDocumentService
{
    Task<IReadOnlyCollection<Document>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Document> CreateAsync(DocumentCreateRequest request, CancellationToken cancellationToken = default);

    Task<Document?> UpdateAsync(Guid id, DocumentUpdateRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Document> UploadTextDocumentAsync(IFormFile file, CancellationToken cancellationToken = default);
}
