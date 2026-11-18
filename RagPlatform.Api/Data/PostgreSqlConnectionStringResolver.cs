using Microsoft.Extensions.Configuration;
using Npgsql;

namespace RagPlatform.Api.Data;

internal static class PostgreSqlConnectionStringResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var host = Environment.GetEnvironmentVariable("RAGPOSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("RAGPOSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("RAGPOSTGRES_DB") ?? "ragplatform";
        var username = Environment.GetEnvironmentVariable("RAGPOSTGRES_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("RAGPOSTGRES_PASSWORD");

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.Parse(port, System.Globalization.CultureInfo.InvariantCulture),
            Database = database,
            Username = username
        };

        if (!string.IsNullOrWhiteSpace(password))
        {
            connectionStringBuilder.Password = password;
        }

        // Leave Password unset when no value is supplied, preserving passwordless authentication configurations.
        return connectionStringBuilder.ConnectionString;
    }
}
