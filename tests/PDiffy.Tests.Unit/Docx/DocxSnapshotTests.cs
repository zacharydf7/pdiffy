using System.Text.RegularExpressions;

namespace PDiffy.Tests.Unit.Docx;

[Collection(nameof(SnapshotFolderTests))]
public sealed class DocxSnapshotTests : SnapshotFolderTests
{
    [Fact]
    public async Task VerifyAsync_ShouldWriteTheReceivedSnapshotAndFail_WhenThereIsNoVerifiedFile()
    {
        var error = await Assert.ThrowsAsync<SnapshotException>(() => DocxSnapshot.VerifyAsync(Docx("Hello"), "Letter"));

        Assert.StartsWith("New DOCX snapshot.", error.Message);
        Assert.Equal(DocxDocument.Load(Docx("Hello")).ToSnapshot(), File.ReadAllText(Path.Combine(Folder, "Letter.received.docx.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldFailWithTheChangedWords_WhenTheDocumentChanged()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Letter.verified.docx.txt"), DocxDocument.Load(Docx("Take with food")).ToSnapshot());

        var error = await Assert.ThrowsAsync<SnapshotException>(() => DocxSnapshot.VerifyAsync(Docx("Take after food"), "Letter"));

        Assert.StartsWith("The DOCX's snapshot differs from the verified one.", error.Message);
        Assert.Contains("\"Take [-with-] {+after+} food\"", error.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldPass_WhenTheDocumentMatchesItsAcceptedSnapshot()
    {
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");
        await DocxSnapshot.VerifyAsync(Docx("Hello"), "Letter", directory: Path.Combine(Folder, "other"));
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, null);

        await DocxSnapshot.VerifyAsync(Docx("Hello"), "Letter", directory: Path.Combine(Folder, "other"));

        Assert.True(File.Exists(Path.Combine(Folder, "other", "Letter.verified.docx.txt")));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutIgnoredSectionsAndScrubText_WhenOptionsSaySo()
    {
        var snapshot = DocxDocument.Load(Docx("Sent 2026-10-28 to order 1234"))
            .ToSnapshot(o => o.Ignore(DocxSection.Styles, DocxSection.Sections).ScrubDates().ScrubText(new Regex(@"order \d+"), "order N"));

        Assert.StartsWith("# docx-snapshot v1\n# ignored sections: sections, styles\n", snapshot);
        Assert.Contains("sections\n  (ignored)\nstyles\n  (ignored)\n", snapshot);
        Assert.Contains("  P \"Sent Date_1 to order N\"\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldListWhatTheProfileLeavesOut_WhenTheContentProfileIsUsed()
    {
        var snapshot = DocxDocument.Load(Docx("Text")).ToSnapshot(o => o.Use(DocxProfile.Content));

        Assert.StartsWith("# docx-snapshot v1\n# ignored sections: sections, styles\n"
            + "# ignored fields: fonts, font-sizes, colors, spacing, indents\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutNothing_WhenTheStrictProfileIsUsed()
    {
        var document = DocxDocument.Load(Docx("Text"));

        Assert.Equal(document.ToSnapshot(), document.ToSnapshot(o => o.Use(DocxProfile.Strict)));
    }

    [Fact]
    public void ToSnapshot_ShouldLeaveOutWhatIgnoreAdds_WhenAProfileIsUsedWithIgnore()
    {
        var snapshot = DocxDocument.Load(Docx("Text")).ToSnapshot(o => o.Use(DocxProfile.Content).Ignore(DocxDetail.ImageHashes));

        Assert.StartsWith("# docx-snapshot v1\n# ignored sections: sections, styles\n"
            + "# ignored fields: image-hashes, fonts, font-sizes, colors, spacing, indents\n", snapshot);
    }

    [Fact]
    public void Use_ShouldThrow_WhenGivenAValueThatIsNotAProfile()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocxSnapshotOptions().Use((DocxProfile)99));
    }

    [Fact]
    public void CompareTo_ShouldFindNoChanges_WhenOnlyAnIgnoredSectionDiffers()
    {
        var before = DocxDocument.Load(Docx("Before"));
        var after = DocxDocument.Load(Docx("After"));

        Assert.True(before.CompareTo(after, o => o.Ignore(DocxSection.Body)).IsEmpty);
    }

    [Fact]
    public void ToSnapshot_ShouldGiveTheSameSnapshot_WhenOnlyTheLengthOfAScrubbedDateChanges()
    {
        static string Snapshot(string date) => new TestDocx
        {
            Body = $"""
                <w:p><w:r><w:t xml:space="preserve">Printed {date}, see </w:t></w:r><w:hyperlink r:id="site"><w:r><w:t>example.com</w:t></w:r></w:hyperlink>
                  <w:r><w:t xml:space="preserve"> or </w:t></w:r><w:hyperlink r:id="site"><w:r><w:t>{date}</w:t></w:r></w:hyperlink></w:p>
                """,
        }.WithLink("site", "https://example.com/orders/0f8fad5b-d9cb-469f-a165-70867728950e").Load().ToSnapshot(o => o.ScrubDates().ScrubGuids());

        var snapshot = Snapshot("9/5/2026");

        Assert.Equal(snapshot, Snapshot("10/28/2026"));
        Assert.Contains("""
              P "Printed Date_1, see example.com or Date_1"
                link "example.com" -> https://example.com/orders/Guid_1 at=20
                link "Date_1" -> https://example.com/orders/Guid_1 at=35
            """.ReplaceLineEndings("\n"), snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldUseOneTokenAcrossTheBodyAndHeaders_WhenTheSameDateIsInBoth()
    {
        var docx = new TestDocx
        {
            Body = """<w:p><w:r><w:t>2026-10-28</w:t></w:r></w:p><w:sectPr><w:headerReference w:type="default" r:id="top"/></w:sectPr>""",
        }.WithHeader("top", "<w:p><w:r><w:t>Printed 2026-10-28</w:t></w:r></w:p>");

        var snapshot = docx.Load().ToSnapshot(o => o.ScrubDates());

        Assert.Contains("  P \"Date_1\"\n", snapshot);
        Assert.Contains("    P \"Printed Date_1\"\n", snapshot);
    }

    [Fact]
    public void ToSnapshot_ShouldIgnoreTheInsertedDocumentsStylesAndNumbering_WhenThoseSectionsAreIgnored()
    {
        var inserted = new TestDocx
        {
            Styles = """<w:style w:type="paragraph" w:styleId="Normal" w:default="1"><w:rPr><w:b/></w:rPr></w:style>""",
            Body = "<w:p><w:r><w:t>Inserted</w:t></w:r></w:p>",
        }.Build().ToArray();
        var docx = new TestDocx { Body = """<w:altChunk r:id="chunk"/>""" }.WithPart("chunk", "aFChunk", "inserted.docx", inserted);

        var snapshot = docx.Load().ToSnapshot(o => o.Ignore(DocxSection.Styles, DocxSection.Numbering));

        Assert.Contains("  Inserted docx\n    styles\n      (ignored)\n    numbering\n      (ignored)\n    body\n", snapshot);
    }

    private static MemoryStream Docx(string text) => new TestDocx { Body = $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>" }.Build();
}
