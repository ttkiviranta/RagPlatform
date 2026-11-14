using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class DocumentImportMessageHandlerTests
{
    private static readonly JsonSerializerOptions MessageJsonOptions = new(JsonSerializerDefaults.Web);

    [TestMethod]
    public async Task HandleAsync_RejectsEmptyBody()
    {
        using var dbContext = CreateDbContext();
        var handler = CreateHandler(dbContext);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDocumentImportMessageException>(
            () => handler.HandleAsync(ReadOnlyMemory<byte>.Empty));

        Assert.AreEqual(DocumentImportMessageFailureKind.EmptyBody, exception.FailureKind);
    }

    [TestMethod]
    public async Task HandleAsync_RejectsMalformedJson()
    {
        using var dbContext = CreateDbContext();
        var handler = CreateHandler(dbContext);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDocumentImportMessageException>(
            () => handler.HandleAsync("{"u8.ToArray()));

        Assert.AreEqual(DocumentImportMessageFailureKind.MalformedJson, exception.FailureKind);
        Assert.IsInstanceOfType<JsonException>(exception.InnerException);
    }

    [TestMethod]
    public async Task HandleAsync_RejectsMessageWithMissingRequiredFields()
    {
        using var dbContext = CreateDbContext();
        var handler = CreateHandler(dbContext);
        var message = new DocumentImportMessage(Guid.Empty, " ", Guid.Empty, " ", default);

        var exception = await Assert.ThrowsExactlyAsync<InvalidDocumentImportMessageException>(
            () => handler.HandleAsync(Serialize(message)));

        Assert.AreEqual(DocumentImportMessageFailureKind.InvalidRequiredFields, exception.FailureKind);
    }

    [TestMethod]
    public async Task HandleAsync_RejectsMessageForMissingDocument()
    {
        using var dbContext = CreateDbContext();
        var handler = CreateHandler(dbContext);
        var message = CreateMessage();

        var exception = await Assert.ThrowsExactlyAsync<InvalidDocumentImportMessageException>(
            () => handler.HandleAsync(Serialize(message)));

        Assert.AreEqual(DocumentImportMessageFailureKind.DocumentNotFound, exception.FailureKind);
    }

    [TestMethod]
    [DataRow("Tenant")]
    [DataRow("Owner")]
    [DataRow("StagingBlobKey")]
    public async Task HandleAsync_RejectsMessageThatDoesNotMatchPersistedDocument(string mismatchedField)
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var handler = CreateHandler(dbContext);
        var message = CreateMessage(document);
        message = mismatchedField switch
        {
            "Tenant" => message with { TenantId = "another-tenant" },
            "Owner" => message with { OwnerUserId = Guid.NewGuid() },
            "StagingBlobKey" => message with { StagingBlobKey = "another/staging-key.pdf" },
            _ => throw new ArgumentOutOfRangeException(nameof(mismatchedField))
        };

        var exception = await Assert.ThrowsExactlyAsync<InvalidDocumentImportMessageException>(
            () => handler.HandleAsync(Serialize(message)));

        Assert.AreEqual(DocumentImportMessageFailureKind.DocumentMismatch, exception.FailureKind);
        Assert.AreEqual(DocumentImportStatus.Queued, document.ImportStatus);
        Assert.AreEqual(0, await dbContext.AuditEvents.CountAsync());
    }

    [TestMethod]
    public async Task HandleAsync_MatchingMessageTransitionsQueuedDocumentAndAddsAuditEvent()
    {
        using var dbContext = CreateDbContext();
        var document = CreateDocument();
        await dbContext.Documents.AddAsync(document);
        await dbContext.SaveChangesAsync();
        var handler = CreateHandler(dbContext);

        var result = await handler.HandleAsync(Serialize(CreateMessage(document)));

        Assert.AreSame(document, result);
        Assert.AreEqual(DocumentImportStatus.Processing, result.ImportStatus);
        Assert.AreNotEqual(default, result.UpdatedAt);

        var auditEvent = await dbContext.AuditEvents.SingleAsync();
        Assert.AreEqual(AuditEventTypes.DocumentImportStarted, auditEvent.EventType);
        Assert.AreEqual("Succeeded", auditEvent.Outcome);
        Assert.AreEqual(document.Id.ToString(), auditEvent.TargetId);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DocumentImportMessageHandler CreateHandler(ApplicationDbContext dbContext)
    {
        var auditEventWriter = new AuditEventWriter(dbContext, new HttpContextAccessor());
        return new DocumentImportMessageHandler(dbContext, auditEventWriter);
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
            ImportStatus = DocumentImportStatus.Queued,
            OwnerUserId = Guid.NewGuid(),
            TenantId = "tenant-a",
            StagingBlobKey = "document/staging-key.pdf"
        };
    }

    private static DocumentImportMessage CreateMessage(Document? document = null)
    {
        document ??= CreateDocument();
        return new DocumentImportMessage(
            document.Id,
            document.TenantId,
            document.OwnerUserId!.Value,
            document.StagingBlobKey!,
            DateTime.UtcNow);
    }

    private static byte[] Serialize(DocumentImportMessage message)
    {
        return JsonSerializer.SerializeToUtf8Bytes(message, MessageJsonOptions);
    }
}
