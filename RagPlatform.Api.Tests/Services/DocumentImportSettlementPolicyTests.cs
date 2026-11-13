using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class DocumentImportSettlementPolicyTests
{
    [TestMethod]
    public async Task DecideAsync_AfterSuccessfulProcessing_DefersSettlement()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument(DocumentImportStatus.Processing);
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var policy = new DocumentImportSettlementPolicy(dbContext);

        var disposition = await policy.DecideAsync(document, null);

        Assert.AreEqual(DocumentImportSettlementDisposition.Defer, disposition);
    }

    [TestMethod]
    public async Task DecideAsync_WhenFailureIsTransient_ReturnsRetry()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument(DocumentImportStatus.Processing);
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var policy = new DocumentImportSettlementPolicy(dbContext);

        var disposition = await policy.DecideAsync(document, new IOException("Temporary failure."));

        Assert.AreEqual(DocumentImportSettlementDisposition.Retry, disposition);
        Assert.AreEqual(DocumentImportStatus.Processing, document.ImportStatus);
    }

    [TestMethod]
    public async Task DecideAsync_WhenFailedStateHasNoPermanentFailureAudit_ReturnsRetry()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument(DocumentImportStatus.Failed);
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var policy = new DocumentImportSettlementPolicy(dbContext);

        var disposition = await policy.DecideAsync(document, new InvalidOperationException("Failure."));

        Assert.AreEqual(DocumentImportSettlementDisposition.Retry, disposition);
    }

    [TestMethod]
    public async Task DecideAsync_WhenFailedStateAndPermanentFailureAuditArePersisted_ReturnsDeadLetter()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument(DocumentImportStatus.Failed);
        await dbContext.Documents.AddAsync(document);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = document.TenantId,
            ActorUserId = document.OwnerUserId,
            EventType = AuditEventTypes.DocumentImportPermanentPdfFailure,
            Outcome = "Failed",
            TargetType = "Document",
            TargetId = document.Id.ToString()
        });
        await dbContext.SaveChangesAsync();
        var policy = new DocumentImportSettlementPolicy(dbContext);

        var disposition = await policy.DecideAsync(document, new InvalidOperationException("Failure."));

        Assert.AreEqual(DocumentImportSettlementDisposition.DeadLetter, disposition);
    }

    [TestMethod]
    public async Task DecideAsync_WhenAuditDoesNotMatchDocumentTenant_ReturnsRetry()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument(DocumentImportStatus.Failed);
        await dbContext.Documents.AddAsync(document);
        dbContext.AuditEvents.Add(new AuditEvent
        {
            TenantId = "another-tenant",
            ActorUserId = document.OwnerUserId,
            EventType = AuditEventTypes.DocumentImportPermanentPdfFailure,
            Outcome = "Failed",
            TargetType = "Document",
            TargetId = document.Id.ToString()
        });
        await dbContext.SaveChangesAsync();
        var policy = new DocumentImportSettlementPolicy(dbContext);

        var disposition = await policy.DecideAsync(document, new InvalidOperationException("Failure."));

        Assert.AreEqual(DocumentImportSettlementDisposition.Retry, disposition);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static Document CreateDocument(DocumentImportStatus status)
    {
        return new Document
        {
            Title = "Test document",
            Content = string.Empty,
            FileName = "test.pdf",
            ContentType = "application/pdf",
            FileSize = 10,
            ImportStatus = status,
            OwnerUserId = Guid.NewGuid(),
            TenantId = "tenant-a",
            StagingBlobKey = "document/staging-key.pdf"
        };
    }
}