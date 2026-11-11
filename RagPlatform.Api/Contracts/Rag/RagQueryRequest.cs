using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Contracts.Rag;

public sealed class RagQueryRequest
{
    [Required]
    [StringLength(2000)]
    public string Query { get; set; } = string.Empty;

    [Range(1, 20)]
    public int TopK { get; set; } = 5;

    public Guid? ConversationId { get; set; }
}
