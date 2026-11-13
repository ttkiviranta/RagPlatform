using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class DocumentImportProcessorTests
{
    [TestMethod]
    public async Task ProcessAsync_PersistsExtractedTextAndSkipsRedelivery()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore();
        var extractor = new FakePdfTextExtractor(new PdfTextExtractionResult("Extracted PDF text", 2));
        var telemetry = new FakeTelemetryService();
        var chunkingService = new FakeDocumentChunkingService();
        var processor = CreateProcessor(dbContext, blobStore, extractor, telemetry, chunkingService);
        var originalUpdatedAt = document.UpdatedAt;

        await processor.ProcessAsync(document);
        var updatedAt = document.UpdatedAt;
        await processor.ProcessAsync(document);

        Assert.AreEqual("Extracted PDF text", document.Content);
        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        Assert.AreNotEqual(originalUpdatedAt, updatedAt);
        Assert.AreEqual(updatedAt, document.UpdatedAt);
        Assert.AreEqual(1, blobStore.DownloadCount);
        Assert.AreEqual(1, extractor.ExtractionCount);
        Assert.AreEqual(2, chunkingService.ImportChunkingCallCount);
        Assert.AreEqual(1000, chunkingService.LastChunkSize);
        Assert.AreEqual(150, chunkingService.LastChunkOverlap);
        var successAudit = await dbContext.AuditEvents.SingleAsync();
        Assert.AreEqual(AuditEventTypes.DocumentImportExtractionSucceeded, successAudit.EventType);
        Assert.AreEqual("Succeeded", successAudit.Outcome);
        Assert.AreEqual(4, telemetry.Events.Count);
        Assert.AreEqual("StagingBlobDownload", telemetry.Events[0].Operation);
        Assert.IsTrue(telemetry.Events[0].Success);
        Assert.AreEqual("PdfTextExtraction", telemetry.Events[1].Operation);
        Assert.IsTrue(telemetry.Events[1].Success);
        Assert.AreEqual("StagingBlobCleanup", telemetry.Events[2].Operation);
        Assert.IsTrue(telemetry.Events[2].Success);
        Assert.AreEqual("StagingBlobCleanup", telemetry.Events[3].Operation);
        Assert.IsTrue(telemetry.Events[3].Success);
        Assert.AreEqual(2, blobStore.DeleteCount);
        Assert.AreEqual("document/staging-key.pdf", document.StagingBlobKey);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenExtractionFails_DoesNotPersistContentOrChangeStatus()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore();
        var telemetry = new FakeTelemetryService();
        var processor = CreateProcessor(
            dbContext,
            blobStore,
            new FakePdfTextExtractor(new InvalidDataException("PDF extraction failed.")),
            telemetry);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => processor.ProcessAsync(document));

        Assert.AreEqual(string.Empty, document.Content);
        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        Assert.AreEqual(1, blobStore.DownloadCount);
        Assert.AreEqual(0, blobStore.DeleteCount);
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
        Assert.IsFalse(telemetry.Events[1].Success);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenDocumentIsAlreadyExtracted_DoesNotDownloadOrExtractAgain()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        document.Content = "Previously extracted text";
        await dbContext.Documents.AddAsync(document);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = document.TenantId,
            ActorUserId = document.OwnerUserId,
            EventType = AuditEventTypes.DocumentImportExtractionSucceeded,
            Outcome = "Succeeded",
            TargetType = "Document",
            TargetId = document.Id.ToString()
        });
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore();
        var extractor = new FakePdfTextExtractor(new PdfTextExtractionResult("Unused", 1));
        var telemetry = new FakeTelemetryService();
        var chunkingService = new FakeDocumentChunkingService();
        var processor = CreateProcessor(dbContext, blobStore, extractor, telemetry, chunkingService);

        await processor.ProcessAsync(document);

        Assert.AreEqual("Previously extracted text", document.Content);
        Assert.AreEqual(0, blobStore.DownloadCount);
        Assert.AreEqual(0, extractor.ExtractionCount);
        Assert.AreEqual(1, blobStore.DeleteCount);
        Assert.AreEqual("StagingBlobCleanup", telemetry.Events[0].Operation);
        Assert.AreEqual(1, chunkingService.ImportChunkingCallCount);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenDocumentIsNotProcessing_RejectsBeforeDownload()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        document.ImportStatus = DocumentImportStatus.Queued;
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore();
        var extractor = new FakePdfTextExtractor(new PdfTextExtractionResult("Unused", 1));
        var processor = CreateProcessor(dbContext, blobStore, extractor, new FakeTelemetryService());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => processor.ProcessAsync(document));

        Assert.AreEqual(0, blobStore.DownloadCount);
        Assert.AreEqual(0, extractor.ExtractionCount);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenExtractionHasPermanentFailure_MarksFailedAndAuditsOnce()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore();
        var extractor = new FakePdfTextExtractor(
            new PermanentPdfFailureException(PdfExtractionFailureKind.Corrupt));
        var telemetry = new FakeTelemetryService();
        var processor = CreateProcessor(dbContext, blobStore, extractor, telemetry);

        await Assert.ThrowsExactlyAsync<PermanentPdfFailureException>(() => processor.ProcessAsync(document));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => processor.ProcessAsync(document));

        Assert.AreEqual(DocumentImportStatus.Failed, document.ImportStatus);
        Assert.AreEqual(string.Empty, document.Content);
        Assert.AreEqual(1, blobStore.DownloadCount);
        Assert.AreEqual(1, extractor.ExtractionCount);
        Assert.AreEqual(0, blobStore.DeleteCount);
        var failureAudit = await dbContext.AuditEvents.SingleAsync();
        Assert.AreEqual(AuditEventTypes.DocumentImportPermanentPdfFailure, failureAudit.EventType);
        Assert.AreEqual("Failed", failureAudit.Outcome);
        Assert.AreEqual(2, telemetry.Events.Count);
        Assert.IsFalse(telemetry.Events[1].Success);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenChunkingReturnsNoChunks_RejectsWithoutChangingStatus()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        document.Content = "Persisted text";
        await dbContext.Documents.AddAsync(document);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = document.TenantId,
            ActorUserId = document.OwnerUserId,
            EventType = AuditEventTypes.DocumentImportExtractionSucceeded,
            Outcome = "Succeeded",
            TargetType = "Document",
            TargetId = document.Id.ToString()
        });
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore();
        var extractor = new FakePdfTextExtractor(new PdfTextExtractionResult("Unused", 1));
        var chunkingService = new FakeDocumentChunkingService([]);
        var processor = CreateProcessor(
            dbContext,
            blobStore,
            extractor,
            new FakeTelemetryService(),
            chunkingService);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => processor.ProcessAsync(document));

        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        Assert.AreEqual(0, blobStore.DownloadCount);
        Assert.AreEqual(0, extractor.ExtractionCount);
        Assert.AreEqual(1, chunkingService.ImportChunkingCallCount);
        Assert.AreEqual(0, await dbContext.DocumentChunks.CountAsync());
    }

    [TestMethod]
    public async Task ProcessAsync_WhenBlobDownloadFails_LeavesStatusProcessingAndTracksFailure()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore(new IOException("Blob download failed."));
        var extractor = new FakePdfTextExtractor(new PdfTextExtractionResult("Unused", 1));
        var telemetry = new FakeTelemetryService();
        var processor = CreateProcessor(dbContext, blobStore, extractor, telemetry);

        await Assert.ThrowsExactlyAsync<IOException>(() => processor.ProcessAsync(document));

        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        Assert.AreEqual(string.Empty, document.Content);
        Assert.AreEqual(0, extractor.ExtractionCount);
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
        Assert.AreEqual(1, telemetry.Events.Count);
        Assert.AreEqual("StagingBlobDownload", telemetry.Events[0].Operation);
        Assert.IsFalse(telemetry.Events[0].Success);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenBlobDeletionFails_RedeliveryRetriesCleanupWithoutReextracting()
    {
        var databaseName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(databaseName);
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore(deleteException: new IOException("Blob deletion failed."));
        var extractor = new FakePdfTextExtractor(new PdfTextExtractionResult("Persisted text", 1));
        var telemetry = new FakeTelemetryService();
        var processor = CreateProcessor(dbContext, blobStore, extractor, telemetry);

        await Assert.ThrowsExactlyAsync<IOException>(() => processor.ProcessAsync(document));

        using (var verificationContext = CreateDbContext(databaseName))
        {
            var persistedDocument = await verificationContext.Documents.SingleAsync();
            Assert.AreEqual("Persisted text", persistedDocument.Content);
            Assert.AreEqual(DocumentImportStatus.Processing, persistedDocument.ImportStatus);
            Assert.AreEqual("document/staging-key.pdf", persistedDocument.StagingBlobKey);
            var successAudit = await verificationContext.AuditEvents.SingleAsync();
            Assert.AreEqual(AuditEventTypes.DocumentImportExtractionSucceeded, successAudit.EventType);
        }

        using (var redeliveryContext = CreateDbContext(databaseName))
        {
            var redeliveredDocument = await redeliveryContext.Documents.SingleAsync();
            var redeliveryProcessor = CreateProcessor(redeliveryContext, blobStore, extractor, telemetry);
            await redeliveryProcessor.ProcessAsync(redeliveredDocument);
            Assert.AreEqual(1, await redeliveryContext.AuditEvents.CountAsync());
        }

        Assert.AreEqual(1, blobStore.DownloadCount);
        Assert.AreEqual(1, extractor.ExtractionCount);
        Assert.AreEqual(2, blobStore.DeleteCount);
        Assert.AreEqual(4, telemetry.Events.Count);
        Assert.AreEqual("StagingBlobCleanup", telemetry.Events[2].Operation);
        Assert.IsFalse(telemetry.Events[2].Success);
        Assert.AreEqual("StagingBlobCleanup", telemetry.Events[3].Operation);
        Assert.IsTrue(telemetry.Events[3].Success);
    }

    [TestMethod]
    public async Task ProcessAsync_WhenExtractionIsCanceled_LeavesStatusProcessingWithoutAudit()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var blobStore = new FakeStagingBlobStore();
        var telemetry = new FakeTelemetryService();
        var processor = CreateProcessor(
            dbContext,
            blobStore,
            new FakePdfTextExtractor(new OperationCanceledException()),
            telemetry);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => processor.ProcessAsync(document));

        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
        Assert.AreEqual(string.Empty, document.Content);
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
        Assert.IsFalse(telemetry.Events[1].Success);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return CreateDbContext(Guid.NewGuid().ToString());
    }

    private static ApplicationDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DocumentImportProcessor CreateProcessor(
        ApplicationDbContext dbContext,
        FakeStagingBlobStore blobStore,
        FakePdfTextExtractor extractor,
        FakeTelemetryService telemetry,
        FakeDocumentChunkingService? chunkingService = null)
    {
        return new DocumentImportProcessor(
            dbContext,
            blobStore,
            extractor,
            chunkingService ?? new FakeDocumentChunkingService(),
            new AuditEventWriter(dbContext, new HttpContextAccessor()),
            telemetry,
            NullLogger<DocumentImportProcessor>.Instance);
    }

    private static Document CreateDocument()
    {
        return new Document
        {
            Title = "Test document",
            Content = string.Empty,
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 10,
            ImportStatus = DocumentImportStatus.Processing,
            OwnerUserId = Guid.NewGuid(),
            TenantId = "tenant-a",
            StagingBlobKey = "document/staging-key.pdf"
        };
    }

    private sealed class FakeStagingBlobStore : IStagingBlobStore
    {
        private readonly Exception? _downloadException;
        private readonly Exception? _deleteException;

        public FakeStagingBlobStore(Exception? downloadException = null, Exception? deleteException = null)
        {
            _downloadException = downloadException;
            _deleteException = deleteException;
        }

        public int DownloadCount { get; private set; }

        public int DeleteCount { get; private set; }

        public Task<Stream> DownloadAsync(Document document, CancellationToken cancellationToken = default)
        {
            DownloadCount++;
            if (_downloadException is not null)
            {
                throw _downloadException;
            }

            return Task.FromResult<Stream>(new MemoryStream());
        }

        public Task DeleteAsync(Document document, CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            if (_deleteException is not null && DeleteCount == 1)
            {
                throw _deleteException;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakePdfTextExtractor : IPdfTextExtractor
    {
        private readonly PdfTextExtractionResult? _result;
        private readonly Exception? _exception;

        public FakePdfTextExtractor(PdfTextExtractionResult result)
        {
            _result = result;
        }

        public FakePdfTextExtractor(Exception exception)
        {
            _exception = exception;
        }

        public int ExtractionCount { get; private set; }

        public PdfTextExtractionResult Extract(Stream pdfStream, CancellationToken cancellationToken = default)
        {
            ExtractionCount++;
            if (_exception is not null)
            {
                throw _exception;
            }

            return _result ?? throw new InvalidOperationException("No extraction result was configured.");
        }
    }

    private sealed class FakeDocumentChunkingService : IDocumentChunkingService
    {
        private readonly IReadOnlyList<DocumentChunk>? _chunks;

        public FakeDocumentChunkingService(IReadOnlyList<DocumentChunk>? chunks = null)
        {
            _chunks = chunks;
        }

        public int ImportChunkingCallCount { get; private set; }

        public int LastChunkSize { get; private set; }

        public int LastChunkOverlap { get; private set; }

        public Task<IReadOnlyList<DocumentChunk>> GenerateChunksForImportAsync(
            Document validatedDocument,
            int chunkSize,
            int chunkOverlap,
            CancellationToken cancellationToken = default)
        {
            ImportChunkingCallCount++;
            LastChunkSize = chunkSize;
            LastChunkOverlap = chunkOverlap;
            IReadOnlyList<DocumentChunk> chunks = _chunks ??
            [
                new DocumentChunk
                {
                    DocumentId = validatedDocument.Id,
                    ChunkIndex = 0,
                    Content = "Generated chunk",
                    CharacterCount = "Generated chunk".Length
                }
            ];
            return Task.FromResult(chunks);
        }

        public Task<IReadOnlyList<DocumentChunk>> GenerateChunksAsync(
            Guid documentId,
            int chunkSize,
            int chunkOverlap,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<DocumentChunk>> GetChunksForDocumentAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<DocumentChunk?> GetByIdAsync(Guid chunkId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTelemetryService : ITelemetryService
    {
        public List<DocumentProcessingEvent> Events { get; } = [];

        public void TrackApiRequest(string route, int statusCode, double durationMs, string method)
        {
        }

        public void TrackDatabaseQuery(string operation, double durationMs, bool success)
        {
        }

        public void TrackDocumentProcessing(string operation, double durationMs, bool success, int itemCount = 0)
        {
            Events.Add(new DocumentProcessingEvent(operation, durationMs, success, itemCount));
        }

        public void TrackDocumentChunking(double durationMs, bool success, int generatedChunkCount, int reusedChunkCount)
        {
        }

        public void TrackEmbeddingGeneration(string modelName, double durationMs, int vectorDimensions, bool success)
        {
        }

        public void TrackVectorSearch(
            string modelName,
            int topK,
            int resultCount,
            double averageSimilarity,
            double durationMs,
            bool success)
        {
        }

        public void TrackRagQuery(
            int inputCharacterCount,
            int topK,
            int retrievedChunkCount,
            int contextLength,
            int promptLength,
            double durationMs,
            bool success)
        {
        }
    }

    private sealed record DocumentProcessingEvent(string Operation, double DurationMs, bool Success, int ItemCount);
}