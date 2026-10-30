using PDiffy.Tests.Unit.Docx;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public class InputStreamTests
{
    [Fact]
    public void Load_ShouldReadFromTheStartAndPutThePositionBack_WhenTheGeneratorLeftThePdfStreamAtTheEnd()
    {
        var pdf = new MemoryStream(Pdf());
        pdf.Position = pdf.Length;

        var first = PdfUaDocument.Load(pdf).ToSnapshot();
        var second = PdfUaDocument.Load(pdf).ToSnapshot();

        Assert.Equal(pdf.Length, pdf.Position);
        Assert.Equal(PdfUaDocument.Load(Pdf()).ToSnapshot(), first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Load_ShouldReadFromTheStartAndPutThePositionBack_WhenTheGeneratorLeftTheDocxStreamAtTheEnd()
    {
        var docx = Docx();
        docx.Position = docx.Length;

        var snapshot = DocxDocument.Load(docx).ToSnapshot();

        Assert.Equal(docx.Length, docx.Position);
        Assert.Equal(DocxDocument.Load(Docx()).ToSnapshot(), snapshot);
    }

    [Fact]
    public void Load_ShouldReadTheDocument_WhenTheStreamCannotSeek()
    {
        var pdf = PdfUaDocument.Load(new ForwardOnlyStream(Pdf()));
        var docx = DocxDocument.Load(new ForwardOnlyStream(Docx().ToArray()));

        Assert.Equal(PdfUaDocument.Load(Pdf()).ToSnapshot(), pdf.ToSnapshot());
        Assert.Equal(DocxDocument.Load(Docx()).ToSnapshot(), docx.ToSnapshot());
    }

    [Fact]
    public void Load_ShouldGiveTheSameDocument_WhenGivenTheBytesInsteadOfAStream()
    {
        Assert.Equal(PdfUaDocument.Load(Pdf()).ToSnapshot(), PdfUaDocument.Load(new MemoryStream(Pdf())).ToSnapshot());
        Assert.Equal(DocxDocument.Load(Docx().ToArray()).ToSnapshot(), DocxDocument.Load(Docx()).ToSnapshot());
    }

    [Theory]
    [InlineData("", "The stream is empty, so there's no PDF to read.")]
    [InlineData("PK\u0003\u0004 rest of a zip", "This is a ZIP file, not a PDF.")]
    [InlineData("\n<!DOCTYPE html><html><body>Server error</body></html>", "This is a web page, not a PDF.")]
    [InlineData("<HTML><body>Not found</body></HTML>", "This is a web page, not a PDF.")]
    [InlineData("\u00ef\u00bb\u00bf<html><body>Not found</body></html>", "This is a web page, not a PDF.")]
    public void Load_ShouldSayWhatTheInputIs_WhenAPdfIsExpectedButTheStreamHoldsSomethingElse(string content, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => PdfUaDocument.Load(Bytes(content)));

        Assert.StartsWith(message, error.Message);
    }

    [Theory]
    [InlineData("", "The stream is empty, so there's no Word document to read.")]
    [InlineData("%PDF-1.7\n", "This is a PDF, not a Word document.")]
    [InlineData("<html><body>Server error</body></html>", "This is a web page, not a Word document.")]
    public void Load_ShouldSayWhatTheInputIs_WhenADocxIsExpectedButTheStreamHoldsSomethingElse(string content, string message)
    {
        var error = Assert.Throws<InvalidDataException>(() => DocxDocument.Load(Bytes(content)));

        Assert.StartsWith(message, error.Message);
    }

    private static byte[] Bytes(string content) => System.Text.Encoding.Latin1.GetBytes(content);

    private static byte[] Pdf()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Trailhead");
        pdf.Structure(new TestElement("P", 0));
        return pdf.Build();
    }

    private static MemoryStream Docx() => new TestDocx { Body = "<w:p><w:r><w:t>Trailhead</w:t></w:r></w:p>" }.Build();

    // A stream that can only be read forward, like a network response.
    private sealed class ForwardOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
