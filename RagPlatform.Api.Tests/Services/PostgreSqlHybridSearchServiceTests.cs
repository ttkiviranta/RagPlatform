using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pgvector;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("PostgreSql")]
[DoNotParallelize]
public sealed class PostgreSqlHybridSearchServiceTests : PostgreSqlTestClassBase
{
    private const string TenantId = "hybrid-search-test";
    private const string EmbeddingModelName = "hybrid-search-test-model";

    [TestMethod]
    public async Task SearchAsync_PostgreSqlCandidates_OrdersByRrfScore()
    {
        using var dbContext = CreateDbContext();
        var fixture = await SeedCandidatesAsync(dbContext);
        var service = CreateService(dbContext);

        var results = await service.SearchAsync("hybridmarker", topK: 3);

        Assert.AreEqual(3, results.Count);
        CollectionAssert.AreEqual(
            new[] { fixture.OverlapChunkId, fixture.FullTextOnlyChunkId, fixture.VectorOnlyChunkId },
            results.Select(result => result.ChunkId).ToArray());
        Assert.IsTrue(results[0].FoundByVectorSearch);
        Assert.IsTrue(results[0].FoundByFullTextSearch);
        Assert.IsFalse(results[1].FoundByVectorSearch);
        Assert.IsTrue(results[1].FoundByFullTextSearch);
        Assert.IsTrue(results[2].FoundByVectorSearch);
        Assert.IsFalse(results[2].FoundByFullTextSearch);
        Assert.AreEqual("hybridmarker overlap full text", results[0].Content);
        Assert.AreEqual(3, results.Select(result => result.ChunkId).Distinct().Count());
    }

    [TestMethod]
    public async Task SearchAsync_DocumentIdFilter_IsAppliedToBothCandidateSearches()
    {
        using var dbContext = CreateDbContext();
        var fixture = await SeedCandidatesAsync(dbContext);
        var service = CreateService(dbContext);

        var results = await service.SearchAsync("hybridmarker", topK: 3, documentId: fixture.OverlapDocumentId);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(fixture.OverlapChunkId, results[0].ChunkId);
        Assert.AreEqual(fixture.OverlapDocumentId, results[0].DocumentId);
        Assert.IsTrue(results[0].FoundByVectorSearch);
        Assert.IsTrue(results[0].FoundByFullTextSearch);
    }

    [TestMethod]
    public async Task SearchAsync_UserScope_FiltersByTenantAndOwner()
    {
        using var dbContext = CreateDbContext();
        var currentOwner = PostgreSqlTestData.CreateUser(TenantId);
        var otherOwner = PostgreSqlTestData.CreateUser(TenantId);
        var otherTenantOwner = PostgreSqlTestData.CreateUser("other-tenant");
        dbContext.Users.AddRange(currentOwner, otherOwner, otherTenantOwner);
        await dbContext.SaveChangesAsync();

        var allowed = await AddCandidateAsync(
            dbContext,
            "accessmarker owned document",
            new[] { 1f, 0f },
            ownerUserId: currentOwner.Id);
        await AddCandidateAsync(
            dbContext,
            "accessmarker other owner document",
            new[] { 0.9f, 0.1f },
            ownerUserId: otherOwner.Id);
        await AddCandidateAsync(
            dbContext,
            "accessmarker other tenant document",
            new[] { 0.8f, 0.6f },
            tenantId: otherTenantOwner.TenantId,
            ownerUserId: otherTenantOwner.Id);

        var currentUser = new User { Id = currentOwner.Id, TenantId = TenantId };
        currentUser.UserRoles.Add(new UserRole { UserId = currentUser.Id, RoleName = RoleNames.User });
        var service = CreateService(dbContext, currentUser);

        var results = await service.SearchAsync("accessmarker", topK: 3);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(allowed.ChunkId, results[0].ChunkId);
        Assert.AreEqual(allowed.DocumentId, results[0].DocumentId);
        Assert.IsTrue(results[0].FoundByVectorSearch);
        Assert.IsTrue(results[0].FoundByFullTextSearch);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(TestDbContextOptions.Create());
    }

    private static HybridSearchService CreateService(ApplicationDbContext dbContext, User? currentUser = null)
    {
        currentUser ??= new User { TenantId = TenantId };
        if (currentUser.UserRoles.Count == 0)
        {
            currentUser.UserRoles.Add(new UserRole
            {
                UserId = currentUser.Id,
                RoleName = RoleNames.PowerUser
            });
        }

        var documentAccessService = new FakeDocumentAccessService(currentUser);
        var vectorSearchService = new VectorSearchService(
            dbContext,
            documentAccessService,
            new FixedEmbeddingProvider(),
            new NoOpTelemetryService(),
            new NoOpAuditEventWriter(),
            NullLogger<VectorSearchService>.Instance,
            Microsoft.Extensions.Options.Options.Create(new FoundryOptions { EmbeddingDimensions = 2 }));
        var fullTextSearchService = new FullTextSearchService(
            dbContext,
            documentAccessService,
            NullLogger<FullTextSearchService>.Instance);

        return new HybridSearchService(
            vectorSearchService,
            fullTextSearchService,
            NullLogger<HybridSearchService>.Instance);
    }

    private static async Task<CandidateFixture> SeedCandidatesAsync(ApplicationDbContext dbContext)
    {
        var overlap = await AddCandidateAsync(
            dbContext,
            "hybridmarker overlap full text",
            new[] { 1f, 0f },
            chunkId: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var vectorOnly = await AddCandidateAsync(
            dbContext,
            "vector only candidate",
            new[] { 0.8f, 0.6f },
            chunkId: Guid.Parse("00000000-0000-0000-0000-000000000003"));
        var fullTextOnly = await AddCandidateAsync(
            dbContext,
            "hybridmarker full text only",
            embedding: null,
            chunkId: Guid.Parse("00000000-0000-0000-0000-000000000001"));

        return new CandidateFixture(
            overlap.ChunkId,
            overlap.DocumentId,
            vectorOnly.ChunkId,
            fullTextOnly.ChunkId);
    }

    private static async Task<(Guid ChunkId, Guid DocumentId)> AddCandidateAsync(
        ApplicationDbContext dbContext,
        string content,
        float[]? embedding,
        Guid? chunkId = null,
        string tenantId = TenantId,
        Guid? ownerUserId = null)
    {
        var document = new Document
        {
            Title = "Hybrid search test document",
            Content = content,
            FileName = "hybrid-search.txt",
            ContentType = "text/plain",
            FileSize = content.Length,
            TenantId = tenantId,
            OwnerUserId = ownerUserId
        };
        var chunk = new DocumentChunk
        {
            Id = chunkId ?? Guid.NewGuid(),
            DocumentId = document.Id,
            Content = content,
            CharacterCount = content.Length
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(chunk);
        if (embedding is not null)
        {
            dbContext.DocumentEmbeddings.Add(new DocumentEmbedding
            {
                ChunkId = chunk.Id,
                EmbeddingVector = new Vector(embedding),
                VectorDimensions = embedding.Length,
                ModelName = EmbeddingModelName
            });
        }

        await dbContext.SaveChangesAsync();
        return (chunk.Id, document.Id);
    }

    private sealed record CandidateFixture(
        Guid OverlapChunkId,
        Guid OverlapDocumentId,
        Guid VectorOnlyChunkId,
        Guid FullTextOnlyChunkId);

    private sealed class FixedEmbeddingProvider : IEmbeddingProvider
    {
        public string ProviderName => "test";

        public string ModelName => EmbeddingModelName;

        public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
            string content,
            string? modelName = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float>>(new[] { 1f, 0f });
        }
    }

    private sealed class NoOpAuditEventWriter : IAuditEventWriter
    {
        public void Add(
            Guid? actorUserId,
            string tenantId,
            string eventType,
            string outcome,
            string? targetType = null,
            string? targetId = null,
            double? durationMilliseconds = null,
            int? inputCharacterCount = null,
            int? requestedTopK = null,
            int? resultCount = null,
            double? averageSimilarity = null,
            int? contextCharacterCount = null,
            int? promptCharacterCount = null,
            string? modelProvider = null,
            string? modelDeployment = null,
            string? modelOperation = null,
            int? inputTokenCount = null,
            int? outputTokenCount = null,
            int? generatedEmbeddingCount = null,
            int? reusedEmbeddingCount = null)
        {
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDocumentAccessService(User currentUser) : IDocumentAccessService
    {
        public Task<User> GetRequiredCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(currentUser);
        }

        public Task<Document?> GetAccessibleDocumentAsync(
            Guid documentId,
            DocumentPermission permission,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public IQueryable<Document> ScopeDocuments(IQueryable<Document> documents, User user)
        {
            var canManageTenant = RoleHierarchy.CanManageTenantDocuments(user);
            var canAccessOwned = RoleHierarchy.CanAccessOwnedDocuments(user);

            return documents.Where(document =>
                document.TenantId == user.TenantId
                && (canManageTenant || (canAccessOwned && document.OwnerUserId == user.Id)));
        }
    }
}