using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Documents;

public interface IStagingBlobStore
{
    Task<Stream> DownloadAsync(Document document, CancellationToken cancellationToken = default);

    Task DeleteAsync(Document document, CancellationToken cancellationToken = default);
}
