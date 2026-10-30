using PDiffy.Tests.Unit.Docx;
using PDiffy.Tests.Unit.Pdfs;

#pragma warning disable CS0618 // The obsolete forwards are what these tests check.

namespace PDiffy.Tests.Unit;

[Collection(nameof(SnapshotFolderTests))]
public sealed class ObsoleteSnapshotTests : SnapshotFolderTests
{
    [Fact]
    public void DefaultOptions_ShouldSetTheNewDefaults_WhenSetThroughTheOldMembers()
    {
        Action<PdfSnapshotOptions> pdf = options => options.Use(PdfProfile.Content);
        Action<DocxSnapshotOptions> docx = options => options.Use(DocxProfile.Content);
        Action<TextSnapshotOptions> text = options => options.ScrubDates();

        (PdfSnapshot.DefaultOptions, DocxSnapshot.DefaultOptions, Snapshot.DefaultOptions) = (pdf, docx, text);

        Assert.Equal((pdf, docx, text),
            (Snapshot.DefaultPdfOptions, Snapshot.DefaultDocxOptions, Snapshot.DefaultTextOptions));
    }

    [Fact]
    public async Task VerifyAsync_ShouldNameTheSnapshotAfterTheCallingTest_WhenCalledThroughTheOldMembers()
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, "Go with a guide");
        pdf.Structure(new TestElement("P", 0));

        await Assert.ThrowsAsync<SnapshotException>(() => PdfSnapshot.VerifyAsync(pdf.Build()));
        await Assert.ThrowsAsync<SnapshotException>(() => DocxSnapshot.VerifyAsync(new TestDocx().Build()));
        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyAsync("Go with a guide"));

        const string name = nameof(ObsoleteSnapshotTests) + "."
            + nameof(VerifyAsync_ShouldNameTheSnapshotAfterTheCallingTest_WhenCalledThroughTheOldMembers);
        Assert.All(["pdfua.txt", "docx.txt", "txt"],
            extension => Assert.True(File.Exists(Path.Combine(Folder, $"{name}.received.{extension}")), extension));
    }
}
