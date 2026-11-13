using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentImportSettlementPolicy : IDocumentImportSettlementPolicy
{
    private readonly ApplicationDbContext _dbContext;

    public DocumentImportSettlementPolicy(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DocumentImportSettlementDisposition> DecideAsync(
        Document document,
        Exception? processingException,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (processingException is null)
        {
            return DocumentImportSettlementDisposition.Defer;
        }

        var failedStatePersisted = await _dbContext.Documents
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.Id == document.Id
                    && candidate.ImportStatus == DocumentImportStatus.Failed,
                cancellationToken);

        if (!failedStatePersisted)
        {
            return DocumentImportSettlementDisposition.Retry;
        }

        var targetId = document.Id.ToString();
        var permanentFailureAuditPersisted = await _dbContext.AuditEvents
            .AsNoTracking()
            .AnyAsync(
                auditEvent => auditEvent.TenantId == document.TenantId
                    && auditEvent.EventType == AuditEventTypes.DocumentImportPermanentPdfFailure
                    && auditEvent.Outcome == "Failed"
                    && auditEvent.TargetType == "Document"
                    && auditEvent.TargetId == targetId,
                cancellationToken);

        return permanentFailureAuditPersisted
            ? DocumentImportSettlementDisposition.DeadLetter
            : DocumentImportSettlementDisposition.Retry;
    }
}