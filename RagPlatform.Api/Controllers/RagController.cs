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
    /// Executes a grounded retrieval-augmented generation query.
    /// </summary>
    /// <param name="request">The user question, retrieval count, and optional owned conversation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The generated answer and references to its retrieved sources.</returns>
    [HttpPost("rag/query")]
    [ProducesResponseType(typeof(RagQueryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
            var response = await _ragService.QueryAsync(
                request.Query,
                request.TopK,
                cancellationToken,
                request.ConversationId);
            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
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
