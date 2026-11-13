namespace RagPlatform.Api.Contracts.Documents;

public sealed record DocumentImportMessage(
    Guid DocumentId,
    string TenantId,
    Guid OwnerUserId,
    string StagingBlobKey,
    DateTime CreatedAtUtc);
