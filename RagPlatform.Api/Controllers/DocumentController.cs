using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IDocumentService documentService, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all documents.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of persisted documents.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Document>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Document>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var documents = await _documentService.GetAllAsync(cancellationToken);
        return Ok(documents);
    }

    /// <summary>
    /// Gets a specific document by identifier.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching document.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Document>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var document = await _documentService.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} was not found.", id);
            return NotFound();
        }

        return Ok(document);
    }

    /// <summary>
    /// Creates a new document.
    /// </summary>
    /// <param name="request">The document to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created document.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Document), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Document>> CreateAsync([FromBody] DocumentCreateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var document = await _documentService.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, document);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Updates an existing document.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="request">The updated document values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated document.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Document), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Document>> UpdateAsync(Guid id, [FromBody] DocumentUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var document = await _documentService.UpdateAsync(id, request, cancellationToken);
            if (document is null)
            {
                _logger.LogWarning("Document {DocumentId} was not found for update.", id);
                return NotFound();
            }

            return Ok(document);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>
    /// Deletes a document by identifier.
    /// </summary>
    /// <param name="id">The document identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>No content when the document is deleted.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _documentService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            _logger.LogWarning("Document {DocumentId} was not found for deletion.", id);
            return NotFound();
        }

        return NoContent();
    }
}
