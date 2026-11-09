using Microsoft.AspNetCore.Authorization;
using RagPlatform.Api.Contracts.Identity;

namespace RagPlatform.Api.Authorization;

public sealed class UserRoleManagementAuthorizationHandler : AuthorizationHandler<UserRoleManagementRequirement>
{
    private readonly ICurrentUserService _currentUserService;

    public UserRoleManagementAuthorizationHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        UserRoleManagementRequirement requirement)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser is { IsActive: true } && RoleHierarchy.CanManageUserRoles(currentUser))
        {
            context.Succeed(requirement);
        }
    }
}
