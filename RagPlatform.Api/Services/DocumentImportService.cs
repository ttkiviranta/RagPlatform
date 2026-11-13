using System.Diagnostics;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Contracts.Telemetry;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Options;

namespace RagPlatform.Api.Services;

public sealed class DocumentImportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _blobStorageOptions;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IDocumentImportMessagePublisher _messagePublisher;
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<DocumentImportService> _logger;

    public DocumentImportService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageOptions> blobStorageOptions,
        IAuditEventWriter auditEventWriter,
        IDocumentImportMessagePublisher messagePublisher,
        ITelemetryService telemetryService,
        ILogger<DocumentImportService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _blobServiceClient = blobServiceClient;
        _blobStorageOptions = blobStorageOptions.Value;
        _auditEventWriter = auditEventWriter;
        _messagePublisher = messagePublisher;
        _telemetryService = telemetryService;
        _logger = logger;
    }

    public async Task<Document> UploadPdfAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var validationStopwatch = Stopwatch.StartNew();
        (string FileName, string Title) fileDetails;
        try
        {
            fileDetails = ValidateFile(file);
            using var validationStream = file.OpenReadStream();
            await ValidatePdfSignatureAsync(validationStream, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(exception, "Rejected an invalid PDF document upload.");
            _telemetryService.TrackDocumentProcessing(
                "PdfUploadValidation",
                validationStopwatch.Elapsed.TotalMilliseconds,
                success: false);
            throw;
        }

        _telemetryService.TrackDocumentProcessing(
            "PdfUploadValidation",
            validationStopwatch.Elapsed.TotalMilliseconds,
            success: true,
            itemCount: 1);

        var (fileName, title) = fileDetails;
        var user = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var document = new Document
        {
            Title = title,
            Content = string.Empty,
            FileName = fileName,
            ContentType = "application/pdf",
            FileSize = file.Length,
            CreatedAt = now,
            UpdatedAt = now,
            ImportStatus = DocumentImportStatus.Queued,
            OwnerUserId = user.Id,
            TenantId = user.TenantId
        };

        var stagingBlobKey = $"{document.Id:N}/{Guid.NewGuid():N}.pdf";
        var containerClient = _blobServiceClient.GetBlobContainerClient(_blobStorageOptions.StagingContainerName);
        var blobClient = containerClient.GetBlobClient(stagingBlobKey);
        var stagingStopwatch = Stopwatch.StartNew();
        try
        {
            await containerClient.CreateIfNotExistsAsync(
                publicAccessType: PublicAccessType.None,
                cancellationToken: cancellationToken);

            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(
                stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" }
                },
                cancellationToken);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError(exception, "Blob staging failed for document {DocumentId}.", document.Id);
            _telemetryService.TrackDocumentProcessing(
                "BlobStaging",
                stagingStopwatch.Elapsed.TotalMilliseconds,
                success: false);
            throw;
        }

        _telemetryService.TrackDocumentProcessing(
            "BlobStaging",
            stagingStopwatch.Elapsed.TotalMilliseconds,
            success: true,
            itemCount: 1);

        document.StagingBlobKey = stagingBlobKey;
        try
        {
            _dbContext.Documents.Add(document);
            _auditEventWriter.Add(
                user.Id,
                user.TenantId,
                AuditEventTypes.DocumentCreated,
                "Succeeded",
                "Document",
                document.Id.ToString());
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception persistenceException)
        {
            try
            {
                await blobClient.DeleteIfExistsAsync(cancellationToken: CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(
                    cleanupException,
                    "Could not remove staging blob for document {DocumentId} after persistence failed.",
                    document.Id);
                throw new AggregateException(
                    "Document persistence failed and staging blob cleanup also failed.",
                    persistenceException,
                    cleanupException);
            }

            throw;
        }

        var importMessage = new DocumentImportMessage(
            document.Id,
            document.TenantId,
            user.Id,
            stagingBlobKey,
            DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc));
        var publishStopwatch = Stopwatch.StartNew();
        try
        {
            await _messagePublisher.PublishAsync(importMessage, cancellationToken);
        }
        catch (Exception publishException)
        {
            _logger.LogError(
                publishException,
                "Publishing the import message failed for document {DocumentId}.",
                document.Id);

            document.ImportStatus = DocumentImportStatus.Failed;
            document.UpdatedAt = DateTime.UtcNow;
            _auditEventWriter.Add(
                user.Id,
                user.TenantId,
                AuditEventTypes.DocumentImportMessagePublishFailed,
                "Failed",
                "Document",
                document.Id.ToString());

            try
            {
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception statusPersistenceException)
            {
                _logger.LogError(
                    statusPersistenceException,
                    "Could not persist Failed status after import message publishing failed for document {DocumentId}.",
                    document.Id);
                _telemetryService.TrackDocumentProcessing(
                    "ServiceBusMessagePublish",
                    publishStopwatch.Elapsed.TotalMilliseconds,
                    success: false);
                throw new DocumentImportPublishException(
                    document.Id,
                    failureStatusPersisted: false,
                    new AggregateException(publishException, statusPersistenceException));
            }

            _telemetryService.TrackDocumentProcessing(
                "ServiceBusMessagePublish",
                publishStopwatch.Elapsed.TotalMilliseconds,
                success: false);
            throw new DocumentImportPublishException(
                document.Id,
                failureStatusPersisted: true,
                publishException);
        }

        _telemetryService.TrackDocumentProcessing(
            "ServiceBusMessagePublish",
            publishStopwatch.Elapsed.TotalMilliseconds,
            success: true,
            itemCount: 1);
        _logger.LogInformation("Published import message for document {DocumentId}.", document.Id);

        _logger.LogInformation(
            "Staged PDF for document {DocumentId} with {FileSize} bytes.",
            document.Id,
            document.FileSize);

        return document;
    }

    private static (string FileName, string Title) ValidateFile(IFormFile file)
    {
        if (file.Length == 0)
        {
            throw new ArgumentException("Empty files are not allowed.", nameof(file));
        }

        if (file.Length > Contracts.Documents.PdfDocumentUploadRequest.MaximumFileSizeBytes)
        {
            throw new ArgumentException("The PDF exceeds the 25 MB upload limit.", nameof(file));
        }

        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(Path.GetExtension(fileName), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only PDF files are allowed.", nameof(file));
        }

        if (fileName.Length > 255)
        {
            throw new ArgumentException("The file name exceeds the 255-character limit.", nameof(file));
        }

        var title = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200)
        {
            throw new ArgumentException("The PDF file name must produce a title of 1 to 200 characters.", nameof(file));
        }

        return (fileName, title);
    }

    private static async Task ValidatePdfSignatureAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (!stream.CanSeek)
        {
            throw new InvalidOperationException("The uploaded PDF stream must support seeking.");
        }

        var header = new byte[1024];
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var read = await stream.ReadAsync(header.AsMemory(bytesRead), cancellationToken);
            if (read == 0)
            {
                break;
            }

            bytesRead += read;
        }

        if (header.AsSpan(0, bytesRead).IndexOf("%PDF-"u8) < 0)
        {
            throw new ArgumentException("The uploaded file does not have a valid PDF header.", "file");
        }

        stream.Position = 0;
    }
}
