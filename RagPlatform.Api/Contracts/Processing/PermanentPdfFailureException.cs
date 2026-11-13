namespace RagPlatform.Api.Contracts.Processing;

public enum PdfExtractionFailureKind
{
    Encrypted,
    Corrupt,
    NoPages,
    NoExtractableText
}

public sealed class PermanentPdfFailureException : Exception
{
    public PermanentPdfFailureException(
        PdfExtractionFailureKind failureKind,
        Exception? innerException = null)
        : base(GetSafeMessage(failureKind), innerException)
    {
        FailureKind = failureKind;
    }

    public PdfExtractionFailureKind FailureKind { get; }

    private static string GetSafeMessage(PdfExtractionFailureKind failureKind)
    {
        return failureKind switch
        {
            PdfExtractionFailureKind.Encrypted => "Encrypted PDF documents are not supported.",
            PdfExtractionFailureKind.Corrupt => "The PDF is corrupt or unreadable.",
            PdfExtractionFailureKind.NoPages => "The PDF contains no pages.",
            PdfExtractionFailureKind.NoExtractableText => "The PDF contains no extractable text.",
            _ => throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, null)
        };
    }
}
