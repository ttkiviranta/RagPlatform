namespace RagPlatform.Api.Options;

public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    public string Endpoint { get; set; } = string.Empty;

    public string ChatDeployment { get; set; } = string.Empty;

    public string EmbeddingDeployment { get; set; } = string.Empty;

    public int EmbeddingDimensions { get; set; } = 1536;

    public int MaxOutputTokens { get; set; } = 800;
}
