using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentImportMessageHandler
{
    private static readonly JsonSerializerOptions MessageJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public DocumentImportMessageHandler(
        ApplicationDbContext dbContext,
        IAuditEventWriter auditEventWriter)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    public async Task<Document> HandleAsync(
        ReadOnlyMemory<byte> messageBody,
        CancellationToken cancellationToken = default)
    {
        if (messageBody.IsEmpty)
        {
            throw new InvalidDataException("The document import message body is empty.");
        }

        DocumentImportMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<DocumentImportMessage>(messageBody.ToArray(), MessageJsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The document import message body is not valid JSON.", exception);
        }

        if (message is null)
        {
            throw new InvalidDataException("The document import message body did not contain a message.");
        }

        var invalidFields = new List<string>();
        if (message.DocumentId == Guid.Empty)
        {
            invalidFields.Add(nameof(message.DocumentId));
        }

        if (string.IsNullOrWhiteSpace(message.TenantId))
        {
            invalidFields.Add(nameof(message.TenantId));
        }

        if (message.OwnerUserId == Guid.Empty)
        {
            invalidFields.Add(nameof(message.OwnerUserId));
        }

        if (string.IsNullOrWhiteSpace(message.StagingBlobKey))
        {
            invalidFields.Add(nameof(message.StagingBlobKey));
        }

        if (message.CreatedAtUtc == default || message.CreatedAtUtc.Kind != DateTimeKind.Utc)
        {
            invalidFields.Add(nameof(message.CreatedAtUtc));
        }

        if (invalidFields.Count > 0)
        {
            throw new InvalidDataException(
                $"The document import message has missing or invalid required fields: {string.Join(", ", invalidFields)}.");
        }

        var document = await _dbContext.Documents
            .SingleOrDefaultAsync(candidate => candidate.Id == message.DocumentId, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                $"Document {message.DocumentId} referenced by the import message does not exist.");
        }

        if (!string.Equals(document.TenantId, message.TenantId, StringComparison.Ordinal)
            || document.OwnerUserId != message.OwnerUserId
            || !string.Equals(document.StagingBlobKey, message.StagingBlobKey, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The document import message does not match persisted document {document.Id}.");
        }

        if (document.ImportStatus == DocumentImportStatus.Queued)
        {
            document.ImportStatus = DocumentImportStatus.Processing;
            document.UpdatedAt = DateTime.UtcNow;
            _auditEventWriter.Add(
                document.OwnerUserId,
                document.TenantId,
                AuditEventTypes.DocumentImportStarted,
                "Succeeded",
                "Document",
                document.Id.ToString());

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return document;
    }
}
