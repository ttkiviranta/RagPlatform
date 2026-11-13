namespace RagPlatform.Api.Contracts.Documents;

public interface IDocumentImportMessagePublisher
{
    Task PublishAsync(DocumentImportMessage message, CancellationToken cancellationToken = default);
}
