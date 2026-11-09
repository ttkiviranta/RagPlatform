using Microsoft.AspNetCore.Authorization;
using RagPlatform.Api.Contracts.Identity;

namespace RagPlatform.Api.Authorization;

public sealed class ActiveUserAuthorizationHandler : AuthorizationHandler<ActiveUserRequirement>
{
    private readonly ICurrentUserService _currentUserService;

    public ActiveUserAuthorizationHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser is { IsActive: true })
        {
            context.Succeed(requirement);
        }
    }
}
