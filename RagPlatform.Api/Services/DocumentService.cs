using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Audit;
using RagPlatform.Api.Contracts.Authorization;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentService : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".md"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IDocumentAccessService _documentAccessService;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext dbContext,
        IDocumentAccessService documentAccessService,
        IAuditEventWriter auditEventWriter,
        ILogger<DocumentService> logger)
    {
        _dbContext = dbContext;
        _documentAccessService = documentAccessService;
        _auditEventWriter = auditEventWriter;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<Document>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var user = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
        return await _documentAccessService.ScopeDocuments(_dbContext.Documents, user)
            .AsNoTracking()
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _documentAccessService.GetAccessibleDocumentAsync(id, DocumentPermission.Read, cancellationToken);
    }

    public async Task<Document> CreateAsync(DocumentCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = NormalizeRequiredValue(request.Title, nameof(request.Title));
        var content = NormalizeRequiredValue(request.Content, nameof(request.Content));
        var user = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);

        var document = new Document
        {
            Title = title,
            Content = content,
            OwnerUserId = user.Id,
            TenantId = user.TenantId,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Creating document {DocumentId}", document.Id);

        _dbContext.Documents.Add(document);
        _auditEventWriter.Add(user.Id, user.TenantId, AuditEventTypes.DocumentCreated, "Succeeded", "Document", document.Id.ToString());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return document;
    }

    public async Task<Document?> UpdateAsync(Guid id, DocumentUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = NormalizeRequiredValue(request.Title, nameof(request.Title));
        var content = NormalizeRequiredValue(request.Content, nameof(request.Content));

        var existingDocument = await _documentAccessService.GetAccessibleDocumentAsync(
            id,
            DocumentPermission.Write,
            cancellationToken);

        if (existingDocument is null)
        {
            _logger.LogWarning("Update requested for missing document {DocumentId}", id);
            return null;
        }

        existingDocument.Title = title;
        existingDocument.Content = content;
        var user = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
        _auditEventWriter.Add(user.Id, user.TenantId, AuditEventTypes.DocumentUpdated, "Succeeded", "Document", id.ToString());

        _logger.LogInformation("Updating document {DocumentId}", id);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return existingDocument;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _documentAccessService.GetAccessibleDocumentAsync(
            id,
            DocumentPermission.Delete,
            cancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Delete requested for missing document {DocumentId}", id);
            return false;
        }

        var user = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);
        _auditEventWriter.Add(user.Id, user.TenantId, AuditEventTypes.DocumentDeleted, "Succeeded", "Document", id.ToString());
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted document {DocumentId}", id);

        return true;
    }

    public async Task<Document> UploadTextDocumentAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        var user = await _documentAccessService.GetRequiredCurrentUserAsync(cancellationToken);

        if (file.Length == 0)
        {
            throw new ArgumentException("Empty files are not allowed.", nameof(file));
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Unsupported file type. Only .txt and .md files are allowed.", nameof(file));
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;

        string content;
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            content = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Uploaded file does not contain any text content.", nameof(file));
        }

        var title = string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(file.FileName))
            ? "Uploaded document"
            : Path.GetFileNameWithoutExtension(file.FileName);

        var document = new Document
        {
            Title = title,
            Content = content,
            FileName = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? GetContentType(extension) : file.ContentType,
            FileSize = file.Length,
            OwnerUserId = user.Id,
            TenantId = user.TenantId,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Uploading document {FileName} ({FileSize} bytes, {ContentType})",
            document.FileName,
            document.FileSize,
            document.ContentType);

        _dbContext.Documents.Add(document);
        _auditEventWriter.Add(user.Id, user.TenantId, AuditEventTypes.DocumentCreated, "Succeeded", "Document", document.Id.ToString());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return document;
    }

    private static string NormalizeRequiredValue(string? value, string propertyName)
    {
        var normalizedValue = value?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            throw new ArgumentException($"{propertyName} is required.", propertyName);
        }

        return normalizedValue;
    }

    private static string GetContentType(string extension)
    {
        return extension switch
        {
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };
    }
}
