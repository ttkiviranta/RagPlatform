using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Models;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentProcessingController : ControllerBase
{
    private readonly DocumentProcessingService _documentProcessingService;
    private readonly ILogger<DocumentProcessingController> _logger;

    public DocumentProcessingController(
        DocumentProcessingService documentProcessingService,
        ILogger<DocumentProcessingController> logger)
    {
        _documentProcessingService = documentProcessingService;
        _logger = logger;
    }

    /// <summary>
    /// Processes a document and generates normalized metadata for future indexing and retrieval workflows.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Generated document processing metadata.</returns>
    [HttpPost("{id:guid}/process")]
    [ProducesResponseType(typeof(DocumentMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentMetadata>> ProcessAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var metadata = await _documentProcessingService.ProcessAsync(id, cancellationToken);
            return Ok(metadata);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} was not found for processing.", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document {DocumentId} could not be processed.", id);
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(Document.Content)] = [ex.Message]
            }));
        }
    }

    /// <summary>
    /// Gets the processing metadata for a document.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Processing metadata for the selected document.</returns>
    [HttpGet("{id:guid}/metadata")]
    [ProducesResponseType(typeof(DocumentMetadata), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentMetadata>> GetMetadataAsync(Guid id, CancellationToken cancellationToken)
    {
        var metadata = await _documentProcessingService.GetMetadataAsync(id, cancellationToken);
        if (metadata is null)
        {
            _logger.LogWarning("Processing metadata for document {DocumentId} was not found.", id);
            return NotFound();
        }

        return Ok(metadata);
    }
}
