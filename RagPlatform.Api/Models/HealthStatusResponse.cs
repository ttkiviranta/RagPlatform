namespace RagPlatform.Api.Models;

public sealed class HealthStatusResponse
{
    public required string Status { get; init; }

    public required string Application { get; init; }

    public required string Version { get; init; }
}
