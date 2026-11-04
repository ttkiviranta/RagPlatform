using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Data;

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
            ?? principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(entraObjectId))
        {
            throw new InvalidOperationException("The authenticated principal does not contain a valid Entra object identifier.");
        }

        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.EntraObjectId == entraObjectId, cancellationToken);

        if (existingUser is not null)
        {
            existingUser.Email ??= GetClaimValue(principal, "preferred_username", "email", ClaimTypes.Email);
            existingUser.UserName ??= GetClaimValue(principal, "preferred_username", "upn", ClaimTypes.Name);
            existingUser.FirstName ??= GetClaimValue(principal, "given_name");
            existingUser.LastName ??= GetClaimValue(principal, "family_name");
            existingUser.TenantId = GetClaimValue(principal, "tid") ?? existingUser.TenantId;
            existingUser.LastLoginAt = DateTime.UtcNow;
            existingUser.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existingUser;
        }

        var newUser = new Models.User
        {
            EntraObjectId = entraObjectId,
            Email = GetClaimValue(principal, "preferred_username", "email", ClaimTypes.Email),
            UserName = GetClaimValue(principal, "preferred_username", "upn", ClaimTypes.Name) ?? entraObjectId,
            FirstName = GetClaimValue(principal, "given_name"),
            LastName = GetClaimValue(principal, "family_name"),
            TenantId = GetClaimValue(principal, "tid") ?? string.Empty,
            IdentityProvider = "MicrosoftEntraId",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Bootstrapped new user for Entra object id {EntraObjectId}.", entraObjectId);
        return newUser;
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
