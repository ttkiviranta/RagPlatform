using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RagPlatform.Api.Swagger;

public sealed class DocumentPdfUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!string.Equals(
                context.ApiDescription.RelativePath,
                "api/documents/upload/pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        operation.Tags = new List<OpenApiTag>
        {
            new() { Name = "Document" }
        };
        operation.Summary = "Uploads a PDF document for asynchronous knowledge-ingestion processing.";
        operation.Description = """
            Uploads PDF files only.

            - Stores the PDF temporarily in Blob Storage.
            - Creates a document with `Queued` status.
            - Publishes a Service Bus import message.
            - Returns `202 Accepted`.
            """;
    }
}
