using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Search;

namespace RagPlatform.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ActiveUser)]
[ApiController]
[Route("api/search")]
public sealed class SearchController : ControllerBase
{
    private readonly IFullTextSearchService _fullTextSearchService;
    private readonly IHybridSearchService _hybridSearchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IFullTextSearchService fullTextSearchService,
        IHybridSearchService hybridSearchService,
        ILogger<SearchController> logger)
    {
        _fullTextSearchService = fullTextSearchService;
        _hybridSearchService = hybridSearchService;
        _logger = logger;
    }

    /// <summary>Searches accessible document chunks using PostgreSQL full-text search.</summary>
    [HttpPost("fulltext")]
    [ProducesResponseType(typeof(IReadOnlyList<FullTextSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<IReadOnlyList<FullTextSearchResult>>> FullTextAsync(
        [FromBody] VectorSearchRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(request, _fullTextSearchService.SearchAsync, "Full-text", cancellationToken);
    }

    /// <summary>Searches accessible document chunks using hybrid ranking.</summary>
    [HttpPost("hybrid")]
    [ProducesResponseType(typeof(IReadOnlyList<HybridSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public Task<ActionResult<IReadOnlyList<HybridSearchResult>>> HybridAsync(
        [FromBody] VectorSearchRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(request, _hybridSearchService.SearchAsync, "Hybrid", cancellationToken);
    }

    private async Task<ActionResult<IReadOnlyList<TResult>>> ExecuteAsync<TResult>(
        VectorSearchRequest request,
        Func<string, int, CancellationToken, Guid?, Task<IReadOnlyList<TResult>>> search,
        string searchName,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var results = await search(request.Query, request.TopK, cancellationToken, request.DocumentId);
            return Ok(results);
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{SearchName} search request failed.", searchName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = $"{searchName} search failed",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
