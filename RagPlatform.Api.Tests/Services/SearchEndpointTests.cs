using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class SearchEndpointTests
{
    private const string Scheme = "SearchTest";
    private static readonly Guid DocumentId = Guid.Parse("9d4db072-a0dc-4ad0-90ce-3b7c38920c75");
    private static readonly Guid ChunkId = Guid.Parse("c54526bd-6962-4209-af0f-140e19b46a08");

    [DataTestMethod]
    [DataRow("fulltext")]
    [DataRow("hybrid")]
    public async Task Search_ValidRequest_ReturnsRankedResultsAndPassesParameters(string route)
    {
        var fullText = new FakeFullTextSearchService();
        var hybrid = new FakeHybridSearchService();
        using var factory = CreateFactory(fullText, hybrid);
        using var client = CreateClient(factory, "active");

        var response = await client.PostAsJsonAsync($"/api/search/{route}", new VectorSearchRequest
        {
            Query = "dead-letter queue",
            TopK = 3,
            DocumentId = DocumentId
        });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        if (route == "fulltext")
        {
            var results = await response.Content.ReadFromJsonAsync<FullTextSearchResult[]>();
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(ChunkId, results[0].ChunkId);
            Assert.AreEqual(DocumentId, results[0].DocumentId);
            Assert.AreEqual("full text", results[0].Content);
            Assert.AreEqual(0.75f, results[0].RankScore);
            Assert.AreEqual(("dead-letter queue", 3, (Guid?)DocumentId), fullText.LastRequest);
            Assert.IsNull(hybrid.LastRequest);
        }
        else
        {
            var results = await response.Content.ReadFromJsonAsync<HybridSearchResult[]>();
            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Length);
            Assert.AreEqual(ChunkId, results[0].ChunkId);
            Assert.AreEqual(DocumentId, results[0].DocumentId);
            Assert.AreEqual("hybrid content", results[0].Content);
            Assert.IsTrue(results[0].FoundByVectorSearch);
            Assert.IsFalse(results[0].FoundByFullTextSearch);
            Assert.AreEqual(("dead-letter queue", 3, (Guid?)DocumentId), hybrid.LastRequest);
            Assert.IsNull(fullText.LastRequest);
        }
    }

    [DataTestMethod]
    [DataRow("fulltext")]
    [DataRow("hybrid")]
    public async Task Search_NoDocumentFilter_PassesNullAndDefaultTopK(string route)
    {
        var fullText = new FakeFullTextSearchService();
        var hybrid = new FakeHybridSearchService();
        using var factory = CreateFactory(fullText, hybrid);
        using var client = CreateClient(factory, "active");

        var response = await client.PostAsJsonAsync($"/api/search/{route}", new { query = "search" });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var request = route == "fulltext" ? fullText.LastRequest : hybrid.LastRequest;
        Assert.AreEqual(("search", 5, (Guid?)null), request);
    }

    [DataTestMethod]
    [DataRow("fulltext")]
    [DataRow("hybrid")]
    public async Task Search_InvalidRequest_ReturnsValidationProblemWithoutCallingServices(string route)
    {
        var fullText = new FakeFullTextSearchService();
        var hybrid = new FakeHybridSearchService();
        using var factory = CreateFactory(fullText, hybrid);
        using var client = CreateClient(factory, "active");

        var response = await client.PostAsJsonAsync($"/api/search/{route}", new { query = "search", topK = 51 });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.IsNotNull(problem);
        Assert.IsTrue(problem.Errors.ContainsKey("TopK"));
        Assert.IsNull(fullText.LastRequest);
        Assert.IsNull(hybrid.LastRequest);
    }

    [DataTestMethod]
    [DataRow("fulltext", null, HttpStatusCode.Unauthorized)]
    [DataRow("hybrid", null, HttpStatusCode.Unauthorized)]
    [DataRow("fulltext", "inactive", HttpStatusCode.Forbidden)]
    [DataRow("hybrid", "inactive", HttpStatusCode.Forbidden)]
    public async Task Search_RequiresActiveUser(string route, string? user, HttpStatusCode expected)
    {
        var fullText = new FakeFullTextSearchService();
        var hybrid = new FakeHybridSearchService();
        using var factory = CreateFactory(fullText, hybrid);
        using var client = CreateClient(factory, user);

        var response = await client.PostAsJsonAsync($"/api/search/{route}", new { query = "search" });

        Assert.AreEqual(expected, response.StatusCode);
        Assert.IsNull(fullText.LastRequest);
        Assert.IsNull(hybrid.LastRequest);
    }

    [DataTestMethod]
    [DataRow("fulltext")]
    [DataRow("hybrid")]
    public async Task Search_ServiceFailure_ReturnsProblemWithoutExposingException(string route)
    {
        var fullText = new FakeFullTextSearchService { Fail = true };
        var hybrid = new FakeHybridSearchService { Fail = true };
        using var factory = CreateFactory(fullText, hybrid);
        using var client = CreateClient(factory, "active");

        var response = await client.PostAsJsonAsync($"/api/search/{route}", new { query = "search" });

        Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.IsNotNull(problem);
        Assert.AreEqual(500, problem.Status);
        Assert.IsNull(problem.Detail);
    }

    [TestMethod]
    public async Task Swagger_ListsBothSearchRoutes()
    {
        using var factory = CreateFactory(new FakeFullTextSearchService(), new FakeHybridSearchService());
        using var client = CreateClient(factory, null);

        var swagger = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/swagger/v1/swagger.json");
        var paths = swagger.GetProperty("paths");
        Assert.IsTrue(paths.GetProperty("/api/search/fulltext").TryGetProperty("post", out _));
        Assert.IsTrue(paths.GetProperty("/api/search/hybrid").TryGetProperty("post", out _));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        FakeFullTextSearchService fullText,
        FakeHybridSearchService hybrid)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ServiceBus:DocumentImportWorkerEnabled"] = "false",
                    ["ApplicationInsights:ConnectionString"] = ""
                }));
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = Scheme;
                    options.DefaultChallengeScheme = Scheme;
                    options.DefaultForbidScheme = Scheme;
                }).AddScheme<AuthenticationSchemeOptions, SearchTestAuthenticationHandler>(Scheme, _ => { });
                services.RemoveAll<ICurrentUserService>();
                services.AddScoped<ICurrentUserService, SearchTestCurrentUserService>();
                services.RemoveAll<IFullTextSearchService>();
                services.RemoveAll<IHybridSearchService>();
                services.AddSingleton<IFullTextSearchService>(fullText);
                services.AddSingleton<IHybridSearchService>(hybrid);
            });
        });
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string? user)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (user is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-User", user);
        }

        return client;
    }

    private sealed class SearchTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-User"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class SearchTestCurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
    {
        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
        public ClaimsPrincipal? Principal => accessor.HttpContext?.User;
        public string? EntraObjectId => null;
        public string? Email => null;
        public string? TenantId => null;

        public Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var isActive = string.Equals(accessor.HttpContext?.Request.Headers["X-Test-User"].ToString(), "active", StringComparison.Ordinal);
            return Task.FromResult<User?>(new User { IsActive = isActive });
        }
    }

    private sealed class FakeFullTextSearchService : IFullTextSearchService
    {
        public (string Query, int TopK, Guid? DocumentId)? LastRequest { get; private set; }
        public bool Fail { get; init; }

        public Task<IReadOnlyList<FullTextSearchResult>> SearchAsync(
            string query, int topK = 5, CancellationToken cancellationToken = default, Guid? documentId = null)
        {
            LastRequest = (query, topK, documentId);
            if (Fail) throw new InvalidOperationException("sensitive failure");
            return Task.FromResult<IReadOnlyList<FullTextSearchResult>>(
                [new FullTextSearchResult { ChunkId = ChunkId, DocumentId = DocumentId, Content = "full text", RankScore = 0.75f }]);
        }
    }

    private sealed class FakeHybridSearchService : IHybridSearchService
    {
        public (string Query, int TopK, Guid? DocumentId)? LastRequest { get; private set; }
        public bool Fail { get; init; }

        public Task<IReadOnlyList<HybridSearchResult>> SearchAsync(
            string query, int topK = 5, CancellationToken cancellationToken = default, Guid? documentId = null)
        {
            LastRequest = (query, topK, documentId);
            if (Fail) throw new InvalidOperationException("sensitive failure");
            return Task.FromResult<IReadOnlyList<HybridSearchResult>>(
                [new HybridSearchResult { ChunkId = ChunkId, DocumentId = DocumentId, Content = "hybrid content", FoundByVectorSearch = true }]);
        }
    }
}
