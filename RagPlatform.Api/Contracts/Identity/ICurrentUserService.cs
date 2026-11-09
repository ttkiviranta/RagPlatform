using System.Security.Claims;

namespace RagPlatform.Api.Contracts.Identity;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    ClaimsPrincipal? Principal { get; }

    string? EntraObjectId { get; }

    string? Email { get; }

    string? TenantId { get; }

    Task<Models.User?> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
