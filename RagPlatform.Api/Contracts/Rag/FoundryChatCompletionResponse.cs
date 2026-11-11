namespace RagPlatform.Api.Contracts.Rag;

public sealed class FoundryChatCompletionResponse
{
    public string Answer { get; set; } = string.Empty;

    public int? InputTokenCount { get; set; }

    public int? OutputTokenCount { get; set; }
}
