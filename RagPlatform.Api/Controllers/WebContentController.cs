using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Contracts.Web;
using RagPlatform.Api.Models;

namespace RagPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/web")]
public sealed class WebContentController : ControllerBase
{
    private readonly IWebContentService _webContentService;
    private readonly ILogger<WebContentController> _logger;

    public WebContentController(IWebContentService webContentService, ILogger<WebContentController> logger)
    {
        _webContentService = webContentService;
        _logger = logger;
    }

    /// <summary>
    /// Imports public web content by URL and stores the extracted content as a document.
    /// </summary>
    /// <param name="request">The URL to import.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The imported document.</returns>
    [HttpPost("import")]
    [ProducesResponseType(typeof(Document), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Document>> ImportAsync([FromBody] WebContentImportRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var document = await _webContentService.ImportAsync(request.Url, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, document);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(WebContentImportRequest.Url), ex.Message);
            return ValidationProblem(ModelState);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Web content import failed for {Url}.", request.Url);
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(WebContentImportRequest.Url)] = [ex.Message]
            }));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Unable to fetch web content for {Url}.", request.Url);
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [nameof(WebContentImportRequest.Url)] = ["The remote page could not be fetched successfully."]
            }));
        }
    }
}