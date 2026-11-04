namespace RagPlatform.Api.Models;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string EntraObjectId { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? UserName { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string IdentityProvider { get; set; } = "MicrosoftEntraId";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public string TenantId { get; set; } = string.Empty;
}
