using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Pgvector;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class EmbeddingServiceImportTests
{
    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_ReturnsPersistedChunksWithoutUserOrEmbeddingGeneration()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        var chunks = new[]
        {
            CreateChunk(document.Id, 0, "First sentence."),
            CreateChunk(document.Id, 1, "Second sentence.")
        };
        await dbContext.Documents.AddAsync(document);
        dbContext.DocumentChunks.AddRange(chunks);
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: chunks.Length));
        await dbContext.SaveChangesAsync();
        var accessService = new FakeDocumentAccessService();
        var embeddingProvider = new FakeEmbeddingProvider();
        var service = CreateService(dbContext, accessService, embeddingProvider);

        var result = await service.GetValidatedChunksForImportAsync(document);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(chunks[0].Id, result[0].Id);
        Assert.AreEqual(chunks[1].Id, result[1].Id);
        Assert.AreEqual(0, accessService.GetAccessibleDocumentCallCount);
        Assert.AreEqual(0, accessService.GetRequiredCurrentUserCallCount);
        Assert.AreEqual(0, embeddingProvider.GenerationCallCount);
        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
    }

    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_RejectsDocumentNotInProcessingStatus()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        document.ImportStatus = DocumentImportStatus.Queued;
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GetValidatedChunksForImportAsync(document));

        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task GenerateAndPersistEmbeddingsForImportAsync_PersistsOnlyMissingAndReusesThemOnRedelivery()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        var firstChunk = CreateChunk(document.Id, 0, "First sentence.");
        var secondChunk = CreateChunk(document.Id, 1, "Second sentence.");
        var existingEmbedding = new DocumentEmbedding
        {
            ChunkId = firstChunk.Id,
            EmbeddingVector = new Vector(new[] { 0.3f, 0.4f }),
            VectorDimensions = 2,
            ModelName = "test-model"
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(firstChunk, secondChunk);
        dbContext.DocumentEmbeddings.Add(existingEmbedding);
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 2));
        await dbContext.SaveChangesAsync();
        var embeddingProvider = new FakeEmbeddingProvider();
        var telemetry = new FakeTelemetryService();
        var service = CreateService(
            dbContext,
            embeddingProvider: embeddingProvider,
            embeddingDimensions: 2,
            telemetry: telemetry);

        var firstResult = await service.GenerateAndPersistEmbeddingsForImportAsync(document);

        Assert.AreEqual(2, firstResult.Count);
        Assert.AreEqual(1, embeddingProvider.GenerationCallCount);
        Assert.AreEqual(2, await dbContext.DocumentEmbeddings.CountAsync());
        Assert.AreEqual(existingEmbedding.Id, firstResult[0].Id);
        Assert.AreEqual("test-model", firstResult[1].ModelName);
        Assert.AreEqual(2, firstResult[1].VectorDimensions);
        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        var successAudit = await dbContext.AuditEvents.SingleAsync(
            auditEvent => auditEvent.EventType == AuditEventTypes.DocumentEmbeddingsGenerated);
        Assert.AreEqual("Succeeded", successAudit.Outcome);
        Assert.AreEqual("test-model", successAudit.ModelDeployment);
        Assert.AreEqual("Test", successAudit.ModelProvider);
        Assert.AreEqual(1, successAudit.GeneratedEmbeddingCount);
        Assert.AreEqual(1, successAudit.ReusedEmbeddingCount);
        Assert.IsNotNull(successAudit.DurationMilliseconds);
        Assert.IsNull(successAudit.InputCharacterCount);
        Assert.AreEqual(1, telemetry.EmbeddingGenerationEvents.Count);
        Assert.IsTrue(telemetry.EmbeddingGenerationEvents[0].Success);
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents.Count);
        Assert.IsTrue(telemetry.EmbeddingBatchEvents[0].Success);
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents[0].GeneratedEmbeddingCount);
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents[0].ReusedEmbeddingCount);

        var secondResult = await service.GenerateAndPersistEmbeddingsForImportAsync(document);

        Assert.AreEqual(2, secondResult.Count);
        Assert.AreEqual(1, embeddingProvider.GenerationCallCount);
        Assert.AreEqual(2, await dbContext.DocumentEmbeddings.CountAsync());
        Assert.AreEqual(existingEmbedding.Id, secondResult[0].Id);
        Assert.AreEqual(firstResult[1].Id, secondResult[1].Id);
        CollectionAssert.AreEqual(new[] { 0.3f, 0.4f }, secondResult[0].EmbeddingVector.ToArray());
        Assert.AreEqual(2, await dbContext.AuditEvents.CountAsync());
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents.Count);
    }

    [TestMethod]
    public async Task GenerateAndPersistEmbeddingsForImportAsync_FailurePreservesChunksAndAllowsPartialSetRecovery()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        var firstChunk = CreateChunk(document.Id, 0, "First sentence.");
        var secondChunk = CreateChunk(document.Id, 1, "Second sentence.");
        var existingEmbedding = new DocumentEmbedding
        {
            ChunkId = firstChunk.Id,
            EmbeddingVector = new Vector(new[] { 0.3f, 0.4f }),
            VectorDimensions = 2,
            ModelName = "test-model"
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(firstChunk, secondChunk);
        dbContext.DocumentEmbeddings.Add(existingEmbedding);
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 2));
        await dbContext.SaveChangesAsync();
        var embeddingProvider = new FakeEmbeddingProvider { ContentToFailOn = "Second sentence." };
        var telemetry = new FakeTelemetryService();
        var service = CreateService(
            dbContext,
            embeddingProvider: embeddingProvider,
            embeddingDimensions: 2,
            telemetry: telemetry);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GenerateAndPersistEmbeddingsForImportAsync(document));

        Assert.AreEqual(1, await dbContext.DocumentEmbeddings.CountAsync());
        Assert.AreEqual(existingEmbedding.Id, (await dbContext.DocumentEmbeddings.SingleAsync()).Id);
        Assert.AreEqual(2, await dbContext.DocumentChunks.CountAsync());
        Assert.AreEqual(1, await dbContext.AuditEvents.CountAsync());
        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents.Count);
        Assert.IsFalse(telemetry.EmbeddingBatchEvents[0].Success);
        Assert.AreEqual(0, telemetry.EmbeddingBatchEvents[0].GeneratedEmbeddingCount);
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents[0].ReusedEmbeddingCount);
        Assert.IsFalse(telemetry.EmbeddingGenerationEvents[0].Success);

        embeddingProvider.ContentToFailOn = null;
        var recoveredResult = await service.GenerateAndPersistEmbeddingsForImportAsync(document);

        Assert.AreEqual(2, recoveredResult.Count);
        Assert.AreEqual(2, embeddingProvider.GenerationCallCount);
        Assert.AreEqual(2, await dbContext.DocumentEmbeddings.CountAsync());
        Assert.AreEqual(existingEmbedding.Id, recoveredResult[0].Id);
        Assert.AreEqual(2, await dbContext.DocumentChunks.CountAsync());
        Assert.AreEqual(2, await dbContext.AuditEvents.CountAsync());
        Assert.AreEqual(2, telemetry.EmbeddingBatchEvents.Count);
        Assert.IsTrue(telemetry.EmbeddingBatchEvents[1].Success);
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents[1].GeneratedEmbeddingCount);
        Assert.AreEqual(1, telemetry.EmbeddingBatchEvents[1].ReusedEmbeddingCount);
        Assert.AreEqual(2, await dbContext.AuditEvents.CountAsync());
    }

    [TestMethod]
    public async Task PrepareEmbeddingsForImportAsync_RejectsEmptyGeneratedVector()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(CreateChunk(document.Id, 0, "Persisted content."));
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 1));
        await dbContext.SaveChangesAsync();
        var embeddingProvider = new FakeEmbeddingProvider { VectorToGenerate = Array.Empty<float>() };
        var service = CreateService(dbContext, embeddingProvider: embeddingProvider, embeddingDimensions: 2);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.PrepareEmbeddingsForImportAsync(document));

        Assert.AreEqual(1, embeddingProvider.GenerationCallCount);
        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task PrepareEmbeddingsForImportAsync_GeneratesMissingVectorsWithoutPersistingThem()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        var chunks = new[]
        {
            CreateChunk(document.Id, 0, "First sentence."),
            CreateChunk(document.Id, 1, "Second sentence.")
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(chunks);
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: chunks.Length));
        await dbContext.SaveChangesAsync();
        var embeddingProvider = new FakeEmbeddingProvider();
        var service = CreateService(dbContext, embeddingProvider: embeddingProvider, embeddingDimensions: 2);

        var result = await service.PrepareEmbeddingsForImportAsync(document);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(2, embeddingProvider.GenerationCallCount);
        CollectionAssert.AreEqual(new[] { "First sentence.", "Second sentence." }, embeddingProvider.GeneratedContents);
        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
        Assert.IsTrue(result.All(embedding => embedding.ModelName == embeddingProvider.ModelName));
        Assert.IsTrue(result.All(embedding => embedding.VectorDimensions == 2));
        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
    }

    [TestMethod]
    public async Task PrepareEmbeddingsForImportAsync_ReusesExistingModelEmbeddingAndGeneratesOnlyMissing()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        var firstChunk = CreateChunk(document.Id, 0, "First sentence.");
        var secondChunk = CreateChunk(document.Id, 1, "Second sentence.");
        var existingEmbedding = new DocumentEmbedding
        {
            ChunkId = firstChunk.Id,
            EmbeddingVector = new Vector(new[] { 0.3f, 0.4f }),
            VectorDimensions = 2,
            ModelName = "test-model"
        };
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(firstChunk, secondChunk);
        dbContext.DocumentEmbeddings.Add(existingEmbedding);
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 2));
        await dbContext.SaveChangesAsync();
        var embeddingProvider = new FakeEmbeddingProvider();
        var service = CreateService(dbContext, embeddingProvider: embeddingProvider, embeddingDimensions: 2);

        var result = await service.PrepareEmbeddingsForImportAsync(document);

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(existingEmbedding.Id, result[0].Id);
        Assert.AreEqual(1, embeddingProvider.GenerationCallCount);
        CollectionAssert.AreEqual(new[] { "Second sentence." }, embeddingProvider.GeneratedContents);
        Assert.AreEqual(1, await dbContext.DocumentEmbeddings.CountAsync());
        Assert.AreEqual(2, result[1].VectorDimensions);
    }

    [TestMethod]
    public async Task PrepareEmbeddingsForImportAsync_RejectsStoredVectorWithInvalidDimensions()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        var chunk = CreateChunk(document.Id, 0, "Persisted content.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(chunk);
        dbContext.DocumentEmbeddings.Add(new DocumentEmbedding
        {
            ChunkId = chunk.Id,
            EmbeddingVector = new Vector(new[] { 0.3f, 0.4f }),
            VectorDimensions = 1,
            ModelName = "test-model"
        });
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 1));
        await dbContext.SaveChangesAsync();
        var embeddingProvider = new FakeEmbeddingProvider();
        var service = CreateService(dbContext, embeddingProvider: embeddingProvider, embeddingDimensions: 2);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.PrepareEmbeddingsForImportAsync(document));

        Assert.AreEqual(0, embeddingProvider.GenerationCallCount);
        Assert.AreEqual(1, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task PrepareEmbeddingsForImportAsync_RejectsGeneratedVectorWithWrongDimensions()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(CreateChunk(document.Id, 0, "Persisted content."));
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 1));
        await dbContext.SaveChangesAsync();
        var embeddingProvider = new FakeEmbeddingProvider { VectorToGenerate = [0.1f] };
        var service = CreateService(dbContext, embeddingProvider: embeddingProvider, embeddingDimensions: 2);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.PrepareEmbeddingsForImportAsync(document));

        Assert.AreEqual(1, embeddingProvider.GenerationCallCount);
        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_RejectsStaleValidatedDocument()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        document.Content = "Stale caller content.";
        var service = CreateService(dbContext);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.GetValidatedChunksForImportAsync(document));

        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_RejectsMissingPersistedChunks()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GetValidatedChunksForImportAsync(document));

        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_RejectsEmptyPersistedContent()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument(" \r\n ");
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => service.GetValidatedChunksForImportAsync(document));

        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_RejectsNonContiguousChunkIndices()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.AddRange(
            CreateChunk(document.Id, 0, "First sentence."),
            CreateChunk(document.Id, 2, "Second sentence."));
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 2));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.GetValidatedChunksForImportAsync(document));

        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_RejectsPartialChunkSet()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("First sentence. Second sentence.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(CreateChunk(document.Id, 0, "First sentence."));
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 2));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.GetValidatedChunksForImportAsync(document));

        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    [TestMethod]
    public async Task GetValidatedChunksForImportAsync_RejectsInconsistentChunkCharacterCount()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument("Persisted content.");
        dbContext.Documents.Add(document);
        dbContext.DocumentChunks.Add(CreateChunk(document.Id, 0, "Persisted content.", characterCount: 1));
        dbContext.AuditEvents.Add(CreateChunkAudit(document, chunkCount: 1));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            () => service.GetValidatedChunksForImportAsync(document));

        Assert.AreEqual(0, await dbContext.DocumentEmbeddings.CountAsync());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static EmbeddingService CreateService(
        ApplicationDbContext dbContext,
        FakeDocumentAccessService? accessService = null,
        FakeEmbeddingProvider? embeddingProvider = null,
        int embeddingDimensions = 2,
        FakeTelemetryService? telemetry = null)
    {
        return new EmbeddingService(
            dbContext,
            accessService ?? new FakeDocumentAccessService(),
            embeddingProvider ?? new FakeEmbeddingProvider(),
            new AuditEventWriter(dbContext, new HttpContextAccessor()),
            Microsoft.Extensions.Options.Options.Create(new FoundryOptions { EmbeddingDimensions = embeddingDimensions }),
            telemetry ?? new FakeTelemetryService(),
            NullLogger<EmbeddingService>.Instance);
    }

    private static Document CreateDocument(string content)
    {
        return new Document
        {
            Title = "Import embedding test document",
            Content = content,
            ImportStatus = DocumentImportStatus.Processing,
            OwnerUserId = Guid.NewGuid(),
            TenantId = "tenant-a",
            StagingBlobKey = "document/staging-key.pdf"
        };
    }

    private static DocumentChunk CreateChunk(
        Guid documentId,
        int chunkIndex,
        string content,
        int? characterCount = null)
    {
        return new DocumentChunk
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Content = content,
            CharacterCount = characterCount ?? content.Length
        };
    }

    private static AuditEvent CreateChunkAudit(Document document, int chunkCount)
    {
        return new AuditEvent
        {
            TenantId = document.TenantId,
            ActorUserId = document.OwnerUserId,
            EventType = AuditEventTypes.DocumentChunksGenerated,
            Outcome = "Succeeded",
            TargetType = "Document",
            TargetId = document.Id.ToString(),
            ResultCount = chunkCount
        };
    }

    private sealed class FakeDocumentAccessService : IDocumentAccessService
    {
        public int GetAccessibleDocumentCallCount { get; private set; }

        public int GetRequiredCurrentUserCallCount { get; private set; }

        public Task<User> GetRequiredCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            GetRequiredCurrentUserCallCount++;
            throw new NotSupportedException("Import chunk validation must not require a current user.");
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

    private sealed class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public string ProviderName => "Test";

        public string ModelName => "test-model";

        public int GenerationCallCount { get; private set; }

        public List<string> GeneratedContents { get; } = [];

        public IReadOnlyList<float> VectorToGenerate { get; set; } = [0.1f, 0.2f];

        public string? ContentToFailOn { get; set; }

        public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
            string content,
            string? modelName = null,
            CancellationToken cancellationToken = default)
        {
            GenerationCallCount++;
            GeneratedContents.Add(content);
            if (string.Equals(content, ContentToFailOn, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Configured embedding failure.");
            }

            return Task.FromResult(VectorToGenerate);
        }
    }

    private sealed class FakeTelemetryService : ITelemetryService
    {
        public List<EmbeddingBatchTelemetry> EmbeddingBatchEvents { get; } = [];

        public List<EmbeddingGenerationTelemetry> EmbeddingGenerationEvents { get; } = [];

        public void TrackApiRequest(string route, int statusCode, double durationMs, string method) { }

        public void TrackDatabaseQuery(string operation, double durationMs, bool success) { }

        public void TrackDocumentProcessing(string operation, double durationMs, bool success, int itemCount = 0) { }

        public void TrackDocumentChunking(double durationMs, bool success, int generatedChunkCount, int reusedChunkCount) { }

        public void TrackEmbeddingGeneration(string modelName, double durationMs, int vectorDimensions, bool success)
        {
            EmbeddingGenerationEvents.Add(new EmbeddingGenerationTelemetry(modelName, durationMs, vectorDimensions, success));
        }

        public void TrackEmbeddingBatch(
            string modelName,
            double durationMs,
            bool success,
            int generatedEmbeddingCount,
            int reusedEmbeddingCount)
        {
            EmbeddingBatchEvents.Add(new EmbeddingBatchTelemetry(
                modelName,
                durationMs,
                success,
                generatedEmbeddingCount,
                reusedEmbeddingCount));
        }

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

    private sealed record EmbeddingBatchTelemetry(
        string ModelName,
        double DurationMs,
        bool Success,
        int GeneratedEmbeddingCount,
        int ReusedEmbeddingCount);

    private sealed record EmbeddingGenerationTelemetry(
        string ModelName,
        double DurationMs,
        int VectorDimensions,
        bool Success);
}
