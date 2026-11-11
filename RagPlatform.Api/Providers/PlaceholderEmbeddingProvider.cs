using RagPlatform.Api.Contracts.Embeddings;

namespace RagPlatform.Api.Providers;

public sealed class PlaceholderEmbeddingProvider : IEmbeddingProvider
{
    public string ProviderName => "PlaceholderEmbeddingProvider";

    public string ModelName => "placeholder-embedding-model";

    public Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string content,
        string? modelName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult<IReadOnlyList<float>>(Array.Empty<float>());
        }

        const int defaultDimensions = 1536;
        var vector = new float[defaultDimensions];

        for (var i = 0; i < defaultDimensions; i++)
        {
            vector[i] = (float)((Math.Sin((i + 1) * (content.Length + 1) / 10d) + 1) / 2d);
        }

        return Task.FromResult<IReadOnlyList<float>>(vector);
    }
}
