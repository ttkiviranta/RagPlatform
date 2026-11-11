namespace RagPlatform.Api.Contracts.Embeddings;

public interface IEmbeddingProvider
{
    string ProviderName { get; }

    string ModelName { get; }

    Task<IReadOnlyList<float>> GenerateEmbeddingAsync(
        string content,
        string? modelName = null,
        CancellationToken cancellationToken = default);
}
