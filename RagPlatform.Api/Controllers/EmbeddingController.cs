using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Embeddings;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ActiveUser)]
[ApiController]
[Route("api")]
public sealed class EmbeddingController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingController> _logger;

    public EmbeddingController(
        IEmbeddingService embeddingService,
        ILogger<EmbeddingController> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Creates an embedding metadata record for a chunk. The actual vector generation is abstracted behind the embedding provider contract.
    /// </summary>
    /// <param name="chunkId">The chunk identifier.</param>
    /// <param name="request">Optional embedding request metadata.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created embedding metadata record.</returns>
    [HttpPost("chunks/{chunkId:guid}/embeddings")]
    [ProducesResponseType(typeof(DocumentEmbedding), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentEmbedding>> GenerateEmbeddingAsync(
        Guid chunkId,
        [FromBody] DocumentEmbeddingRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                chunkId,
                request?.ModelName,
                cancellationToken);

            return StatusCode(StatusCodes.Status201Created, embedding);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Embedding generation requested for missing chunk {ChunkId}.", chunkId);
            return NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Gets all embedding metadata records associated with a chunk.
    /// </summary>
    /// <param name="chunkId">The chunk identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The embedding metadata records for the chunk.</returns>
    [HttpGet("chunks/{chunkId:guid}/embeddings")]
    [ProducesResponseType(typeof(IEnumerable<DocumentEmbedding>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<DocumentEmbedding>>> GetEmbeddingsForChunkAsync(
        Guid chunkId,
        CancellationToken cancellationToken)
    {
        var embeddings = await _embeddingService.GetEmbeddingsForChunkAsync(chunkId, cancellationToken);

        if (!embeddings.Any())
        {
            _logger.LogWarning("No embeddings found for chunk {ChunkId}.", chunkId);
            return NotFound();
        }

        return Ok(embeddings);
    }

    [HttpGet("embeddings/{id:guid}")]
    [ProducesResponseType(typeof(DocumentEmbedding), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentEmbedding>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var embedding = await _embeddingService.GetByIdAsync(id, cancellationToken);
        if (embedding is null)
        {
            _logger.LogWarning("Embedding {EmbeddingId} was not found.", id);
            return NotFound();
        }

        return Ok(embedding);
    }
}
