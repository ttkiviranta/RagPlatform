using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Chunking;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ActiveUser)]
[ApiController]
[Route("api")]
public sealed class DocumentChunkController : ControllerBase
{
    private readonly IDocumentChunkingService _documentChunkingService;
    private readonly ILogger<DocumentChunkController> _logger;

    public DocumentChunkController(
        IDocumentChunkingService documentChunkingService,
        ILogger<DocumentChunkController> logger)
    {
        _documentChunkingService = documentChunkingService;
        _logger = logger;
    }

    /// <summary>
    /// Generates chunk records for a document using the configured chunk size and overlap values.
    /// </summary>
    /// <param name="documentId">The parent document identifier.</param>
    /// <param name="request">The chunk generation options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated chunks for the document.</returns>
    [HttpPost("documents/{documentId:guid}/chunks")]
    [ProducesResponseType(typeof(IEnumerable<DocumentChunk>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<DocumentChunk>>> GenerateChunksAsync(
        Guid documentId,
        [FromBody] DocumentChunkRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var chunks = await _documentChunkingService.GenerateChunksAsync(
                documentId,
                request.ChunkSize,
                request.ChunkOverlap,
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, chunks);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Chunk generation requested for missing document {DocumentId}.", documentId);
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Chunk generation failed for document {DocumentId}.", documentId);
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(DocumentChunkRequest.ChunkSize)] = [ex.Message]
            }));
        }
    }

    /// <summary>
    /// Returns all chunks associated with a document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The chunk records for the selected document.</returns>
    [HttpGet("documents/{documentId:guid}/chunks")]
    [ProducesResponseType(typeof(IEnumerable<DocumentChunk>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<DocumentChunk>>> GetChunksForDocumentAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var documentExists = await _documentChunkingService.GetChunksForDocumentAsync(documentId, cancellationToken);

        if (!documentExists.Any())
        {
            _logger.LogWarning("No chunks found for document {DocumentId}.", documentId);
            return NotFound();
        }

        return Ok(documentExists);
    }

    /// <summary>
    /// Gets a single chunk by identifier.
    /// </summary>
    /// <param name="id">The chunk identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching chunk.</returns>
    [HttpGet("chunks/{id:guid}")]
    [ProducesResponseType(typeof(DocumentChunk), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentChunk>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var chunk = await _documentChunkingService.GetByIdAsync(id, cancellationToken);
        if (chunk is null)
        {
            _logger.LogWarning("Chunk {ChunkId} was not found.", id);
            return NotFound();
        }

        return Ok(chunk);
    }
}
