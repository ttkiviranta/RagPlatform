using Microsoft.Extensions.Configuration;
using Npgsql;
using RagPlatform.Api.Data;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
[DoNotParallelize]
public sealed class PostgreSqlConnectionStringConfigurationTests
{
    [TestMethod]
    public void ConfiguredConnectionString_TakesPrecedenceOverFallbackEnvironmentVariables()
    {
        using var environment = SetFallbackEnvironment("fallback-password");
        var configuredConnectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "configured-host",
            Port = 5440,
            Database = "configured-database",
            Username = "configured-user",
            Password = "configured-password"
        }.ConnectionString;

        var actual = GetConfiguredConnectionString(configuredConnectionString);

        Assert.AreEqual("configured-host", actual.Host);
        Assert.AreEqual(5440, actual.Port);
        Assert.AreEqual("configured-database", actual.Database);
        Assert.AreEqual("configured-user", actual.Username);
        Assert.AreEqual("configured-password", actual.Password);
    }

    [TestMethod]
    public void FallbackEnvironmentVariables_AreMappedToConnectionString()
    {
        using var environment = SetFallbackEnvironment("fallback-password");

        var actual = GetConfiguredConnectionString(string.Empty);

        Assert.AreEqual("fallback-host", actual.Host);
        Assert.AreEqual(5544, actual.Port);
        Assert.AreEqual("fallback-database", actual.Database);
        Assert.AreEqual("fallback-user", actual.Username);
        Assert.AreEqual("fallback-password", actual.Password);
    }

    [TestMethod]
    public void FallbackPassword_SpecialCharactersArePreserved()
    {
        const string password = "p;ass=word with spaces\"and quotes";
        using var environment = SetFallbackEnvironment(password);

        var actual = GetConfiguredConnectionString(string.Empty);

        Assert.AreEqual(password, actual.Password);
    }

    [TestMethod]
    public void FallbackWithoutPassword_LeavesPasswordUnset()
    {
        using var environment = SetFallbackEnvironment(null);

        var actual = GetConfiguredConnectionString(string.Empty);

        Assert.IsTrue(string.IsNullOrEmpty(actual.Password));
    }

    private static NpgsqlConnectionStringBuilder GetConfiguredConnectionString(string defaultConnectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = defaultConnectionString
            })
            .Build();

        var connectionString = PostgreSqlConnectionStringResolver.Resolve(configuration);
        return new NpgsqlConnectionStringBuilder(connectionString);
    }

    private static EnvironmentVariableScope SetFallbackEnvironment(string? password)
    {
        return new EnvironmentVariableScope(new Dictionary<string, string?>
        {
            ["RAGPOSTGRES_HOST"] = "fallback-host",
            ["RAGPOSTGRES_PORT"] = "5544",
            ["RAGPOSTGRES_DB"] = "fallback-database",
            ["RAGPOSTGRES_USER"] = "fallback-user",
            ["RAGPOSTGRES_PASSWORD"] = password
        });
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues;

        public EnvironmentVariableScope(IReadOnlyDictionary<string, string?> values)
        {
            _originalValues = values.Keys.ToDictionary(
                name => name,
                Environment.GetEnvironmentVariable);

            foreach (var (name, value) in values)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }
}
