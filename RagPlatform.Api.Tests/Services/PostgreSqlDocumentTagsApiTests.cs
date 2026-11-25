using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("PostgreSql")]
[DoNotParallelize]
public sealed class PostgreSqlDocumentTagsApiTests : PostgreSqlTestClassBase
{
    private const string Scheme = "DocumentTagTest";
    private const string ConnectionVariable = "RAGPLATFORM_TEST_POSTGRES_CONNECTION";

    [TestInitialize]
    public void RequirePostgreSql()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable)))
        {
            Assert.Inconclusive($"Set {ConnectionVariable} to a disposable database.");
        }
    }

    [TestMethod]
    public async Task DocumentTags_CreateUpdateClearAndAuthorization_WorkAcrossRequests()
    {
        using var db = new ApplicationDbContext(TestDbContextOptions.Create());
        var owner = PostgreSqlTestData.CreateUser("tag-tenant-a");
        var other = PostgreSqlTestData.CreateUser("tag-tenant-a");
        var foreign = PostgreSqlTestData.CreateUser("tag-tenant-b");
        foreach (var user in new[] { owner, other, foreign })
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = RoleNames.User });
        }
        db.Users.AddRange(owner, other, foreign);
        await db.SaveChangesAsync();

        using var factory = CreateFactory();
        using var client = CreateClient(factory, owner);
        using var otherClient = CreateClient(factory, other);
        using var foreignClient = CreateClient(factory, foreign);
        var create = await client.PostAsJsonAsync("/api/documents", new { title = "test", content = "content", tags = new[] { " security ", "Identity" } });
        Assert.AreEqual(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        AssertTags(created, "Identity", "security");

        AssertTags(await client.GetFromJsonAsync<JsonElement>($"/api/documents/{id}"), "Identity", "security");
        var listed = await client.GetFromJsonAsync<JsonElement>("/api/documents");
        AssertTags(listed.EnumerateArray().Single(), "Identity", "security");
        Assert.AreEqual(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/documents/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await foreignClient.GetAsync($"/api/documents/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await otherClient.PutAsJsonAsync($"/api/documents/{id}", new { title = "test", content = "content", tags = new[] { "forbidden" } })).StatusCode);

        var invalid = await client.PutAsJsonAsync($"/api/documents/{id}", new { title = "test", content = "changed", tags = new[] { "x", " x " } });
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        var validation = await invalid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.IsTrue(validation.GetProperty("errors").TryGetProperty("tags", out _));
        var unchanged = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{id}");
        AssertTags(unchanged, "Identity", "security");
        Assert.AreEqual("content", unchanged.GetProperty("content").GetString());

        var replace = await client.PutAsJsonAsync($"/api/documents/{id}", new { title = "test", content = "content", tags = new[] { "Identity", "new" } });
        Assert.AreEqual(HttpStatusCode.OK, replace.StatusCode);
        AssertTags(await replace.Content.ReadFromJsonAsync<JsonElement>(), "Identity", "new");
        AssertTags(await client.GetFromJsonAsync<JsonElement>($"/api/documents/{id}"), "Identity", "new");
        var preserve = await client.PutAsJsonAsync($"/api/documents/{id}", new { title = "test", content = "content" });
        AssertTags(await preserve.Content.ReadFromJsonAsync<JsonElement>(), "Identity", "new");
        var preserveNull = await client.PutAsJsonAsync($"/api/documents/{id}", new { title = "test", content = "content", tags = (string[]?)null });
        AssertTags(await preserveNull.Content.ReadFromJsonAsync<JsonElement>(), "Identity", "new");
        var clear = await client.PutAsJsonAsync($"/api/documents/{id}", new { title = "test", content = "content", tags = Array.Empty<string>() });
        AssertTags(await clear.Content.ReadFromJsonAsync<JsonElement>());
        AssertTags(await client.GetFromJsonAsync<JsonElement>($"/api/documents/{id}"));
    }

    [TestMethod]
    public async Task TextUpload_AndLegacyCreate_ReturnExpectedTags()
    {
        using var db = new ApplicationDbContext(TestDbContextOptions.Create());
        var owner = PostgreSqlTestData.CreateUser("tag-upload-tenant");
        owner.UserRoles.Add(new UserRole { UserId = owner.Id, RoleName = RoleNames.User });
        db.Users.Add(owner);
        await db.SaveChangesAsync();
        using var factory = CreateFactory();
        using var client = CreateClient(factory, owner);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("upload content"), "file", "upload.txt");
        form.Add(new StringContent("alpha"), "tags");
        form.Add(new StringContent(" beta "), "tags");
        var upload = await client.PostAsync("/api/documents/upload", form);
        Assert.AreEqual(HttpStatusCode.Created, upload.StatusCode);
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>();
        AssertTags(uploaded, "alpha", "beta");
        AssertTags(await client.GetFromJsonAsync<JsonElement>($"/api/documents/{uploaded.GetProperty("id").GetGuid()}"), "alpha", "beta");

        var legacy = await client.PostAsJsonAsync("/api/documents", new { title = "legacy", content = "content" });
        Assert.AreEqual(HttpStatusCode.Created, legacy.StatusCode);
        AssertTags(await legacy.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static void AssertTags(JsonElement document, params string[] expected)
    {
        CollectionAssert.AreEqual(expected, document.GetProperty("tags").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    private static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = Environment.GetEnvironmentVariable(ConnectionVariable),
            ["ServiceBus:DocumentImportWorkerEnabled"] = "false"
        }));
        builder.ConfigureServices(services => services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Scheme;
            options.DefaultChallengeScheme = Scheme;
            options.DefaultForbidScheme = Scheme;
        }).AddScheme<AuthenticationSchemeOptions, TagAuthenticationHandler>(Scheme, _ => { }));
    });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, User user)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Test-Object", user.EntraObjectId);
        client.DefaultRequestHeaders.Add("X-Test-Tenant", user.TenantId);
        return client;
    }

    private sealed class TagAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var oid = Request.Headers["X-Test-Object"].ToString();
            var tenant = Request.Headers["X-Test-Tenant"].ToString();
            if (string.IsNullOrEmpty(oid) || string.IsNullOrEmpty(tenant))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid", oid), new Claim("tid", tenant)], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
