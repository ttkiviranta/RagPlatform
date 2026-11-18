using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace RagPlatform.Api.Data;

public sealed class ApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var connectionString = PostgreSqlConnectionStringResolver.Resolve(builder.Configuration);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .Options;

        return new ApplicationDbContext(options);
    }
}
