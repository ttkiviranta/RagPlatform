using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(ApplicationDbContext dbContext, ILogger<DocumentService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<Document>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);
    }

    public async Task<Document> CreateAsync(DocumentCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = NormalizeRequiredValue(request.Title, nameof(request.Title));
        var content = NormalizeRequiredValue(request.Content, nameof(request.Content));

        var document = new Document
        {
            Title = title,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Creating document {DocumentId}", document.Id);

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return document;
    }

    public async Task<Document?> UpdateAsync(Guid id, DocumentUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var title = NormalizeRequiredValue(request.Title, nameof(request.Title));
        var content = NormalizeRequiredValue(request.Content, nameof(request.Content));

        var existingDocument = await _dbContext.Documents
            .FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

        if (existingDocument is null)
        {
            _logger.LogWarning("Update requested for missing document {DocumentId}", id);
            return null;
        }

        existingDocument.Title = title;
        existingDocument.Content = content;

        _logger.LogInformation("Updating document {DocumentId}", id);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return existingDocument;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (document is null)
        {
            _logger.LogWarning("Delete requested for missing document {DocumentId}", id);
            return false;
        }

        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted document {DocumentId}", id);

        return true;
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
}
