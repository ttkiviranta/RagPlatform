namespace RagPlatform.Api.Models;

public sealed class UserRole
{
    public Guid UserId { get; set; }

    public User User { get; set; } = default!;

    public string RoleName { get; set; } = string.Empty;

    public Role Role { get; set; } = default!;

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
