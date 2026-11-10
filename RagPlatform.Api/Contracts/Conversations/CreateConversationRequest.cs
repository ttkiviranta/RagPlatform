using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Contracts.Conversations;

public sealed class CreateConversationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;
}
