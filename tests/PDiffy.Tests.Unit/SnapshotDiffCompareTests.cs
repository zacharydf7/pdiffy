using PDiffy.Tests.Unit.Docx;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public sealed class SnapshotDiffCompareTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pdiffy-compare-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Compare_ShouldCountTheLaterDocumentsNewContentAsAdded_WhenGivenTwoPdfs()
    {
        var paragraph = new PdfStructureElement { Type = "P", FirstPage = 1, Text = "Go with a guide" };
        var before = Documents.Empty() with { Structure = [paragraph] };
        var after = before with { Structure = [paragraph, paragraph with { Text = "Bring water" }] };

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal((ChangeKind.Added, PdfSection.Structure), (change.Kind, change.PdfSection));
    }

    [Fact]
    public void Compare_ShouldCountTheLaterDocumentsNewContentAsAdded_WhenGivenTwoWordDocuments()
    {
        var before = new TestDocx { Body = "<w:p><w:r><w:t>Go with a guide</w:t></w:r></w:p>" }.Load();
        var after = new TestDocx
        {
            Body = "<w:p><w:r><w:t>Go with a guide</w:t></w:r></w:p><w:p><w:r><w:t>Bring water</w:t></w:r></w:p>",
        }.Load();

        var change = Assert.Single(SnapshotDiff.Compare(before, after).Changes);

        Assert.Equal((ChangeKind.Added, DocxSection.Body), (change.Kind, change.DocxSection));
    }

    [Fact]
    public void CompareFiles_ShouldReadEachPdf_WhenGivenTwoPdfFiles()
    {
        var (before, after) = (WritePdf("before.pdf", "Go with a guide"), WritePdf("after.pdf", "Go without a guide"));

        var diff = SnapshotDiff.CompareFiles(before, after);

        Assert.Contains(diff.Changes,
            change => change.Kind == ChangeKind.TextChanged && change.PdfSection == PdfSection.Structure);
    }

    [Fact]
    public void CompareFiles_ShouldReadEachWordDocument_WhenGivenTwoDocxFiles()
    {
        var before = WriteDocx("before.docx", "<w:p><w:r><w:t>Go with a guide</w:t></w:r></w:p>");
        var after = WriteDocx("after.docx", "<w:p><w:r><w:t>Go without a guide</w:t></w:r></w:p>");

        var change = Assert.Single(SnapshotDiff.CompareFiles(before, after).Changes);

        Assert.Equal((ChangeKind.TextChanged, DocxSection.Body), (change.Kind, change.DocxSection));
    }

    [Fact]
    public void CompareFiles_ShouldCompareTheTextsAsSnapshots_WhenGivenFilesThatAreNotDocuments()
    {
        var before = Write("before.json", "{\n  \"a\": 1\n}\n"u8.ToArray());
        var after = Write("after.json", "{\n  \"a\": 2\n}\n"u8.ToArray());

        var change = Assert.Single(SnapshotDiff.CompareFiles(before, after).Changes);

        Assert.Equal((ChangeKind.TextChanged, "line 2"), (change.Kind, change.Path));
    }

    [Fact]
    public void CompareFiles_ShouldApplyThePdfOptions_WhenGivenPdfFiles()
    {
        var (before, after) = (WritePdf("before.pdf", "Go with a guide"), WritePdf("after.pdf", "Go without a guide"));

        var diff = SnapshotDiff.CompareFiles(before, after,
            configurePdf: options => options.Ignore(PdfSection.Structure));

        Assert.DoesNotContain(diff.Changes, change => change.PdfSection == PdfSection.Structure);
    }

    [Fact]
    public void CompareFiles_ShouldApplyTheWordOptions_WhenGivenDocxFiles()
    {
        var before = WriteDocx("before.docx", "<w:p><w:r><w:t>Go with a guide</w:t></w:r></w:p>");
        var after = WriteDocx("after.docx", "<w:p><w:r><w:t>Go without a guide</w:t></w:r></w:p>");

        var diff = SnapshotDiff.CompareFiles(before, after,
            configureDocx: options => options.Ignore(DocxSection.Body));

        Assert.True(diff.IsEmpty);
    }

    private string WritePdf(string name, string text)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, text);
        pdf.Structure(new TestElement("P", 0));
        return Write(name, pdf.Build());
    }

    private string WriteDocx(string name, string body) => Write(name, new TestDocx { Body = body }.Build().ToArray());

    private string Write(string name, byte[] contents)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, contents);
        return path;
    }
}
