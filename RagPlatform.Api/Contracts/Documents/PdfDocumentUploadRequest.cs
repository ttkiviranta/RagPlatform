using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RagPlatform.Api.Contracts.Documents;

public sealed class PdfDocumentUploadRequest
{
    public const long MaximumFileSizeBytes = 25L * 1024 * 1024;
    public const long MaximumRequestBodySizeBytes = MaximumFileSizeBytes + 1024L * 1024;

    [Required]
    public IFormFile File { get; set; } = default!;
}
