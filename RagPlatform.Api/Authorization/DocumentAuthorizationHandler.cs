using Microsoft.AspNetCore.Authorization;
using RagPlatform.Api.Contracts.Identity;

namespace RagPlatform.Api.Authorization;

public sealed class DocumentAuthorizationHandler
    : AuthorizationHandler<DocumentPermissionRequirement, DocumentAuthorizationResource>
{
    private readonly ICurrentUserService _currentUserService;

    public DocumentAuthorizationHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentPermissionRequirement requirement,
        DocumentAuthorizationResource document)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser is not { IsActive: true }
            || !string.Equals(currentUser.TenantId, document.TenantId, StringComparison.Ordinal))
        {
            return;
        }

        if (RoleHierarchy.CanManageTenantDocuments(currentUser)
            || (RoleHierarchy.HasRole(currentUser, RoleNames.User) && document.OwnerUserId == currentUser.Id))
        {
            context.Succeed(requirement);
        }
    }
}
