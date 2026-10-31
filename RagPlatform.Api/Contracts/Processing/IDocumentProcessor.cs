using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Processing;

public interface IDocumentProcessor
{
    string DocumentType { get; }

    Task<DocumentMetadata> ProcessAsync(Document document, CancellationToken cancellationToken = default);
}
