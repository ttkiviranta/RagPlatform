using Microsoft.EntityFrameworkCore;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Data;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Services;

public sealed class DocumentProcessingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IPlainTextProcessor _plainTextProcessor;
    private readonly IMarkdownProcessor _markdownProcessor;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        ApplicationDbContext dbContext,
        IPlainTextProcessor plainTextProcessor,
        IMarkdownProcessor markdownProcessor,
        ILogger<DocumentProcessingService> logger)
    {
        _dbContext = dbContext;
        _plainTextProcessor = plainTextProcessor;
        _markdownProcessor = markdownProcessor;
        _logger = logger;
    }

    public async Task<DocumentMetadata?> GetMetadataAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.DocumentMetadata
            .AsNoTracking()
            .FirstOrDefaultAsync(metadata => metadata.DocumentId == documentId, cancellationToken);
    }

    public async Task<DocumentMetadata> ProcessAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _dbContext.Documents
            .FirstOrDefaultAsync(item => item.Id == documentId, cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException($"Document {documentId} was not found.");
        }

        if (string.IsNullOrWhiteSpace(document.Content))
        {
            throw new InvalidOperationException("Document content cannot be empty for processing.");
        }

        var processor = ResolveProcessor(document);
        var response = await processor.ProcessAsync(document, cancellationToken);

        var existingMetadata = await _dbContext.DocumentMetadata
            .FirstOrDefaultAsync(metadata => metadata.DocumentId == documentId, cancellationToken);

        if (existingMetadata is null)
        {
            _dbContext.DocumentMetadata.Add(response);
        }
        else
        {
            existingMetadata.DocumentId = response.DocumentId;
            existingMetadata.FileName = response.FileName;
            existingMetadata.ContentType = response.ContentType;
            existingMetadata.CharacterCount = response.CharacterCount;
            existingMetadata.WordCount = response.WordCount;
            existingMetadata.LineCount = response.LineCount;
            existingMetadata.ProcessedAt = response.ProcessedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Document {DocumentId} processed successfully. CharacterCount={CharacterCount}, WordCount={WordCount}, LineCount={LineCount}",
            documentId,
            response.CharacterCount,
            response.WordCount,
            response.LineCount);

        return response;
    }

    private IDocumentProcessor ResolveProcessor(Document document)
    {
        var extension = Path.GetExtension(document.FileName);
        var contentType = document.ContentType ?? string.Empty;

        if (contentType.Contains("markdown", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase))
        {
            return _markdownProcessor;
        }

        return _plainTextProcessor;
    }
}
