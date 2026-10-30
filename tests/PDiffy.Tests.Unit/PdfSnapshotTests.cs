using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

[Collection(nameof(SnapshotFolderTests))]
public sealed class PdfSnapshotTests : SnapshotFolderTests
{
    [Fact]
    public async Task VerifyPdfAsync_ShouldWriteTheReceivedFileAndFail_WhenThereIsNoVerifiedFile()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.diff.html"), "left over");

        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide"));

        Assert.StartsWith("New PDF snapshot.", error.Message);
        Assert.Contains($"{Environment.NewLine}1 page; ", error.Message);
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.diff.html")));
        Assert.Equal(SnapshotOf("Hello"), File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldPassAndDeleteTheStaleReceivedFile_WhenTheVerifiedFileMatches()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello").ReplaceLineEndings("\r\n"));
        File.WriteAllText(Path.Combine(Folder, "Guide.received.pdfua.txt"), "left over");
        File.WriteAllText(Path.Combine(Folder, "Guide.diff.html"), "left over");

        await Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide");

        Assert.False(File.Exists(Path.Combine(Folder, "Guide.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.diff.html")));
    }

    [Fact]
    public void VerifyPdf_ShouldThrowRightAway_WhenThereIsNoVerifiedFile()
    {
        var error = Assert.Throws<SnapshotException>(() => Snapshot.VerifyPdf(Pdf("Hello"), "Guide"));

        Assert.StartsWith("New PDF snapshot.", error.Message);
        Assert.Equal(SnapshotOf("Hello"), File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public void VerifyPdf_ShouldPass_WhenGivenTheStreamBytesOrLoadedDocumentOfAMatchingPdf()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello"));
        var pdf = Pdf("Hello");

        Snapshot.VerifyPdf(pdf, "Guide");
        Snapshot.VerifyPdf(pdf.ToArray(), "Guide");
        Snapshot.VerifyPdf(PdfUaDocument.Load(pdf), "Guide");

        Assert.False(File.Exists(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public void VerifyPdfAsync_ShouldThrowAtTheCall_WhenTheSnapshotDiffersAndTheCallIsNotAwaited()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello"));

        var error = Assert.Throws<SnapshotException>(() => { _ = Snapshot.VerifyPdfAsync(Pdf("Goodbye"), "Guide"); });

        Assert.StartsWith("The PDF's snapshot differs from the verified one.", error.Message);
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldPass_WhenGivenTheBytesOrTheLoadedDocumentOfAMatchingPdf()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello"));
        var pdf = Pdf("Hello");

        await Snapshot.VerifyPdfAsync(pdf.ToArray(), "Guide");
        await Snapshot.VerifyPdfAsync(PdfUaDocument.Load(pdf), "Guide");

        Assert.False(File.Exists(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldApplyTheDefaultsAndTheCallsOwnOptions_WhenDefaultOptionsAreSet()
    {
        Snapshot.DefaultPdfOptions = options => options.Use(PdfProfile.Accessibility);

        await Assert.ThrowsAsync<SnapshotException>(
            () => Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide", o => o.Ignore(PdfSection.Fonts)));

        var expected = PdfUaDocument.Load(Pdf("Hello"))
            .ToSnapshot(o => o.Use(PdfProfile.Accessibility).Ignore(PdfSection.Fonts));
        Assert.Equal(expected, File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldFailWithTheChangedLines_WhenTheSnapshotDiffers()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Hello"));

        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyPdfAsync(Pdf("Goodbye"), "Guide"));

        Assert.StartsWith("The PDF's snapshot differs from the verified one.", error.Message);
        Assert.DoesNotContain(" lines: ", error.Message);
        Assert.Contains("~ text structure > P (p1)\n    \"[-Hello-] {+Goodbye+}\"", error.Message);
        Assert.Contains($"received: {Path.Combine(Folder, "Guide.received.pdfua.txt")}", error.Message);
        Assert.Equal(SnapshotOf("Goodbye"), File.ReadAllText(Path.Combine(Folder, "Guide.received.pdfua.txt")));
        Assert.Contains($"report: {Path.Combine(Folder, "Guide.diff.html")}", error.Message);
        Assert.Contains("<mark>", File.ReadAllText(Path.Combine(Folder, "Guide.diff.html")));
        var received = Path.Combine(Folder, "Guide.received.pdfua.txt");
        var newLine = Environment.NewLine;
        Assert.Contains($"To accept, run:{newLine}  dotnet pdiffy accept \"{received}\"{newLine}", error.Message);
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldSayTheFormatChangedBeforeTheChanges_WhenTheVerifiedFileHasAnotherFormatVersion()
    {
        Directory.CreateDirectory(Folder);
        var olderFormat = SnapshotOf("Hello").Replace("# pdfua-snapshot v1", "# pdfua-snapshot v0");
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), olderFormat);

        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyPdfAsync(Pdf("Goodbye"), "Guide"));

        Assert.StartsWith("The verified file uses pdfua-snapshot v0, and this version of PDiffy writes pdfua-snapshot v1.",
            error.Message);
        Assert.Contains("The PDF's snapshot differs from the verified one.", error.Message);
        Assert.Contains("~ text structure > P (p1)\n    \"[-Hello-] {+Goodbye+}\"", error.Message);
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldOverwriteTheVerifiedFile_WhenAcceptModeIsOn()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt"), SnapshotOf("Goodbye"));
        File.WriteAllText(Path.Combine(Folder, "Guide.received.pdfua.txt"), "left over");
        File.WriteAllText(Path.Combine(Folder, "Guide.diff.html"), "left over");
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");

        await Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide");

        Assert.Equal(SnapshotOf("Hello"), File.ReadAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(Folder, "Guide.diff.html")));
    }

    [Theory]
    [InlineData("CI", "true")]
    [InlineData("TF_BUILD", "True")]
    public async Task VerifyPdfAsync_ShouldIgnoreAcceptMode_WhenRunningOnCi(string variable, string value)
    {
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");
        Environment.SetEnvironmentVariable(variable, value);

        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide"));

        Assert.False(File.Exists(Path.Combine(Folder, "Guide.verified.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven()
    {
        await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyPdfAsync(Pdf("Hello")));

        var method = nameof(VerifyPdfAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven);
        var expected = $"{nameof(PdfSnapshotTests)}.{method}";
        Assert.True(File.Exists(Path.Combine(Folder, $"{expected}.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldPassAfterAccepting_WhenTextContainsALineSeparator()
    {
        // A UTF-16 Info entry "A", U+2028, "B": a line separator, which the snapshot keeps as text.
        var pdf = new TestPdf { InfoEntries = "/Note <FEFF004120280042>" };
        pdf.AddPage();
        var bytes = pdf.Build();
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, "1");
        await Snapshot.VerifyPdfAsync(new MemoryStream(bytes), "Guide");
        Environment.SetEnvironmentVariable(Snapshot.AcceptVariable, null);

        await Snapshot.VerifyPdfAsync(new MemoryStream(bytes), "Guide");

        Assert.Contains("info Note=\"A\u2028B\"", File.ReadAllText(Path.Combine(Folder, "Guide.verified.pdfua.txt")));
    }

    [Fact]
    public void VerifyPdf_ShouldPrintTheAcceptCommandWithTheFullPath_WhenTheFolderIsRelative()
    {
        var relative = Path.GetRelativePath(Environment.CurrentDirectory, Folder);
        Assert.SkipWhen(Path.IsPathRooted(relative), "The temp folder is on another drive, so no relative path reaches it.");

        var error = Assert.Throws<SnapshotException>(
            () => Snapshot.VerifyPdf(Pdf("Hello"), "Guide", directory: relative));

        Assert.Contains($"dotnet pdiffy accept \"{Path.Combine(Folder, "Guide.received.pdfua.txt")}\"", error.Message);
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldSayToSetTheDirectory_WhenTheTestFilesFolderDoesNotExist()
    {
        Snapshot.Directory = null;

        var error = await Assert.ThrowsAsync<SnapshotException>(
            () => Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide", testFile: "/_/tests/GuideTests.cs"));

        Assert.Contains("Set Snapshot.Directory", error.Message);
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldFindTheTestFilesFolderInTheProject_WhenTheBuildMappedItsPath()
    {
        Snapshot.Directory = null;
        var inside = $"pdiffy-mapped-{Guid.NewGuid():N}";
        var folder = Snapshot.ProjectFolder("obj", inside);
        Directory.CreateDirectory(folder);
        try
        {
            var mapped = $"/_/tests/PDiffy.Tests.Unit/obj/{inside}/GuideTests.cs";
            await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide",
                testFile: mapped));

            Assert.True(File.Exists(Path.Combine(folder, "Guide.received.pdfua.txt")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldAskWhetherTheVerifiedFileWasCommitted_WhenASnapshotIsNewOnABuildServer()
    {
        Environment.SetEnvironmentVariable("CI", "true");

        var error = await Assert.ThrowsAsync<SnapshotException>(() => Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide"));

        Assert.Contains("it isn't in this checkout. Was it committed", error.Message);
    }

    [Fact]
    public async Task VerifyPdfAsync_ShouldApplyTheOptions_WhenGivenSome()
    {
        await Assert.ThrowsAsync<SnapshotException>(
            () => Snapshot.VerifyPdfAsync(Pdf("Hello"), "Guide", o => o.Ignore(PdfSection.Layout)));

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
