namespace RagPlatform.Api.Contracts.Processing;

public interface IPdfTextExtractor
{
    PdfTextExtractionResult Extract(Stream pdfStream, CancellationToken cancellationToken = default);
}
