using Microsoft.AspNetCore.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;

namespace RagPlatform.Api.Authorization;

public sealed class ActiveUserAuthorizationHandler : AuthorizationHandler<ActiveUserRequirement>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ApplicationDbContext _dbContext;

    public ActiveUserAuthorizationHandler(
        ICurrentUserService currentUserService,
        IAuditEventWriter auditEventWriter,
        ApplicationDbContext dbContext)
    {
        _currentUserService = currentUserService;
        _auditEventWriter = auditEventWriter;
        _dbContext = dbContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ActiveUserRequirement requirement)
    {
        var currentUser = await _currentUserService.GetCurrentUserAsync();
        if (currentUser is { IsActive: true })
        {
            context.Succeed(requirement);
            return;
        }

        var tenantId = _currentUserService.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            _auditEventWriter.Add(
                currentUser?.Id,
                tenantId,
                AuditEventTypes.ActiveUserAuthorizationDenied,
                "Denied",
                "User");
            await _dbContext.SaveChangesAsync();
        }
    }
}
