using Azure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagPlatform.Api.Authorization;
using RagPlatform.Api.Contracts.Documents;
using RagPlatform.Api.Services;

namespace RagPlatform.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ActiveUser)]
[ApiController]
[Route("api/documents")]
public sealed class DocumentImportController : ControllerBase
{
    private readonly DocumentImportService _documentImportService;
    private readonly ILogger<DocumentImportController> _logger;

    public DocumentImportController(
        DocumentImportService documentImportService,
        ILogger<DocumentImportController> logger)
    {
        _documentImportService = documentImportService;
        _logger = logger;
    }

    /// <summary>
    /// Uploads a PDF to temporary Blob Storage and queues its document record for import.
    /// </summary>
    /// <param name="request">The PDF upload.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created document identifier and its initial import status.</returns>
    [HttpPost("upload/pdf")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PdfDocumentUploadRequest.MaximumRequestBodySizeBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = PdfDocumentUploadRequest.MaximumRequestBodySizeBytes)]
    [ProducesResponseType(typeof(DocumentImportResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DocumentImportResponse>> UploadPdfAsync(
        [FromForm] PdfDocumentUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var document = await _documentImportService.UploadPdfAsync(request.File, cancellationToken);
            var response = new DocumentImportResponse(
                document.Id,
                document.ImportStatus!.Value.ToString());

            return Accepted(response);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(PdfDocumentUploadRequest.File), exception.Message);
            return ValidationProblem(ModelState);
        }
        catch (DocumentImportPublishException exception)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "The document import message could not be published.",
                Detail = exception.FailureStatusPersisted
                    ? "The document was saved and marked Failed."
                    : "The document was saved, but its Failed status could not be persisted."
            };
            problem.Extensions["documentId"] = exception.DocumentId;
            problem.Extensions["status"] = exception.FailureStatusPersisted ? "Failed" : "Unknown";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, problem);
        }
        catch (RequestFailedException exception)
        {
            _logger.LogError(exception, "Blob staging failed for a PDF document upload.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Temporary document storage is unavailable.");
        }
    }
}
