namespace RagPlatform.Api.Options;

public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public bool UseHybridSearch { get; set; } = false;

    public bool UseReranking { get; set; } = false;

    public bool AllowModelKnowledgeFallback { get; set; } = false;
}
