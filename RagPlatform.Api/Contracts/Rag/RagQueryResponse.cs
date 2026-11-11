namespace RagPlatform.Api.Contracts.Rag;

public sealed class RagQueryResponse
{
    public string Query { get; set; } = string.Empty;

    public string Answer { get; set; } = string.Empty;

    public IReadOnlyList<RagRetrievedChunk> Sources { get; set; } = Array.Empty<RagRetrievedChunk>();

    public Guid? ConversationId { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
