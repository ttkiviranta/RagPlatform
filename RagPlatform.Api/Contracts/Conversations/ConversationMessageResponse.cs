using RagPlatform.Api.Contracts.Rag;

namespace RagPlatform.Api.Contracts.Conversations;

public sealed class ConversationMessageResponse
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public IReadOnlyList<RagCitationDto> Citations { get; set; } = Array.Empty<RagCitationDto>();

    public DateTime CreatedAtUtc { get; set; }
}
