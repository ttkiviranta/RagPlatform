using System.Text;
using RagPlatform.Api.Contracts.Processing;
using RagPlatform.Api.Services;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
public sealed class PdfPigTextExtractorTests
{
    private readonly PdfPigTextExtractor _extractor = new();

    [TestMethod]
    public void Extract_ValidPdf_ReturnsTextAndPageCount()
    {
        using var stream = new MemoryStream(CreatePdf("Readable PDF text"));

        var result = _extractor.Extract(stream);

        Assert.AreEqual(1, result.PageCount);
        Assert.Contains("Readable PDF text", result.Text);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public void Extract_EncryptedPdf_ThrowsPermanentFailureAndPreservesParserException()
    {
        using var stream = new MemoryStream(CreateEncryptedPdf());

        var exception = Assert.ThrowsExactly<PermanentPdfFailureException>(
            () => _extractor.Extract(stream));

        Assert.AreEqual(PdfExtractionFailureKind.Encrypted, exception.FailureKind);
        Assert.IsInstanceOfType<PdfDocumentEncryptedException>(exception.InnerException);
        Assert.AreEqual("Encrypted PDF documents are not supported.", exception.Message);
    }

    [TestMethod]
    public void Extract_CorruptPdf_ThrowsPermanentFailureAndPreservesParserException()
    {
        using var stream = new MemoryStream("This is not a PDF."u8.ToArray());

        var exception = Assert.ThrowsExactly<PermanentPdfFailureException>(
            () => _extractor.Extract(stream));

        Assert.AreEqual(PdfExtractionFailureKind.Corrupt, exception.FailureKind);
        Assert.IsInstanceOfType<PdfDocumentFormatException>(exception.InnerException);
        Assert.AreEqual("The PDF is corrupt or unreadable.", exception.Message);
    }

    [TestMethod]
    public void Extract_WhitespaceOnlyPdf_ThrowsPermanentFailure()
    {
        using var stream = new MemoryStream(CreatePdf("   \t"));

        var exception = Assert.ThrowsExactly<PermanentPdfFailureException>(
            () => _extractor.Extract(stream));

        Assert.AreEqual(PdfExtractionFailureKind.NoExtractableText, exception.FailureKind);
        Assert.AreEqual("The PDF contains no extractable text.", exception.Message);
    }

    [TestMethod]
    public void Extract_NoPagePdf_ThrowsPermanentFailure()
    {
        using var stream = new MemoryStream(CreatePdfWithoutPages());

        var exception = Assert.ThrowsExactly<PermanentPdfFailureException>(
            () => _extractor.Extract(stream));

        Assert.AreEqual(PdfExtractionFailureKind.NoPages, exception.FailureKind);
        Assert.AreEqual("The PDF contains no pages.", exception.Message);
    }

    [TestMethod]
    public void Extract_UnexpectedStreamException_PropagatesWithoutPermanentClassification()
    {
        using var stream = new FailingReadStream();

        var exception = Assert.ThrowsExactly<InvalidOperationException>(
            () => _extractor.Extract(stream));

        Assert.AreEqual("The test stream failed.", exception.Message);
    }

    [TestMethod]
    public void Extract_Cancellation_PropagatesWithoutPermanentClassification()
    {
        using var stream = new MemoryStream(CreatePdf("Readable PDF text"));
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(
            () => _extractor.Extract(stream, cancellationSource.Token));
    }

    private static byte[] CreatePdf(string text)
    {
        using var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText(text, 12, new PdfPoint(25, 700), font);
        return builder.Build();
    }

    private static byte[] CreatePdfWithoutPages()
    {
        using var builder = new PdfDocumentBuilder();
        return builder.Build();
    }

    private static byte[] CreateEncryptedPdf()
    {
        using var stream = new MemoryStream();
        var objectOffsets = new long[5];

        WriteAscii(stream, "%PDF-1.4\n");
        WriteObject(stream, objectOffsets, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        WriteObject(stream, objectOffsets, 2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        WriteObject(stream, objectOffsets, 3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        WriteObject(
            stream,
            objectOffsets,
            4,
            $"<< /Filter /Standard /V 1 /R 2 /O <{new string('0', 64)}> /U <{new string('0', 64)}> /P -4 >>");

        var crossReferenceOffset = stream.Position;
        WriteAscii(stream, "xref\n0 5\n0000000000 65535 f \n");
        for (var objectNumber = 1; objectNumber < objectOffsets.Length; objectNumber++)
        {
            WriteAscii(stream, $"{objectOffsets[objectNumber]:D10} 00000 n \n");
        }

        WriteAscii(
            stream,
            $"trailer\n<< /Size 5 /Root 1 0 R /Encrypt 4 0 R /ID [<{new string('0', 32)}><{new string('0', 32)}>] >>\nstartxref\n{crossReferenceOffset}\n%%EOF\n");
        return stream.ToArray();
    }

    private static void WriteObject(MemoryStream stream, long[] objectOffsets, int objectNumber, string contents)
    {
        objectOffsets[objectNumber] = stream.Position;
        WriteAscii(stream, $"{objectNumber} 0 obj\n{contents}\nendobj\n");
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes);
    }

    private sealed class FailingReadStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => 1;

        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("The test stream failed.");
        }

        public override int Read(Span<byte> buffer)
        {
            throw new InvalidOperationException("The test stream failed.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
