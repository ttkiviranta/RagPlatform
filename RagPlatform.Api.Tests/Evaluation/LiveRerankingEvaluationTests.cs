using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Evaluation;

[TestClass]
[TestCategory("LiveEvaluation")]
public sealed class LiveRerankingEvaluationTests
{
    [TestMethod]
    public async Task CompareHybridAndReranking()
    {
        if (Environment.GetEnvironmentVariable("RAG_EVAL_RUN") != "1")
            Assert.Inconclusive("Set RAG_EVAL_RUN=1 to explicitly enable the live, billable evaluation.");

        var apiUri = new Uri(Required("RAG_EVAL_API_URL"), UriKind.Absolute);
        if (apiUri.Scheme != Uri.UriSchemeHttps && !(apiUri.Scheme == Uri.UriSchemeHttp && apiUri.IsLoopback))
            throw new InvalidOperationException("The API URL must use HTTPS, or HTTP on loopback only.");
        if (!string.IsNullOrEmpty(apiUri.UserInfo) || !string.IsNullOrEmpty(apiUri.Query) || !string.IsNullOrEmpty(apiUri.Fragment))
            throw new InvalidOperationException("The API URL must not contain credentials, query, or fragment.");
        var token = Required("RAG_EVAL_API_TOKEN");
        var caseFile = Required("RAG_EVAL_CASES");
        var outputFile = Required("RAG_EVAL_OUTPUT");
        var testCases = JsonSerializer.Deserialize<RerankingEvaluationCase[]>(await File.ReadAllTextAsync(caseFile), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("The case file must contain an array.");
        if (testCases.Length == 0 || testCases.Any(item => item is null || string.IsNullOrWhiteSpace(item.CaseId) || string.IsNullOrWhiteSpace(item.Query) || item.RelevantDocumentIds is null || item.RelevantDocumentIds.Any(id => id == Guid.Empty)) || testCases.Select(item => item.CaseId).Distinct(StringComparer.Ordinal).Count() != testCases.Length)
            throw new InvalidOperationException("Cases need unique IDs, nonempty queries, and a relevantDocumentIds array of nonempty IDs; use an empty array for unjudged cases.");
        var topK = int.Parse(Environment.GetEnvironmentVariable("RAG_EVAL_TOPK") ?? "5", System.Globalization.CultureInfo.InvariantCulture);
        if (topK is < 1 or > 20) throw new InvalidOperationException("RAG_EVAL_TOPK must be between 1 and 20.");
        var foundryOptions = new FoundryOptions
        {
            Endpoint = Required("Foundry__Endpoint"),
            ChatDeployment = Required("Foundry__ChatDeployment"),
            MaxOutputTokens = int.Parse(Environment.GetEnvironmentVariable("Foundry__MaxOutputTokens") ?? "800", System.Globalization.CultureInfo.InvariantCulture),
            ChatTimeoutSeconds = int.Parse(Environment.GetEnvironmentVariable("Foundry__ChatTimeoutSeconds") ?? "60", System.Globalization.CultureInfo.InvariantCulture),
            ChatTemperature = double.Parse(Environment.GetEnvironmentVariable("Foundry__ChatTemperature") ?? "0.2", System.Globalization.CultureInfo.InvariantCulture)
        };
        if (foundryOptions.MaxOutputTokens <= 0 || foundryOptions.ChatTimeoutSeconds is < 1 or > 60 || !double.IsFinite(foundryOptions.ChatTemperature) || foundryOptions.ChatTemperature is < 0 or > 2)
            throw new InvalidOperationException("Invalid Foundry chat settings.");
        await using var output = new FileStream(outputFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var services = new ServiceCollection().AddHttpClient().BuildServiceProvider();
        var options = Microsoft.Extensions.Options.Options.Create(foundryOptions);
        var inference = new FoundryInferenceClient(services.GetRequiredService<IHttpClientFactory>(), new DefaultAzureCredential(), options);
        var chat = new CapturingEvaluationChat(new FoundryChatCompletionService(inference, options, new EvaluationUser(), new EvaluationAudit()));
        var reranker = new FoundryRerankingService(chat);
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = apiUri, Timeout = TimeSpan.FromMinutes(2) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var hybrid = new HttpHybridSearch(http);
        var results = new List<RerankingEvaluationResult>();
        foreach (var testCase in testCases)
            results.Add(await RerankingEvaluation.RunAsync(testCase, topK, hybrid, reranker, chat));

        var report = new
        {
            TimestampUtc = DateTimeOffset.UtcNow, TopK = topK, foundryOptions.ChatDeployment,
            foundryOptions.ChatTemperature, foundryOptions.MaxOutputTokens, foundryOptions.ChatTimeoutSeconds,
            Summary = EvaluationSummary.Create(results),
            Results = results
        };
        await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        Assert.IsFalse(results.Any(result => result.Status != "reranked"), "One or more cases could not be reranked; see the report for statuses.");
    }

    private static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value)
        ? value : throw new InvalidOperationException($"{name} is required for the live evaluation.");

    private sealed class HttpHybridSearch(HttpClient http) : IHybridSearchService
    {
        public async Task<IReadOnlyList<HybridSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default, Guid? documentId = null, IReadOnlyCollection<string?>? tags = null)
        {
            using var response = await http.PostAsJsonAsync("/api/search/hybrid", new { query, topK, documentId, tags }, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<HybridSearchResult[]>(cancellationToken) ?? throw new InvalidOperationException("Hybrid search returned no response body.");
        }
    }

    private sealed class EvaluationUser : ICurrentUserService
    {
        public bool IsAuthenticated => false;
        public ClaimsPrincipal? Principal => null;
        public string? EntraObjectId => null;
        public string? Email => null;
        public string? TenantId => null;
        public Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
    }

    private sealed class EvaluationAudit : IAuditEventWriter
    {
        public void Add(Guid? actorUserId, string tenantId, string eventType, string outcome, string? targetType = null, string? targetId = null, double? durationMilliseconds = null, int? inputCharacterCount = null, int? requestedTopK = null, int? resultCount = null, double? averageSimilarity = null, int? contextCharacterCount = null, int? promptCharacterCount = null, string? modelProvider = null, string? modelDeployment = null, string? modelOperation = null, int? inputTokenCount = null, int? outputTokenCount = null, int? generatedEmbeddingCount = null, int? reusedEmbeddingCount = null) => throw new InvalidOperationException("The evaluation has no application audit identity.");
        public Task FlushAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("The evaluation has no application audit identity.");
    }
}
