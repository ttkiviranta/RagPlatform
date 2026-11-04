using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
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

    public Guid? UserId
    {
        get
        {
            var entraObjectId = EntraObjectId;
            return Guid.TryParse(entraObjectId, out var userId) ? userId : null;
        }
    }

    public string? EntraObjectId => _httpContextAccessor.HttpContext?.User?.FindFirst("oid")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
        ?? _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

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
        if (string.IsNullOrWhiteSpace(entraObjectId))
        {
            return null;
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(currentUser => currentUser.EntraObjectId == entraObjectId, cancellationToken);

        if (user is not null)
        {
            return user;
        }

        return await _userBootstrapService.GetOrCreateCurrentUserAsync(principal, cancellationToken);
    }
}
