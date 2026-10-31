namespace RagPlatform.Api.Contracts.Documents;

public sealed class DocumentUploadResponse
{
    public Guid Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
