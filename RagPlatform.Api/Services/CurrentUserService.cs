using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;

namespace RagPlatform.Api.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserBootstrapService _userBootstrapService;

    public CurrentUserService(
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        IUserBootstrapService userBootstrapService)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _userBootstrapService = userBootstrapService;
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public string? EntraObjectId => _httpContextAccessor.HttpContext?.User?.FindFirst("oid")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

    public string? Email => _httpContextAccessor.HttpContext?.User?.FindFirst("preferred_username")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("email")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Email)?.Value;

    public string? TenantId => _httpContextAccessor.HttpContext?.User?.FindFirst("tid")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value;

    public async Task<Models.User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null || !IsAuthenticated)
        {
            return null;
        }

        var principal = httpContext.User;
        if (principal.Identity is null || !principal.Identity.IsAuthenticated)
        {
            return null;
        }

        var entraObjectId = EntraObjectId;
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(entraObjectId) || string.IsNullOrWhiteSpace(tenantId))
        {
            return null;
        }

        var user = await _dbContext.Users
            .Include(currentUser => currentUser.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(
                currentUser => currentUser.EntraObjectId == entraObjectId && currentUser.TenantId == tenantId,
                cancellationToken);

        if (user is not null)
        {
            if (!user.UserRoles.Any(userRole => userRole.RoleName == RoleNames.User))
            {
                await _userBootstrapService.GetOrCreateCurrentUserAsync(principal, cancellationToken);
                return await _dbContext.Users
                    .Include(currentUser => currentUser.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
                    .FirstOrDefaultAsync(
                        currentUser => currentUser.EntraObjectId == entraObjectId && currentUser.TenantId == tenantId,
                        cancellationToken);
            }

            return user;
        }

        await _userBootstrapService.GetOrCreateCurrentUserAsync(principal, cancellationToken);

        return await _dbContext.Users
            .Include(currentUser => currentUser.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(
                currentUser => currentUser.EntraObjectId == entraObjectId && currentUser.TenantId == tenantId,
                cancellationToken);
    }
}
