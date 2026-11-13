using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentImportProcessor
{
    private const string BlobDownloadOperation = "StagingBlobDownload";
    private const string PdfExtractionOperation = "PdfTextExtraction";

    private readonly ApplicationDbContext _dbContext;
    private readonly IStagingBlobStore _stagingBlobStore;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IDocumentChunkingService _documentChunkingService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<DocumentImportProcessor> _logger;

    public DocumentImportProcessor(
        ApplicationDbContext dbContext,
        IStagingBlobStore stagingBlobStore,
        IPdfTextExtractor pdfTextExtractor,
        IDocumentChunkingService documentChunkingService,
        IAuditEventWriter auditEventWriter,
        ITelemetryService telemetryService,
        ILogger<DocumentImportProcessor> logger)
    {
        _dbContext = dbContext;
        _stagingBlobStore = stagingBlobStore;
        _pdfTextExtractor = pdfTextExtractor;
        _documentChunkingService = documentChunkingService;
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