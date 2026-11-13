using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Documents;

public interface IDocumentImportSettlementPolicy
{
    Task<DocumentImportSettlementDisposition> DecideAsync(
        Document document,
        Exception? processingException,
        CancellationToken cancellationToken = default);
}