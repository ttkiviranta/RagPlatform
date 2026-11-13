using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class DocumentChunkingImportTests
{
    [TestMethod]
    public async Task GenerateChunksForImportAsync_UsesPersistedProcessingDocumentWithoutCurrentUser()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var accessService = new FakeDocumentAccessService();
        var telemetryService = new FakeTelemetryService();
        var chunkingService = new DocumentChunkingService(
            dbContext,
            accessService,
            new AuditEventWriter(dbContext, new HttpContextAccessor()),
            telemetryService,
            NullLogger<DocumentChunkingService>.Instance);

        var chunks = await chunkingService.GenerateChunksForImportAsync(document, chunkSize: 20, chunkOverlap: 0);

        Assert.AreEqual(2, chunks.Count);
        Assert.AreEqual(0, chunks[0].ChunkIndex);
        Assert.AreEqual(1, chunks[1].ChunkIndex);
        Assert.AreEqual(chunks[0].Content.Length, chunks[0].CharacterCount);
        Assert.AreEqual(chunks[1].Content.Length, chunks[1].CharacterCount);
        Assert.AreEqual(0, accessService.GetAccessibleDocumentCallCount);
        Assert.AreEqual(2, await dbContext.DocumentChunks.CountAsync());
        var auditEvent = await dbContext.AuditEvents.SingleAsync();
        Assert.AreEqual(AuditEventTypes.DocumentChunksGenerated, auditEvent.EventType);
        Assert.AreEqual("Succeeded", auditEvent.Outcome);
        Assert.AreEqual("Document", auditEvent.TargetType);
        Assert.AreEqual(document.Id.ToString(), auditEvent.TargetId);
        Assert.AreEqual(2, auditEvent.ResultCount);
        Assert.IsTrue(auditEvent.DurationMilliseconds.HasValue && auditEvent.DurationMilliseconds.Value >= 0);
        Assert.AreEqual(1, telemetryService.ChunkingEvents.Count);
        Assert.IsTrue(telemetryService.ChunkingEvents[0].Success);
        Assert.AreEqual(2, telemetryService.ChunkingEvents[0].GeneratedChunkCount);
        Assert.AreEqual(0, telemetryService.ChunkingEvents[0].ReusedChunkCount);
        Assert.IsTrue(telemetryService.ChunkingEvents[0].DurationMilliseconds >= 0);
    }

    [TestMethod]
    public async Task GenerateChunksForImportAsync_ReusesACompleteValidChunkSet()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        var firstChunk = CreateChunk(document.Id, 0, "First sentence.");
        var secondChunk = CreateChunk(document.Id, 1, "Second sentence.");
        await dbContext.Documents.AddAsync(document);
        dbContext.DocumentChunks.AddRange(firstChunk, secondChunk);
        await dbContext.SaveChangesAsync();
        var telemetryService = new FakeTelemetryService();
        var chunkingService = CreateChunkingService(
            dbContext,
            new FakeDocumentAccessService(),
            telemetryService: telemetryService);

        var chunks = await chunkingService.GenerateChunksForImportAsync(document, chunkSize: 20, chunkOverlap: 0);
        await chunkingService.GenerateChunksForImportAsync(document, chunkSize: 20, chunkOverlap: 0);

        Assert.AreEqual(2, chunks.Count);
        Assert.AreEqual(firstChunk.Id, chunks[0].Id);
        Assert.AreEqual(secondChunk.Id, chunks[1].Id);
        Assert.AreEqual(2, await dbContext.DocumentChunks.CountAsync());
        Assert.AreEqual(1, await dbContext.AuditEvents.CountAsync());
        Assert.AreEqual(2, telemetryService.ChunkingEvents.Count);
        Assert.IsTrue(telemetryService.ChunkingEvents.All(item => item.Success));
        Assert.IsTrue(telemetryService.ChunkingEvents.All(item => item.GeneratedChunkCount == 0));
        Assert.IsTrue(telemetryService.ChunkingEvents.All(item => item.ReusedChunkCount == 2));
    }

    [TestMethod]
    public async Task GenerateChunksForImportAsync_RejectsContiguousButPartialChunkSetWithoutRegenerating()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(CreateChunk(document.Id, 0, "First sentence."));
        await dbContext.SaveChangesAsync();
        var telemetryService = new FakeTelemetryService();
        var chunkingService = CreateChunkingService(
            dbContext,
            new FakeDocumentAccessService(),
            telemetryService: telemetryService);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => chunkingService.GenerateChunksForImportAsync(document, chunkSize: 20, chunkOverlap: 0));

        Assert.AreEqual(1, await dbContext.DocumentChunks.CountAsync());
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
        Assert.AreEqual(1, telemetryService.ChunkingEvents.Count);
        Assert.IsFalse(telemetryService.ChunkingEvents[0].Success);
    }

    [TestMethod]
    public async Task GenerateChunksForImportAsync_WhenAuditPersistenceFails_DoesNotLeaveChunksOrSuccessAuditTracked()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var telemetryService = new FakeTelemetryService();
        var chunkingService = CreateChunkingService(
            dbContext,
            new FakeDocumentAccessService(),
            new FailingAuditEventWriter(dbContext),
            telemetryService);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => chunkingService.GenerateChunksForImportAsync(document, chunkSize: 20, chunkOverlap: 0));
        await dbContext.SaveChangesAsync();

        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        Assert.AreEqual(0, await dbContext.DocumentChunks.CountAsync());
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
        Assert.AreEqual(1, telemetryService.ChunkingEvents.Count);
        Assert.IsFalse(telemetryService.ChunkingEvents[0].Success);
        Assert.AreEqual(2, telemetryService.ChunkingEvents[0].GeneratedChunkCount);
    }

    [TestMethod]
    public async Task GenerateChunksForImportAsync_RejectsChunkSetWithMissingIndex()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(
            CreateChunk(document.Id, 0, "First sentence."),
            CreateChunk(document.Id, 2, "Second sentence."));
        await dbContext.SaveChangesAsync();
        var chunkingService = CreateChunkingService(dbContext, new FakeDocumentAccessService());

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => chunkingService.GenerateChunksForImportAsync(document, chunkSize: 20, chunkOverlap: 0));

        Assert.AreEqual(2, await dbContext.DocumentChunks.CountAsync());
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
    }

    [TestMethod]
    public async Task GenerateChunksForImportAsync_RejectsInconsistentCharacterCount()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(
            CreateChunk(document.Id, 0, "First sentence.", characterCount: 1),
            CreateChunk(document.Id, 1, "Second sentence."));
        await dbContext.SaveChangesAsync();
        var chunkingService = CreateChunkingService(dbContext, new FakeDocumentAccessService());

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => chunkingService.GenerateChunksForImportAsync(document, chunkSize: 20, chunkOverlap: 0));

        Assert.AreEqual(2, await dbContext.DocumentChunks.CountAsync());
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
    }

    [TestMethod]
    public async Task GenerateChunksForImportAsync_RejectsDocumentNotInProcessingStatus()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        document.ImportStatus = DocumentImportStatus.Queued;
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var chunkingService = CreateChunkingService(dbContext, new FakeDocumentAccessService());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => chunkingService.GenerateChunksForImportAsync(document, chunkSize: 100, chunkOverlap: 10));

        Assert.AreEqual(0, await dbContext.DocumentChunks.CountAsync());
    }

    [TestMethod]
    public async Task GenerateChunksForImportAsync_RejectsEmptyPersistedContent()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument(" \r\n ");
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var chunkingService = CreateChunkingService(dbContext, new FakeDocumentAccessService());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => chunkingService.GenerateChunksForImportAsync(document, chunkSize: 100, chunkOverlap: 10));

        Assert.AreEqual(0, await dbContext.DocumentChunks.CountAsync());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DocumentChunkingService CreateChunkingService(
        ApplicationDbContext dbContext,
        FakeDocumentAccessService accessService,
        IAuditEventWriter? auditEventWriter = null,
        FakeTelemetryService? telemetryService = null)
    {
        return new DocumentChunkingService(
            dbContext,
            accessService,
            auditEventWriter ?? new AuditEventWriter(dbContext, new HttpContextAccessor()),
            telemetryService ?? new FakeTelemetryService(),
            NullLogger<DocumentChunkingService>.Instance);
    }

    private static Document CreateDocument(string content)
    {
        return new Document
        {
            Title = "Import test document",
            Content = content,
            ImportStatus = DocumentImportStatus.Processing,
            OwnerUserId = Guid.NewGuid(),
            TenantId = "tenant-a",
            StagingBlobKey = "document/staging-key.pdf"
        };
    }

    private static DocumentChunk CreateChunk(Guid documentId, int chunkIndex, string content, int? characterCount = null)
    {
        return new DocumentChunk
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Content = content,
            CharacterCount = characterCount ?? content.Length
        };
    }

    private sealed class FailingAuditEventWriter(ApplicationDbContext dbContext) : IAuditEventWriter
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
            int? outputTokenCount = null)
        {
            dbContext.AuditEvents.Add(new AuditEvent
            {
                TenantId = tenantId,
                ActorUserId = actorUserId,
                EventType = eventType,
                Outcome = outcome,
                TargetType = targetType,
                TargetId = targetId,
                DurationMilliseconds = durationMilliseconds,
                ResultCount = resultCount
            });
        }

        public Task FlushAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated audit persistence failure.");
        }
    }

    private sealed class FakeTelemetryService : ITelemetryService
    {
        public List<ChunkingTelemetryEvent> ChunkingEvents { get; } = [];

        public void TrackDocumentChunking(
            double durationMs,
            bool success,
            int generatedChunkCount,
            int reusedChunkCount)
        {
            ChunkingEvents.Add(new ChunkingTelemetryEvent(
                durationMs,
                success,
                generatedChunkCount,
                reusedChunkCount));
        }

        public void TrackApiRequest(string route, int statusCode, double durationMs, string method) { }

        public void TrackDatabaseQuery(string operation, double durationMs, bool success) { }

        public void TrackDocumentProcessing(string operation, double durationMs, bool success, int itemCount = 0) { }

        public void TrackEmbeddingGeneration(string modelName, double durationMs, int vectorDimensions, bool success) { }

        public void TrackVectorSearch(
            string modelName,
            int topK,
            int resultCount,
            double averageSimilarity,
            double durationMs,
            bool success) { }

        public void TrackRagQuery(
            int inputCharacterCount,
            int topK,
            int retrievedChunkCount,
            int contextLength,
            int promptLength,
            double durationMs,
            bool success) { }
    }

    private sealed record ChunkingTelemetryEvent(
        double DurationMilliseconds,
        bool Success,
        int GeneratedChunkCount,
        int ReusedChunkCount);

    private sealed class FakeDocumentAccessService : IDocumentAccessService
    {
        public int GetAccessibleDocumentCallCount { get; private set; }

        public Task<User> GetRequiredCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Document?> GetAccessibleDocumentAsync(
            Guid documentId,
            DocumentPermission permission,
            CancellationToken cancellationToken = default)
        {
            GetAccessibleDocumentCallCount++;
            return Task.FromResult<Document?>(null);
        }

        public IQueryable<Document> ScopeDocuments(IQueryable<Document> documents, User user)
        {
            throw new NotSupportedException();
        }
    }
}