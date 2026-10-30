using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Models;

public sealed class DocumentUpdateRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}
