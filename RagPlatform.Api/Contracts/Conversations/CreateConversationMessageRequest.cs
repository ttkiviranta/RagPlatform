using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Contracts.Conversations;

public sealed class CreateConversationMessageRequest
{
    [Required]
    public string Content { get; set; } = string.Empty;
}
