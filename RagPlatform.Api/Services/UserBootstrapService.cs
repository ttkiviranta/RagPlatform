using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class UserBootstrapService : IUserBootstrapService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserBootstrapService> _logger;

    public UserBootstrapService(
        ApplicationDbContext dbContext,
        ILogger<UserBootstrapService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Models.User> GetOrCreateCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var entraObjectId = principal.FindFirst("oid")?.Value
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        var tenantId = GetClaimValue(principal, "tid", "http://schemas.microsoft.com/identity/claims/tenantid");

        if (string.IsNullOrWhiteSpace(entraObjectId) || string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("The authenticated principal does not contain valid Entra object and tenant identifiers.");
        }

        var existingUser = await _dbContext.Users
            .Include(user => user.UserRoles)
            .FirstOrDefaultAsync(
                user => user.EntraObjectId == entraObjectId && user.TenantId == tenantId,
                cancellationToken);

        if (existingUser is not null)
        {
            existingUser.Email = GetClaimValue(principal, "preferred_username", "email", ClaimTypes.Email) ?? existingUser.Email;
            existingUser.UserName = GetClaimValue(principal, "preferred_username", "upn", ClaimTypes.Name) ?? existingUser.UserName;
            existingUser.FirstName = GetClaimValue(principal, "given_name") ?? existingUser.FirstName;
            existingUser.LastName = GetClaimValue(principal, "family_name") ?? existingUser.LastName;
            existingUser.LastLoginAt = DateTime.UtcNow;
            existingUser.UpdatedAt = DateTime.UtcNow;

            await EnsureBaseRoleAsync(existingUser, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return existingUser;
        }

        var newUser = new User
        {
            EntraObjectId = entraObjectId,
            Email = GetClaimValue(principal, "preferred_username", "email", ClaimTypes.Email),
            UserName = GetClaimValue(principal, "preferred_username", "upn", ClaimTypes.Name) ?? entraObjectId,
            FirstName = GetClaimValue(principal, "given_name"),
            LastName = GetClaimValue(principal, "family_name"),
            TenantId = tenantId,
            IdentityProvider = "MicrosoftEntraId",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(newUser);
        await EnsureBaseRoleAsync(newUser, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bootstrapped new user for Entra object id {EntraObjectId}.", entraObjectId);
        return newUser;
    }

    private async Task EnsureBaseRoleAsync(User user, CancellationToken cancellationToken)
    {
        if (user.UserRoles.Any(userRole => userRole.RoleName == RoleNames.User))
        {
            return;
        }

        var baseRole = await _dbContext.Roles
            .SingleAsync(role => role.Name == RoleNames.User, cancellationToken);

        user.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            RoleName = baseRole.Name,
            Role = baseRole,
            AssignedAt = DateTime.UtcNow
        });
    }

    private static string? GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
