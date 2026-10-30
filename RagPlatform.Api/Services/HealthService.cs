using RagPlatform.Api.Contracts.Health;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class HealthService : IHealthService
{
    private readonly ApplicationDbContext _dbContext;

    public HealthService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public HealthStatusResponse GetStatus()
    {
        var databaseAvailable = false;

        try
        {
            databaseAvailable = _dbContext.Database.CanConnect();
        }
        catch
        {
            databaseAvailable = false;
        }

        return new HealthStatusResponse
        {
            Status = databaseAvailable ? "Healthy" : "Degraded",
            Application = "RAG Platform",
            Version = "0.1.0",
            Database = databaseAvailable ? "Connected" : "Not connected or connection string is not configured"
        };
    }
}
