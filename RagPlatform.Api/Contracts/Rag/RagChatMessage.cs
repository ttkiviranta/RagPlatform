namespace RagPlatform.Api.Contracts.Rag;

public sealed class RagChatMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
