using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Contracts.Search;

public sealed class VectorSearchRequest
{
    [Required]
    [StringLength(2000)]
    public string Query { get; set; } = string.Empty;

    [Range(1, 50)]
    public int TopK { get; set; } = 5;
}
