namespace RagPlatform.Api.Services;

public sealed class DocumentImportPublishException : Exception
{
    public DocumentImportPublishException(Guid documentId, bool failureStatusPersisted, Exception innerException)
        : base("The document import message could not be published.", innerException)
    {
        DocumentId = documentId;
        FailureStatusPersisted = failureStatusPersisted;
    }

    public Guid DocumentId { get; }

    public bool FailureStatusPersisted { get; }
}
