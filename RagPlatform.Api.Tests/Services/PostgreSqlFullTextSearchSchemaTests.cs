using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("PostgreSql")]
[DoNotParallelize]
public sealed class PostgreSqlFullTextSearchSchemaTests : PostgreSqlTestClassBase
{
    [TestMethod]
    public async Task Migration_CreatesGeneratedSearchVectorColumn()
    {
        using var dbContext = CreateDbContext();

        var columnExists = await dbContext.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                    AND table_name = 'DocumentChunks'
                    AND column_name = 'SearchVector'
                    AND udt_name = 'tsvector'
                    AND is_generated = 'ALWAYS'
            ) AS "Value"
            """).SingleAsync();

        Assert.IsTrue(columnExists);
    }

    [TestMethod]
    public async Task SearchVector_IsGeneratedFromChunkContent()
    {
        using var dbContext = CreateDbContext();
        var chunk = await CreateChunkAsync(dbContext, "quartz xylophone");

        var searchVector = await GetSearchVectorTextAsync(dbContext, chunk.Id);

        Assert.Contains("quartz", searchVector);
        Assert.Contains("xylophon", searchVector);
    }

    [TestMethod]
    public async Task SearchVector_UpdatesWhenChunkContentChanges()
    {
        using var dbContext = CreateDbContext();
        var chunk = await CreateChunkAsync(dbContext, "quartz");

        chunk.Content = "xylophone";
        await dbContext.SaveChangesAsync();

        var searchVector = await GetSearchVectorTextAsync(dbContext, chunk.Id);

        Assert.Contains("xylophon", searchVector);
        Assert.DoesNotContain("quartz", searchVector);
    }

    [TestMethod]
    public async Task Migration_CreatesGinIndexForSearchVector()
    {
        using var dbContext = CreateDbContext();

        var indexExists = await dbContext.Database.SqlQueryRaw<bool>("""
            SELECT EXISTS (
                SELECT 1
                FROM pg_indexes
                WHERE schemaname = current_schema()
                    AND tablename = 'DocumentChunks'
                    AND indexname = 'IX_DocumentChunks_SearchVector'
                    AND indexdef ILIKE '%USING gin%'
            ) AS "Value"
            """).SingleAsync();

        Assert.IsTrue(indexExists);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(TestDbContextOptions.Create());
    }

    private static async Task<DocumentChunk> CreateChunkAsync(
        ApplicationDbContext dbContext,
        string content)
    {
        var document = new Document
        {
            Title = "Full-text search test document",
            Content = content,
            FileName = "full-text-search.txt",
            ContentType = "text/plain",
            FileSize = content.Length,
            TenantId = "full-text-search-test"
        };
        var chunk = new DocumentChunk
        {
            DocumentId = document.Id,
            Content = content,
            CharacterCount = content.Length
        };

        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(chunk);
        await dbContext.SaveChangesAsync();
        return chunk;
    }

    private static Task<string> GetSearchVectorTextAsync(
        ApplicationDbContext dbContext,
        Guid chunkId)
    {
        return dbContext.Database.SqlQueryRaw<string>("""
            SELECT "SearchVector"::text AS "Value"
            FROM "DocumentChunks"
            WHERE "Id" = {0}
            """, chunkId).SingleAsync();
    }
}