using System.ComponentModel.DataAnnotations;

namespace RagPlatform.Api.Contracts.Embeddings;

public sealed class DocumentEmbeddingRequest
{
    [StringLength(200)]
    public string? ModelName { get; set; }
}
