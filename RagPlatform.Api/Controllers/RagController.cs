using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Rag;

namespace RagPlatform.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ActiveUser)]
[ApiController]
[Route("api")]
public sealed class RagController : ControllerBase
{
    private readonly IRagService _ragService;
    private readonly ILogger<RagController> _logger;

    public RagController(
        IRagService ragService,
        ILogger<RagController> logger)
    {
        _ragService = ragService;
        _logger = logger;
    }

    /// <summary>
    /// Executes a retrieval-augmented generation query using the vector search foundation and prompt assembly pipeline.
    /// </summary>
    /// <param name="request">The user question and optional top-k retrieval count.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retrieved chunk metadata, generated context, and prompt payload.</returns>
    [HttpPost("rag/query")]
    [ProducesResponseType(typeof(RagQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<RagQueryResponse>> QueryAsync(
        [FromBody] RagQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var response = await _ragService.QueryAsync(request.Query, request.TopK, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ModelState.AddModelError(ex.ParamName ?? nameof(RagQueryRequest.TopK), ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(RagQueryRequest.Query), ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG query request failed.");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "RAG query failed",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
