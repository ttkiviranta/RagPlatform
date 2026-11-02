using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Web;

public interface IWebContentService
{
    Task<Document> ImportAsync(string url, CancellationToken cancellationToken = default);
}