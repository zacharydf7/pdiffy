using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

[Collection(nameof(SnapshotFolderTests))]
public sealed class PdfSnapshotTests : SnapshotFolderTests
{
    [Fact]
    public async Task VerifyAsync_ShouldWriteTheReceivedFileAndFail_WhenThereIsNoVerifiedFile()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.diff.html"), "left over");

        var error = await Assert.ThrowsAsync<SnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Guide"));

        Assert.StartsWith("New PDF snapshot.", error.Message);
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.diff.html")));
        Assert.Equal(SnapshotOf("Hello"), File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldPassAndDeleteTheStaleReceivedFile_WhenTheVerifiedFileMatches()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello").ReplaceLineEndings("\r\n"));
        File.WriteAllText(Path.Combine(Folder, "Guide.received.pdfua.txt"), "left over");
        File.WriteAllText(Path.Combine(Folder, "Guide.diff.html"), "left over");

        await PdfSnapshot.VerifyAsync(Pdf("Hello"), "Guide");

        Assert.False(File.Exists(Path.Combine(Folder, "Guide.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.diff.html")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldPass_WhenGivenTheBytesOrTheLoadedDocumentOfAMatchingPdf()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello"));
        var pdf = Pdf("Hello");

        await PdfSnapshot.VerifyAsync(pdf.ToArray(), "Guide");
        await PdfSnapshot.VerifyAsync(PdfUaDocument.Load(pdf), "Guide");

        Assert.False(File.Exists(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldApplyTheDefaultsAndTheCallsOwnOptions_WhenDefaultOptionsAreSet()
    {
        PdfSnapshot.DefaultOptions = options => options.Use(SnapshotProfile.Accessibility);

        await Assert.ThrowsAsync<SnapshotException>(
            () => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Guide", o => o.Ignore(Section.Fonts)));

        var expected = PdfUaDocument.Load(Pdf("Hello"))
            .ToSnapshot(o => o.Use(SnapshotProfile.Accessibility).Ignore(Section.Fonts));
        Assert.Equal(expected, File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldFailWithTheChangedLines_WhenTheSnapshotDiffers()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello"));

        var error = await Assert.ThrowsAsync<SnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Goodbye"), "Guide"));

        Assert.StartsWith("The PDF's snapshot differs from the verified one.", error.Message);
        Assert.Contains("~ text structure > P (p1)\n    \"[-Hello-] {+Goodbye+}\"", error.Message);
        Assert.Contains($"received: {Path.Combine(Folder, "Guide.received.pdfua.txt")}", error.Message);
        Assert.Equal(SnapshotOf("Goodbye"), File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
        Assert.Contains($"report: {Path.Combine(Folder, "Guide.diff.html")}", error.Message);
        Assert.Contains("<mark>", File.ReadAllText(Path.Combine(Folder, "Guide.diff.html")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldSayTheFormatChangedBeforeTheChanges_WhenTheVerifiedFileHasAnotherFormatVersion()
    {
        Directory.CreateDirectory(Folder);
        var olderFormat = SnapshotOf("Hello").Replace("# pdfua-snapshot v1", "# pdfua-snapshot v0");
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), olderFormat);

        var error = await Assert.ThrowsAsync<SnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Goodbye"), "Guide"));

        Assert.StartsWith("The verified file uses pdfua-snapshot v0, and this version of PDiffy writes pdfua-snapshot v1.",
            error.Message);
        Assert.Contains("The PDF's snapshot differs from the verified one.", error.Message);
        Assert.Contains("~ text structure > P (p1)\n    \"[-Hello-] {+Goodbye+}\"", error.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldOverwriteTheVerifiedFile_WhenAcceptModeIsOn()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Goodbye"));
        File.WriteAllText(Path.Combine(Folder, "Guide.received.pdfua.txt"), "left over");
        File.WriteAllText(Path.Combine(Folder, "Guide.diff.html"), "left over");
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");

        await PdfSnapshot.VerifyAsync(Pdf("Hello"), "Guide");

        Assert.Equal(SnapshotOf("Hello"), File.ReadAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.diff.html")));
    }

    [Theory]
    [InlineData("CI", "true")]
    [InlineData("TF_BUILD", "True")]
    public async Task VerifyAsync_ShouldIgnoreAcceptMode_WhenRunningOnCi(string variable, string value)
    {
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");
        Environment.SetEnvironmentVariable(variable, value);

        await Assert.ThrowsAsync<SnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Guide"));

        Assert.False(File.Exists(Path.Combine(Folder, "Guide.verified.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven()
    {
        await Assert.ThrowsAsync<SnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Hello")));

        var method = nameof(VerifyAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven);
        var expected = $"{nameof(PdfSnapshotTests)}.{method}";
        Assert.True(File.Exists(Path.Combine(Folder, $"{expected}.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldPassAfterAccepting_WhenTextContainsALineSeparator()
    {
        // A UTF-16 Info entry "A", U+2028, "B": a line separator, which the snapshot keeps as text.
        var pdf = new TestPdf { InfoEntries = "/Note <FEFF004120280042>" };
        pdf.AddPage();
        var bytes = pdf.Build();
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");
        await PdfSnapshot.VerifyAsync(new MemoryStream(bytes), "Guide");
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, null);

        await PdfSnapshot.VerifyAsync(new MemoryStream(bytes), "Guide");

        Assert.Contains("info Note=\"A\u2028B\"", File.ReadAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldSayToSetTheDirectory_WhenTheTestFilesFolderDoesNotExist()
    {
        Snapshot.Directory = null;

        var error = await Assert.ThrowsAsync<SnapshotException>(
            () => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Guide", testFile: "/_/tests/GuideTests.cs"));

        Assert.Contains("Set Snapshot.Directory", error.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldApplyTheOptions_WhenGivenSome()
    {
        await Assert.ThrowsAsync<SnapshotException>(
            () => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Guide", o => o.Ignore(Section.Layout)));

        Assert.Contains("# ignored sections: layout\n", File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    private static MemoryStream Pdf(string text)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, text);
        pdf.Structure(new TestElement("P", 0));
        return new MemoryStream(pdf.Build());
    }

    private static string SnapshotOf(string text) => PdfUaDocument.Load(Pdf(text)).ToSnapshot();
}
