namespace RagPlatform.Api.Contracts.Rag;

public sealed class RagQueryResponse
{
    public string Query { get; set; } = string.Empty;

    public IReadOnlyList<RagRetrievedChunk> RetrievedChunks { get; set; } = Array.Empty<RagRetrievedChunk>();

    public string GeneratedContext { get; set; } = string.Empty;

    public string GeneratedPrompt { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
