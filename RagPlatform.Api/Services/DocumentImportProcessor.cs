using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class DocumentImportProcessor
{
    private const string BlobDownloadOperation = "StagingBlobDownload";
    private const string PdfExtractionOperation = "PdfTextExtraction";

    private readonly ApplicationDbContext _dbContext;
    private readonly IStagingBlobStore _stagingBlobStore;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IDocumentChunkingService _documentChunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly FoundryOptions _foundryOptions;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<DocumentImportProcessor> _logger;

    public DocumentImportProcessor(
        ApplicationDbContext dbContext,
        IStagingBlobStore stagingBlobStore,
        IPdfTextExtractor pdfTextExtractor,
        IDocumentChunkingService documentChunkingService,
        IEmbeddingService embeddingService,
        IEmbeddingProvider embeddingProvider,
        IOptions<FoundryOptions> foundryOptions,
        IAuditEventWriter auditEventWriter,
        ITelemetryService telemetryService,
        ILogger<DocumentImportProcessor> logger)
    {
        _dbContext = dbContext;
        _stagingBlobStore = stagingBlobStore;
        _pdfTextExtractor = pdfTextExtractor;
        _documentChunkingService = documentChunkingService;
        _embeddingService = embeddingService;
        _embeddingProvider = embeddingProvider;
        _foundryOptions = foundryOptions.Value;
        _auditEventWriter = auditEventWriter;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public async Task ProcessAsync(Document validatedDocument, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validatedDocument);

        var document = await _dbContext.Documents
            .SingleOrDefaultAsync(candidate => candidate.Id == validatedDocument.Id, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                $"Validated document {validatedDocument.Id} no longer exists.");
        }

        if (document.TenantId != validatedDocument.TenantId
            || document.OwnerUserId != validatedDocument.OwnerUserId
            || document.StagingBlobKey != validatedDocument.StagingBlobKey)
        {
            throw new InvalidDataException(
                $"Validated document {validatedDocument.Id} no longer matches its persisted record.");
        }

        if (document.ImportStatus == DocumentImportStatus.Completed)
        {
            await FinalizeImportAsync(document, cancellationToken);
            return;
        }

        if (document.ImportStatus != DocumentImportStatus.Processing)
        {
            throw new InvalidOperationException(
                $"Document {document.Id} must be in Processing status before import can continue.");
        }

        if (!string.IsNullOrWhiteSpace(document.Content))
        {
            var documentTargetId = document.Id.ToString();
            var extractionAuditPersisted = await _dbContext.AuditEvents.AnyAsync(
                auditEvent => auditEvent.EventType == AuditEventTypes.DocumentImportExtractionSucceeded
                    && auditEvent.Outcome == "Succeeded"
                    && auditEvent.TargetType == "Document"
                    && auditEvent.TargetId == documentTargetId,
                cancellationToken);

            if (!extractionAuditPersisted)
            {
                throw new InvalidOperationException(
                    $"Document {document.Id} has content but no persisted extraction-success audit event.");
            }

            await DeleteStagingBlobAsync(document, cancellationToken);
            await GenerateChunksAsync(document, cancellationToken);
            await _embeddingService.GenerateAndPersistEmbeddingsForImportAsync(document, cancellationToken);
            await FinalizeImportAsync(document, cancellationToken);
            return;
        }

        Stream pdfStream;
        var downloadStopwatch = Stopwatch.StartNew();
        try
        {
            pdfStream = await _stagingBlobStore.DownloadAsync(document, cancellationToken);
        }
        catch
        {
            _telemetryService.TrackDocumentProcessing(
                BlobDownloadOperation,
                downloadStopwatch.Elapsed.TotalMilliseconds,
                success: false);
            throw;
        }

        _telemetryService.TrackDocumentProcessing(
            BlobDownloadOperation,
            downloadStopwatch.Elapsed.TotalMilliseconds,
            success: true);

        await using (pdfStream)
        {
            var extractionStopwatch = Stopwatch.StartNew();
            PdfTextExtractionResult extractionResult;
            try
            {
                extractionResult = _pdfTextExtractor.Extract(pdfStream, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(extractionResult.Text))
                {
                    throw new InvalidDataException("PDF extraction returned no text to persist.");
                }
            }
            catch (PermanentPdfFailureException exception)
            {
                _telemetryService.TrackDocumentProcessing(
                    PdfExtractionOperation,
                    extractionStopwatch.Elapsed.TotalMilliseconds,
                    success: false);
                await PersistPermanentFailureAsync(document, exception, extractionStopwatch.Elapsed.TotalMilliseconds, cancellationToken);
                throw;
            }
            catch
            {
                _telemetryService.TrackDocumentProcessing(
                    PdfExtractionOperation,
                    extractionStopwatch.Elapsed.TotalMilliseconds,
                    success: false);
                throw;
            }

            _telemetryService.TrackDocumentProcessing(
                PdfExtractionOperation,
                extractionStopwatch.Elapsed.TotalMilliseconds,
                success: true,
                itemCount: extractionResult.PageCount);

            document.Content = extractionResult.Text;
            document.UpdatedAt = DateTime.UtcNow;
            _auditEventWriter.Add(
                document.OwnerUserId,
                document.TenantId,
                AuditEventTypes.DocumentImportExtractionSucceeded,
                "Succeeded",
                "Document",
                document.Id.ToString(),
                durationMilliseconds: extractionStopwatch.Elapsed.TotalMilliseconds);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Extracted PDF text and saved it for document {DocumentId}; page count is {PageCount}.",
                document.Id,
                extractionResult.PageCount);

            await DeleteStagingBlobAsync(document, cancellationToken);
            await GenerateChunksAsync(document, cancellationToken);
            await _embeddingService.GenerateAndPersistEmbeddingsForImportAsync(document, cancellationToken);
            await FinalizeImportAsync(document, cancellationToken);
        }
    }

    public async Task ValidateCompletedImportAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using var transaction = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var persistedDocument = await GetLockedDocumentAsync(documentId, transaction is not null, cancellationToken);
            if (persistedDocument.ImportStatus != DocumentImportStatus.Completed)
            {
                throw new InvalidOperationException(
                    $"Document {documentId} must be Completed before its import message can be completed.");
            }

            await ValidateCompletionEvidenceAsync(persistedDocument, cancellationToken);
            await ValidateCompletionAuditAsync(documentId, cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
    }

    private async Task FinalizeImportAsync(Document document, CancellationToken cancellationToken)
    {
        await using var transaction = _dbContext.Database.IsNpgsql()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        AuditEvent? completionAudit = null;
        var originalStatus = document.ImportStatus;
        var originalUpdatedAt = document.UpdatedAt;

        try
        {
            var persistedDocument = await GetLockedDocumentAsync(document.Id, transaction is not null, cancellationToken);
            if (persistedDocument.ImportStatus == DocumentImportStatus.Completed)
            {
                await ValidateCompletionEvidenceAsync(persistedDocument, cancellationToken);
                await ValidateCompletionAuditAsync(persistedDocument.Id, cancellationToken);
                document.ImportStatus = persistedDocument.ImportStatus;
                document.UpdatedAt = persistedDocument.UpdatedAt;
            }
            else
            {
                if (persistedDocument.ImportStatus != DocumentImportStatus.Processing)
                {
                    throw new InvalidOperationException(
                        $"Document {persistedDocument.Id} must be in Processing status before import can be finalized.");
                }

                await ValidateCompletionEvidenceAsync(persistedDocument, cancellationToken);

                document.ImportStatus = DocumentImportStatus.Completed;
                document.UpdatedAt = DateTime.UtcNow;
                _auditEventWriter.Add(
                    persistedDocument.OwnerUserId,
                    persistedDocument.TenantId,
                    AuditEventTypes.DocumentImportCompleted,
                    "Succeeded",
                    "Document",
                    document.Id.ToString());
                completionAudit = _dbContext.ChangeTracker.Entries<AuditEvent>()
                    .Single(entry => entry.State == EntityState.Added
                        && entry.Entity.EventType == AuditEventTypes.DocumentImportCompleted
                        && entry.Entity.TargetType == "Document"
                        && entry.Entity.TargetId == document.Id.ToString())
                    .Entity;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            document.ImportStatus = originalStatus;
            document.UpdatedAt = originalUpdatedAt;
            if (completionAudit is not null)
            {
                _dbContext.Entry(completionAudit).State = EntityState.Detached;
            }

            throw;
        }
    }

    private async Task<Document> GetLockedDocumentAsync(
        Guid documentId,
        bool usePostgreSqlLock,
        CancellationToken cancellationToken)
    {
        var document = usePostgreSqlLock
            ? (await _dbContext.Documents
                .FromSqlInterpolated($"SELECT * FROM \"Documents\" WHERE \"Id\" = {documentId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(cancellationToken))
                .SingleOrDefault()
            : await _dbContext.Documents
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == documentId, cancellationToken);

        return document
            ?? throw new KeyNotFoundException($"Document {documentId} was not found during import finalization.");
    }

    private async Task ValidateCompletionEvidenceAsync(Document document, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(document.Content))
        {
            throw new InvalidDataException($"Document {document.Id} has no persisted content.");
        }

        var chunks = await _dbContext.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == document.Id)
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToListAsync(cancellationToken);
        if (chunks.Count == 0)
        {
            throw new InvalidDataException($"Document {document.Id} has no persisted chunks.");
        }

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            if (chunk.ChunkIndex != index
                || string.IsNullOrWhiteSpace(chunk.Content)
                || chunk.CharacterCount != chunk.Content.Length)
            {
                throw new InvalidDataException($"Document {document.Id} has an invalid persisted chunk set.");
            }
        }

        var documentId = document.Id.ToString();
        var hasChunkAudit = await _dbContext.AuditEvents.AnyAsync(
            auditEvent => auditEvent.EventType == AuditEventTypes.DocumentChunksGenerated
                && auditEvent.Outcome == "Succeeded"
                && auditEvent.TargetType == "Document"
                && auditEvent.TargetId == documentId
                && auditEvent.ResultCount == chunks.Count,
            cancellationToken);
        if (!hasChunkAudit)
        {
            throw new InvalidDataException($"Document {document.Id} has no matching chunk-generation audit event.");
        }

        if (_foundryOptions.EmbeddingDimensions <= 0)
        {
            throw new InvalidOperationException("Foundry:EmbeddingDimensions must be greater than zero.");
        }

        var modelName = _embeddingProvider.ModelName;
        var chunkIds = chunks.Select(chunk => chunk.Id).ToArray();
        var embeddings = await _dbContext.DocumentEmbeddings
            .AsNoTracking()
            .Where(embedding => chunkIds.Contains(embedding.ChunkId)
                && embedding.ModelName == modelName)
            .ToListAsync(cancellationToken);
        if (embeddings.Count != chunks.Count)
        {
            throw new InvalidDataException(
                $"Document {document.Id} does not have a complete embedding set for model '{modelName}'.");
        }

        foreach (var embedding in embeddings)
        {
            var vectorDimensions = embedding.EmbeddingVector.ToArray().Length;
            if (vectorDimensions == 0
                || vectorDimensions != _foundryOptions.EmbeddingDimensions
                || vectorDimensions != embedding.VectorDimensions)
            {
                throw new InvalidDataException(
                    $"Document {document.Id} has an invalid persisted embedding for model '{modelName}'.");
            }
        }

        var hasEmbeddingAudit = await _dbContext.AuditEvents.AnyAsync(
            auditEvent => auditEvent.EventType == AuditEventTypes.DocumentEmbeddingsGenerated
                && auditEvent.Outcome == "Succeeded"
                && auditEvent.TargetType == "Document"
                && auditEvent.TargetId == documentId
                && auditEvent.ModelDeployment == modelName,
            cancellationToken);
        if (!hasEmbeddingAudit)
        {
            throw new InvalidDataException(
                $"Document {document.Id} has no embedding-generation audit event for model '{modelName}'.");
        }
    }

    private async Task ValidateCompletionAuditAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var hasCompletionAudit = await _dbContext.AuditEvents.AnyAsync(
            auditEvent => auditEvent.EventType == AuditEventTypes.DocumentImportCompleted
                && auditEvent.Outcome == "Succeeded"
                && auditEvent.TargetType == "Document"
                && auditEvent.TargetId == documentId.ToString(),
            cancellationToken);
        if (!hasCompletionAudit)
        {
            throw new InvalidDataException($"Completed document {documentId} has no completion audit event.");
        }
    }

    private async Task GenerateChunksAsync(Document document, CancellationToken cancellationToken)
    {
        var chunkRequest = new DocumentChunkRequest();
        var chunks = await _documentChunkingService.GenerateChunksForImportAsync(
            document,
            chunkRequest.ChunkSize,
            chunkRequest.ChunkOverlap,
            cancellationToken);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException($"Chunking produced no chunks for document {document.Id}.");
        }

        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            if (chunk.ChunkIndex != index
                || string.IsNullOrWhiteSpace(chunk.Content)
                || chunk.CharacterCount != chunk.Content.Length)
            {
                throw new InvalidDataException(
                    $"Chunking returned an invalid chunk set for document {document.Id}.");
            }
        }
    }

    private async Task DeleteStagingBlobAsync(Document document, CancellationToken cancellationToken)
    {
        var cleanupStopwatch = Stopwatch.StartNew();
        try
        {
            await _stagingBlobStore.DeleteAsync(document, cancellationToken);
        }
        catch (Exception exception)
        {
            _telemetryService.TrackDocumentProcessing(
                "StagingBlobCleanup",
                cleanupStopwatch.Elapsed.TotalMilliseconds,
                success: false);
            _logger.LogWarning(
                exception,
                "Could not delete the staging blob for document {DocumentId}; cleanup will be retried.",
                document.Id);
            throw;
        }

        _telemetryService.TrackDocumentProcessing(
            "StagingBlobCleanup",
            cleanupStopwatch.Elapsed.TotalMilliseconds,
            success: true);
    }

    private async Task PersistPermanentFailureAsync(
        Document document,
        PermanentPdfFailureException exception,
        double extractionDurationMilliseconds,
        CancellationToken cancellationToken)
    {
        if (document.ImportStatus != DocumentImportStatus.Processing
            || !string.IsNullOrWhiteSpace(document.Content))
        {
            return;
        }

        document.ImportStatus = DocumentImportStatus.Failed;
        document.UpdatedAt = DateTime.UtcNow;
        _auditEventWriter.Add(
            document.OwnerUserId,
            document.TenantId,
            AuditEventTypes.DocumentImportPermanentPdfFailure,
            "Failed",
            "Document",
            document.Id.ToString(),
            durationMilliseconds: extractionDurationMilliseconds);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Document {DocumentId} failed PDF extraction permanently. FailureKind={FailureKind}.",
            document.Id,
            exception.FailureKind);
    }
}