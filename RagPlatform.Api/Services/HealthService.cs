using RagPlatform.Api.Contracts.Health;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class HealthService : IHealthService
{
    public HealthStatusResponse GetStatus()
    {
        return new HealthStatusResponse
        {
            Status = "Healthy",
            Application = "RAG Platform",
            Version = "0.1.0"
        };
    }
}
