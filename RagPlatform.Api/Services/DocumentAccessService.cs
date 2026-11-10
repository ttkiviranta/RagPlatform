using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentAccessService : IDocumentAccessService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;

    public DocumentAccessService(
        ApplicationDbContext dbContext,
        IAuthorizationService authorizationService,
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<User> GetRequiredCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var user = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        if (user is not { IsActive: true })
        {
            throw new UnauthorizedAccessException("An active application user is required.");
        }

        return user;
    }

    public async Task<Document?> GetAccessibleDocumentAsync(
        Guid documentId,
        DocumentPermission permission,
        CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .AsNoTracking()
            .Where(item => item.Id == documentId)
            .Select(item => new DocumentAuthorizationResource(item.OwnerUserId, item.TenantId))
            .FirstOrDefaultAsync(cancellationToken);

        if (_currentUserService.Principal is not { } principal)
        {
            return null;
        }

        var currentUser = await _currentUserService.GetCurrentUserAsync(cancellationToken);
        if (currentUser is not { IsActive: true })
        {
            return null;
        }

        if (document is null)
        {
            await RecordDeniedAccessAsync(currentUser, documentId, cancellationToken);
            return null;
        }

        var policyName = permission switch
        {
            DocumentPermission.Read => AuthorizationPolicies.DocumentRead,
            DocumentPermission.Write => AuthorizationPolicies.DocumentWrite,
            DocumentPermission.Delete => AuthorizationPolicies.DocumentDelete,
            _ => throw new ArgumentOutOfRangeException(nameof(permission))
        };

        var authorization = await _authorizationService.AuthorizeAsync(principal, document, policyName);
        if (!authorization.Succeeded)
        {
            await RecordDeniedAccessAsync(currentUser, documentId, cancellationToken);
            return null;
        }

        return await _dbContext.Documents
            .FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);
    }

    private async Task RecordDeniedAccessAsync(User currentUser, Guid documentId, CancellationToken cancellationToken)
    {
        _auditEventWriter.Add(
            currentUser.Id,
            currentUser.TenantId,
            AuditEventTypes.DocumentAccessDenied,
            "Denied",
            "Document",
            documentId.ToString());
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public IQueryable<Document> ScopeDocuments(IQueryable<Document> documents, User user)
    {
        var tenantId = user.TenantId;
        var userId = user.Id;
        var canManageTenant = RoleHierarchy.CanManageTenantDocuments(user);
        var canAccessOwned = RoleHierarchy.CanAccessOwnedDocuments(user);

        return documents.Where(document =>
            document.TenantId == tenantId
            && (canManageTenant || (canAccessOwned && document.OwnerUserId == userId)));
    }
}
