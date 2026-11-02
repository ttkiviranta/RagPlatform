using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Contracts.Web;

public sealed class WebContentImportRequest
{
    [Required]
    [Url]
    public string Url { get; set; } = string.Empty;
}