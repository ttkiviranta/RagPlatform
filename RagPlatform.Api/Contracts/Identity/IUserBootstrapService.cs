using System.Security.Claims;

namespace RagPlatform.Api.Contracts.Identity;

public interface IUserBootstrapService
{
    Task<Models.User> GetOrCreateCurrentUserAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}
