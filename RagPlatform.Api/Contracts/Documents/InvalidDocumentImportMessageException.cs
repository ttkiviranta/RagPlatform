namespace RagPlatform.Api.Contracts.Documents;

public enum DocumentImportMessageFailureKind
{
    EmptyBody,
    MalformedJson,
    MissingPayload,
    InvalidRequiredFields,
    DocumentNotFound,
    DocumentMismatch
}

public sealed class InvalidDocumentImportMessageException : Exception
{
    public InvalidDocumentImportMessageException(
        DocumentImportMessageFailureKind failureKind,
        Exception? innerException = null)
        : base("The document import message is invalid.", innerException)
    {
        FailureKind = failureKind;
    }

    public DocumentImportMessageFailureKind FailureKind { get; }
}