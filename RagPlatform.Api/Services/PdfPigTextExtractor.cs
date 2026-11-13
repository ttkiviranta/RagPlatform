using System.Text;
using RagPlatform.Api.Contracts.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace RagPlatform.Api.Services;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public PdfTextExtractionResult Extract(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        if (!pdfStream.CanRead)
        {
            throw new ArgumentException("The PDF stream must be readable.", nameof(pdfStream));
        }

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            using var document = PdfDocument.Open(pdfStream);
            if (document.NumberOfPages == 0)
            {
                throw new PermanentPdfFailureException(PdfExtractionFailureKind.NoPages);
            }

            var extractedText = new StringBuilder();
            var pageCount = 0;
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pageCount > 0)
                {
                    extractedText.Append("\n\n");
                }

                extractedText.Append(ContentOrderTextExtractor.GetText(page).Trim());
                pageCount++;
            }

            var text = extractedText.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new PermanentPdfFailureException(PdfExtractionFailureKind.NoExtractableText);
            }

            return new PdfTextExtractionResult(text, pageCount);
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new PermanentPdfFailureException(PdfExtractionFailureKind.Encrypted, exception);
        }
        catch (PdfDocumentFormatException exception)
        {
            throw new PermanentPdfFailureException(PdfExtractionFailureKind.Corrupt, exception);
        }
    }
}
