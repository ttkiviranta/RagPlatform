using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Conversations;
using RagPlatform.Api.Contracts.Identity;
using RagPlatform.Api.Contracts.Rag;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;
using RagPlatform.Api.Tests.Services;

namespace RagPlatform.Api.Tests.Evaluation;

[TestClass]
[TestCategory("PostgreSql")]
[DoNotParallelize]
public sealed class PostgreSqlRagEvaluationTests : PostgreSqlTestClassBase
{
    private const string PostgreSqlConnectionEnvironmentVariable = "RAGPLATFORM_TEST_POSTGRES_CONNECTION";

    [TestInitialize]
    public void RequirePostgreSqlConnection()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlConnectionEnvironmentVariable)))
        {
            Assert.Inconclusive($"Set {PostgreSqlConnectionEnvironmentVariable} to run PostgreSQL evaluation tests.");
        }
    }

    [TestMethod]
    public async Task CitationService_CrossTenantIsolation_ExcludesOtherTenantEvidence()
    {
        using var dbContext = CreateDbContext();
        var owner = await AddUserAsync(dbContext, "evaluation-tenant-a", RoleNames.User);
        var otherTenantOwner = await AddUserAsync(dbContext, "evaluation-tenant-b", RoleNames.User);
        var accessible = await AddDocumentAsync(dbContext, owner, "Tenant A evidence", "tenant-a.txt");
        var inaccessible = await AddDocumentAsync(dbContext, otherTenantOwner, "Tenant B secret marker", "tenant-b.txt");
        var citationService = CreateCitationService(dbContext, owner);

        var citations = await citationService.CreateCitationsAsync(
        [
            ToSearchResult(accessible),
            ToSearchResult(inaccessible)
        ]);

        Assert.AreEqual(1, citations.Count);
        Assert.AreEqual(accessible.Chunk.Id, citations[0].ChunkId);
        Assert.AreEqual(accessible.Document.Id, citations[0].DocumentId);
        Assert.AreEqual("Tenant A evidence", citations[0].Excerpt);
        Assert.IsFalse(citations.Any(citation => citation.Excerpt.Contains("Tenant B secret marker", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CitationService_OwnerIsolation_ExcludesOtherOwnersPrivateEvidence()
    {
        using var dbContext = CreateDbContext();
        var owner = await AddUserAsync(dbContext, "evaluation-shared-tenant", RoleNames.User);
        var otherOwner = await AddUserAsync(dbContext, "evaluation-shared-tenant", RoleNames.User);
        var accessible = await AddDocumentAsync(dbContext, owner, "Owner evidence", "owner.txt");
        var inaccessible = await AddDocumentAsync(dbContext, otherOwner, "Private marker", "private.txt");
        var citationService = CreateCitationService(dbContext, owner);

        var citations = await citationService.CreateCitationsAsync(
        [
            ToSearchResult(accessible),
            ToSearchResult(inaccessible)
        ]);

        Assert.AreEqual(1, citations.Count);
        Assert.AreEqual(accessible.Chunk.Id, citations[0].ChunkId);
        Assert.AreEqual("Owner evidence", citations[0].Excerpt);
        Assert.IsFalse(citations.Any(citation => citation.Excerpt.Contains("Private marker", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task CitationService_ReconstructsCitationFromPersistedChunkAndDocument()
    {
        using var dbContext = CreateDbContext();
        var owner = await AddUserAsync(dbContext, "evaluation-citation-tenant", RoleNames.User);
        const string persistedContent = "Persisted authoritative content with normalized whitespace.";
        var persisted = await AddDocumentAsync(dbContext, owner, persistedContent, "persisted-source.txt");
        var citationService = CreateCitationService(dbContext, owner);
        var searchResult = ToSearchResult(persisted);
        searchResult.ContentSnippet = "Untrusted search snippet must not replace persisted content.";

        var citations = await citationService.CreateCitationsAsync([searchResult]);

        var citation = citations.Single();
        Assert.AreEqual(1, citation.CitationNumber);
        Assert.AreEqual(persisted.Document.Id, citation.DocumentId);
        Assert.AreEqual(persisted.Chunk.Id, citation.ChunkId);
        Assert.AreEqual("persisted-source.txt", citation.SourceName);
        Assert.AreEqual(persistedContent, citation.Excerpt);
        Assert.IsNull(citation.PageNumber);
    }

    [TestMethod]
    public async Task RagQuery_SuccessfulAnswer_PersistsConversationAndCitationSnapshot()
    {
        using var dbContext = CreateDbContext();
        var owner = await AddUserAsync(dbContext, "evaluation-persistence-tenant", RoleNames.User);
        var persisted = await AddDocumentAsync(dbContext, owner, "Synthetic allowed answer evidence.", "answer.txt");
        var answer = new RecordingAnswerGenerationService(new GroundedAnswerResult(
            "Synthetic answer [1].",
            [1],
            36,
            120));
        var service = CreateRagService(dbContext, owner, [ToSearchResult(persisted)], answer);

        var response = await service.QueryAsync("What is the synthetic answer?");

        Assert.AreEqual("Synthetic answer [1].", response.Answer);
        Assert.AreEqual(1, response.Citations.Count);
        Assert.AreEqual(persisted.Chunk.Id, response.Citations[0].ChunkId);
        Assert.AreNotEqual(Guid.Empty, response.ConversationId);
        var messages = await dbContext.ConversationMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == response.ConversationId)
            .ToListAsync();
        Assert.AreEqual(2, messages.Count);
        var userMessage = messages.Single(message => message.Role == ConversationMessageRole.User);
        var assistantMessage = messages.Single(message => message.Role == ConversationMessageRole.Assistant);
        Assert.AreEqual("What is the synthetic answer?", userMessage.Content);
        Assert.AreEqual("Synthetic answer [1].", assistantMessage.Content);

        var persistedCitation = await dbContext.ConversationCitations
            .AsNoTracking()
            .SingleAsync(citation => citation.ConversationMessageId == assistantMessage.Id);
        Assert.AreEqual(persisted.Document.Id, persistedCitation.DocumentId);
        Assert.AreEqual(persisted.Chunk.Id, persistedCitation.ChunkId);
        Assert.AreEqual("Synthetic allowed answer evidence.", persistedCitation.Excerpt);
        Assert.AreEqual("answer.txt", persistedCitation.SourceName);
    }

    [TestMethod]
    public async Task RagQuery_FailedGeneration_DoesNotPersistPartialConversationTurns()
    {
        using var dbContext = CreateDbContext();
        var owner = await AddUserAsync(dbContext, "evaluation-failure-tenant", RoleNames.User);
        var persisted = await AddDocumentAsync(dbContext, owner, "Synthetic generation failure evidence.", "failure.txt");
        var answer = new RecordingAnswerGenerationService(new TimeoutException("Synthetic generation failure."));
        var service = CreateRagService(dbContext, owner, [ToSearchResult(persisted)], answer);

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => service.QueryAsync("Trigger synthetic failure?"));

        Assert.AreEqual(0, await dbContext.Conversations.CountAsync());
        Assert.AreEqual(0, await dbContext.ConversationMessages.CountAsync());
        Assert.AreEqual(0, await dbContext.ConversationCitations.CountAsync());
        Assert.AreEqual(1, await dbContext.AuditEvents.CountAsync(audit => audit.Outcome == "Failed"));
    }

    [TestMethod]
    public async Task RagQuery_UnauthorizedRetrievedChunk_NeverReachesAnswerOrCitations()
    {
        using var dbContext = CreateDbContext();
        var owner = await AddUserAsync(dbContext, "evaluation-no-leak-tenant", RoleNames.User);
        var otherOwner = await AddUserAsync(dbContext, "evaluation-no-leak-tenant", RoleNames.User);
        var accessible = await AddDocumentAsync(dbContext, owner, "Allowed marker evidence.", "allowed.txt");
        var inaccessible = await AddDocumentAsync(dbContext, otherOwner, "Unauthorized secret marker.", "secret.txt");
        var answer = new RecordingAnswerGenerationService(new GroundedAnswerResult(
            "The authorized evidence is available [1].",
            [1],
            30,
            100));
        var service = CreateRagService(
            dbContext,
            owner,
            [ToSearchResult(inaccessible), ToSearchResult(accessible)],
            answer);

        var response = await service.QueryAsync("What evidence is available?");

        Assert.AreEqual(1, answer.ReceivedCitations.Count);
        Assert.AreEqual(accessible.Chunk.Id, answer.ReceivedCitations[0].ChunkId);
        Assert.IsFalse(answer.ReceivedCitations.Any(citation => citation.Excerpt.Contains("Unauthorized secret marker", StringComparison.Ordinal)));
        Assert.AreEqual(1, response.Citations.Count);
        Assert.AreEqual(accessible.Chunk.Id, response.Citations[0].ChunkId);
        Assert.IsFalse(response.Citations.Any(citation => citation.SourceName == "secret.txt"));

        var persistedMessages = await dbContext.ConversationMessages
            .AsNoTracking()
            .ToListAsync();
        var assistantMessage = persistedMessages.Single(message => message.Role == ConversationMessageRole.Assistant);
        var snapshots = await dbContext.ConversationCitations
            .AsNoTracking()
            .Where(citation => citation.ConversationMessageId == assistantMessage.Id)
            .ToListAsync();
        Assert.AreEqual(1, snapshots.Count);
        Assert.AreEqual(accessible.Chunk.Id, snapshots[0].ChunkId);
        Assert.IsFalse(snapshots.Any(citation => citation.Excerpt.Contains("Unauthorized secret marker", StringComparison.Ordinal)));
    }

    private static ApplicationDbContext CreateDbContext() =>
        new(TestDbContextOptions.Create());

    private static async Task<User> AddUserAsync(
        ApplicationDbContext dbContext,
        string tenantId,
        string roleName)
    {
        var user = PostgreSqlTestData.CreateUser(tenantId);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = roleName });
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private static async Task<(Document Document, DocumentChunk Chunk)> AddDocumentAsync(
        ApplicationDbContext dbContext,
        User owner,
        string content,
        string fileName)
    {
        var document = new Document
        {
            Title = fileName,
            Content = content,
            FileName = fileName,
            ContentType = "text/plain",
            FileSize = content.Length,
            TenantId = owner.TenantId,
            OwnerUserId = owner.Id
        };
        var chunk = new DocumentChunk
        {
            DocumentId = document.Id,
            ChunkIndex = 0,
            Content = content,
            CharacterCount = content.Length
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(chunk);
        await dbContext.SaveChangesAsync();
        return (document, chunk);
    }

    private static VectorSearchResult ToSearchResult((Document Document, DocumentChunk Chunk) source) => new()
    {
        DocumentId = source.Document.Id,
        ChunkId = source.Chunk.Id,
        ContentSnippet = source.Chunk.Content,
        SimilarityScore = 1
    };

    private static RagCitationService CreateCitationService(ApplicationDbContext dbContext, User currentUser)
    {
        var currentUserService = new EvaluationCurrentUserService(currentUser);
        var access = CreateDocumentAccessService(dbContext, currentUserService);
        return new RagCitationService(dbContext, access);
    }

    private static RagService CreateRagService(
        ApplicationDbContext dbContext,
        User currentUser,
        IReadOnlyList<VectorSearchResult> results,
        RecordingAnswerGenerationService answer)
    {
        var currentUserService = new EvaluationCurrentUserService(currentUser);
        var access = CreateDocumentAccessService(dbContext, currentUserService);
        var citation = new RagCitationService(dbContext, access);
        var conversation = new ConversationMessageService(
            dbContext,
            currentUserService,
            new AuditEventWriter(dbContext, new HttpContextAccessor()));
        return new RagService(
            new FixedVectorSearchService(results),
            new EmptyHybridSearchService(),
            Microsoft.Extensions.Options.Options.Create(new RagOptions { UseHybridSearch = false }),
            citation,
            new NoOpTelemetryService(),
            currentUserService,
            new AuditEventWriter(dbContext, new HttpContextAccessor()),
            answer,
            conversation,
            NullLogger<RagService>.Instance);
    }

    private static DocumentAccessService CreateDocumentAccessService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        var authorization = new ServiceCollection()
            .AddLogging()
            .AddAuthorization()
            .BuildServiceProvider()
            .GetRequiredService<IAuthorizationService>();
        return new DocumentAccessService(
            dbContext,
            authorization,
            currentUserService,
            new AuditEventWriter(dbContext, new HttpContextAccessor()));
    }

    private sealed class EvaluationCurrentUserService(User user) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? Principal => null;
        public string? EntraObjectId => user.EntraObjectId;
        public string? Email => user.Email;
        public string? TenantId => user.TenantId;
        public Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(user);
    }

    private sealed class FixedVectorSearchService(IReadOnlyList<VectorSearchResult> results) : IVectorSearchService
    {
        public Task<VectorSearchResponse> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null) =>
            Task.FromResult(new VectorSearchResponse { Results = results, RequestedTopK = topK });
    }

    private sealed class EmptyHybridSearchService : IHybridSearchService
    {
        public Task<IReadOnlyList<HybridSearchResult>> SearchAsync(
            string query,
            int topK = 5,
            CancellationToken cancellationToken = default,
            Guid? documentId = null) =>
            Task.FromResult<IReadOnlyList<HybridSearchResult>>(Array.Empty<HybridSearchResult>());
    }

    private sealed class RecordingAnswerGenerationService : IAnswerGenerationService
    {
        private readonly GroundedAnswerResult? _result;
        private readonly Exception? _error;

        public RecordingAnswerGenerationService(GroundedAnswerResult result) => _result = result;

        public RecordingAnswerGenerationService(Exception error) => _error = error;

        public IReadOnlyList<RagCitationDto> ReceivedCitations { get; private set; } = Array.Empty<RagCitationDto>();

        public Task<GroundedAnswerResult> GenerateAnswerAsync(
            string question,
            int retrievedChunkCount,
            IReadOnlyList<RagCitationDto> citations,
            IReadOnlyList<ConversationMessageResponse> conversationHistory,
            CancellationToken cancellationToken = default)
        {
            ReceivedCitations = citations;
            return _error is null
                ? Task.FromResult(_result!)
                : Task.FromException<GroundedAnswerResult>(_error);
        }
    }
}
