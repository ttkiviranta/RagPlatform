using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class VectorSearchController : ControllerBase
{
    private readonly IVectorSearchService _vectorSearchService;
    private readonly ILogger<VectorSearchController> _logger;

    public VectorSearchController(
        IVectorSearchService vectorSearchService,
        ILogger<VectorSearchController> logger)
    {
        _vectorSearchService = vectorSearchService;
        _logger = logger;
    }

    /// <summary>
    /// Performs a vector similarity search against the stored chunk embeddings.
    /// </summary>
    /// <param name="request">The search request containing the query text and top-k selection.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The most relevant document chunks based on the query vector similarity.</returns>
    [HttpPost("search/vector")]
    [ProducesResponseType(typeof(VectorSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VectorSearchResponse>> SearchAsync(
        [FromBody] VectorSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _vectorSearchService.SearchAsync(request.Query, request.TopK, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? nameof(VectorSearchRequest.TopK), ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(VectorSearchRequest.Query), ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search request failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Vector search failed",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
