using System.Linq;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Authorization;

public interface IDocumentAccessService
{
    Task<User> GetRequiredCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<Document?> GetAccessibleDocumentAsync(
        Guid documentId,
        DocumentPermission permission,
        CancellationToken cancellationToken = default);

    IQueryable<Document> ScopeDocuments(IQueryable<Document> documents, User user);
}
