using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace RagPlatform.Api.Contracts.Documents;

public sealed class DocumentUploadRequest
{
    [Required]
    public IFormFile File { get; set; } = default!;
}
