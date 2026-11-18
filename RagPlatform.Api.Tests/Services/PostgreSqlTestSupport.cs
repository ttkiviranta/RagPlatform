using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Tests.Services;

internal static class TestDbContextOptions
{
    private const string ConnectionStringEnvironmentVariable = "RAGPLATFORM_TEST_POSTGRES_CONNECTION";

    public static DbContextOptions<ApplicationDbContext> Create(string? databaseName = null)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            optionsBuilder.UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());
        }
        else
        {
            optionsBuilder.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector());
        }

        return optionsBuilder.Options;
    }
}

internal static class PostgreSqlTestData
{
    public static User CreateUser(string tenantId)
    {
        return new User
        {
            EntraObjectId = Guid.NewGuid().ToString("N"),
            TenantId = tenantId
        };
    }

    public static Document WithOwner(Document document)
    {
        var owner = CreateUser(document.TenantId);
        document.Owner = owner;
        document.OwnerUserId = owner.Id;
        return document;
    }
}

public abstract class PostgreSqlTestClassBase
{
    private const string ConnectionStringEnvironmentVariable = "RAGPLATFORM_TEST_POSTGRES_CONNECTION";

    [TestInitialize]
    public Task ResetPostgreSqlDataAsync()
    {
        return ResetDatabaseAsync();
    }

    [TestCleanup]
    public async Task ClearPostgreSqlDataAsync()
    {
        await ResetDatabaseAsync();
    }

    private static async Task ResetDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE TABLE
                "AuditEvents",
                "ConversationCitations",
                "ConversationMessages",
                "Conversations",
                "DocumentEmbeddings",
                "DocumentChunks",
                "DocumentMetadata",
                "Documents",
                "UserRoles",
                "Users"
            RESTART IDENTITY CASCADE;
            """;
        await command.ExecuteNonQueryAsync();
    }
}
