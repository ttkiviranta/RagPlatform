using System.Text.Json.Serialization;

namespace RagPlatform.Api.Models;

public sealed class DocumentTag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }

    [JsonIgnore]
    public Document Document { get; set; } = default!;

    public required string Name { get; set; }
}
