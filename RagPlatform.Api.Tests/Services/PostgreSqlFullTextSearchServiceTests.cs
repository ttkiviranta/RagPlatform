using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Search;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("PostgreSql")]
[DoNotParallelize]
public sealed class PostgreSqlFullTextSearchServiceTests : PostgreSqlTestClassBase
{
    [TestMethod]
    public async Task SearchAsync_QuotedPhrase_ReturnsExactPhraseMatches()
    {
        using var dbContext = CreateDbContext();
        var expectedContent = "quartz xylophone retrieval";
        await CreateDocumentWithChunksAsync(dbContext, "full-text-search-test", [
            expectedContent,
            "quartz retrieval xylophone"
        ]);
        var service = CreateService(dbContext, CreateSearchUser("full-text-search-test", RoleNames.PowerUser));

        var results = await service.SearchAsync("\"quartz xylophone\"");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(expectedContent, results[0].Content);
    }

    [TestMethod]
    public async Task SearchAsync_EnglishStemming_MatchesInflectedWords()
    {
        using var dbContext = CreateDbContext();
        var expectedContent = "The engineers are running the search";
        await CreateDocumentWithChunksAsync(dbContext, "full-text-search-test", [expectedContent]);
        var service = CreateService(dbContext, CreateSearchUser("full-text-search-test", RoleNames.PowerUser));

        var results = await service.SearchAsync("run");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(expectedContent, results[0].Content);
    }

    [TestMethod]
    public async Task SearchAsync_UserRole_FiltersByTenantAndOwner()
    {
        using var dbContext = CreateDbContext();
        const string tenantId = "full-text-search-test";
        var currentOwner = PostgreSqlTestData.CreateUser(tenantId);
        var otherTenantOwner = PostgreSqlTestData.CreateUser("other-tenant");
        var otherOwner = PostgreSqlTestData.CreateUser(tenantId);
        dbContext.Users.AddRange(currentOwner, otherTenantOwner, otherOwner);
        CreateOwnedDocument(dbContext, currentOwner, tenantId, "shared accessmarker");
        CreateOwnedDocument(dbContext, otherOwner, tenantId, "shared accessmarker");
        CreateOwnedDocument(dbContext, otherTenantOwner, "other-tenant", "shared accessmarker");
        await dbContext.SaveChangesAsync();
        var service = CreateService(
            dbContext,
            CreateSearchUser(tenantId, RoleNames.User, currentOwner.Id));

        var results = await service.SearchAsync("accessmarker");

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(currentOwner.Id, await dbContext.Documents
            .Where(document => document.Id == results[0].DocumentId)
            .Select(document => document.OwnerUserId)
            .SingleAsync());
    }

    [TestMethod]
    public async Task SearchAsync_DocumentId_ReturnsOnlyChunksFromRequestedDocument()
    {
        using var dbContext = CreateDbContext();
        var firstDocument = await CreateDocumentWithChunksAsync(
            dbContext,
            "full-text-search-test",
            ["shared documentfilter marker"]);
        var secondDocument = await CreateDocumentWithChunksAsync(
            dbContext,
            "full-text-search-test",
            ["shared documentfilter marker"]);
        var service = CreateService(dbContext, CreateSearchUser("full-text-search-test", RoleNames.PowerUser));

        var results = await service.SearchAsync("documentfilter", documentId: secondDocument.Id);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(secondDocument.Id, results[0].DocumentId);
        Assert.AreNotEqual(firstDocument.Id, results[0].DocumentId);
    }

    [TestMethod]
    public async Task SearchAsync_MultipleMatches_OrdersByRankScore()
    {
        using var dbContext = CreateDbContext();
        var lowerRankContent = "quartz xylophone";
        var higherRankContent = "quartz xylophone quartz xylophone quartz xylophone";
        await CreateDocumentWithChunksAsync(
            dbContext,
            "full-text-search-test",
            [lowerRankContent, higherRankContent]);
        var service = CreateService(dbContext, CreateSearchUser("full-text-search-test", RoleNames.PowerUser));

        var results = await service.SearchAsync("quartz xylophone");

        Assert.AreEqual(2, results.Count);
        Assert.AreEqual(higherRankContent, results[0].Content);
        Assert.IsGreaterThan(results[1].RankScore, results[0].RankScore);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(TestDbContextOptions.Create());
    }

    private static FullTextSearchService CreateService(ApplicationDbContext dbContext, User currentUser)
    {
        return new FullTextSearchService(
            dbContext,
            new FakeDocumentAccessService(currentUser),
            NullLogger<FullTextSearchService>.Instance);
    }

    private static User CreateSearchUser(string tenantId, string roleName, Guid? id = null)
    {
        var user = new User
        {
            Id = id ?? Guid.NewGuid(),
            TenantId = tenantId
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleName = roleName });
        return user;
    }

    private static async Task<Document> CreateDocumentWithChunksAsync(
        ApplicationDbContext dbContext,
        string tenantId,
        IReadOnlyList<string> contents)
    {
        var document = new Document
        {
            Title = "Full-text search test document",
            Content = string.Join(" ", contents),
            FileName = "full-text-search.txt",
            ContentType = "text/plain",
            FileSize = contents.Sum(content => content.Length),
            TenantId = tenantId
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(contents.Select((content, index) => new DocumentChunk
        {
            DocumentId = document.Id,
            ChunkIndex = index,
            Content = content,
            CharacterCount = content.Length
        }));
        await dbContext.SaveChangesAsync();
        return document;
    }

    private static Document CreateOwnedDocument(
        ApplicationDbContext dbContext,
        User owner,
        string tenantId,
        string content)
    {
        var document = new Document
        {
            Title = "Full-text search access test document",
            Content = content,
            FileName = "full-text-search-access.txt",
            ContentType = "text/plain",
            FileSize = content.Length,
            TenantId = tenantId,
            OwnerUserId = owner.Id
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(new DocumentChunk
        {
            DocumentId = document.Id,
            Content = content,
            CharacterCount = content.Length
        });
        return document;
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