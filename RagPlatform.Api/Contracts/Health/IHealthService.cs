using RagPlatform.Api.Models;

namespace RagPlatform.Api.Contracts.Health;

public interface IHealthService
{
    HealthStatusResponse GetStatus();
}
