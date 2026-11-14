namespace RagPlatform.Api.Contracts.Documents;

public sealed record DocumentImportStatusResponse(
    Guid DocumentId,
    string? ImportStatus,
    DateTime CreatedAt,
    DateTime UpdatedAt);