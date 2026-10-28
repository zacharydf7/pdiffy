using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

// Tests in one class run one at a time, so changing the environment and PdfSnapshot.Directory here is safe; no other
// test class reads them. Each test gets its own folder and puts everything back afterwards.
public sealed class PdfSnapshotTests : IDisposable
{
    private static readonly string[] Variables = [PdfSnapshot.AcceptVariable, "CI", "TF_BUILD"];

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "pdiffy-tests", Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, string?> _savedVariables =
        Variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
    private readonly string? _savedDirectory = PdfSnapshot.Directory;

    public PdfSnapshotTests()
    {
        PdfSnapshot.Directory = _folder;
        foreach (var name in Variables) Environment.SetEnvironmentVariable(name, null);
    }

    public void Dispose()
    {
        PdfSnapshot.Directory = _savedDirectory;
        foreach (var (name, value) in _savedVariables) Environment.SetEnvironmentVariable(name, value);
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task VerifyAsync_ShouldWriteTheReceivedFileAndFail_WhenThereIsNoVerifiedFile()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "Letter.diff.html"), "left over");

        var error = await Assert.ThrowsAsync<PdfSnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Letter"));

        Assert.StartsWith("New PDF snapshot.", error.Message);
        Assert.False(File.Exists(Path.Combine(_folder, "Letter.diff.html")));
        Assert.Equal(Snapshot("Hello"), File.ReadAllText(Path.Combine(_folder, "Letter.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldPassAndDeleteTheStaleReceivedFile_WhenTheVerifiedFileMatches()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt"), Snapshot("Hello").ReplaceLineEndings("\r\n"));
        File.WriteAllText(Path.Combine(_folder, "Letter.received.pdfua.txt"), "left over");
        File.WriteAllText(Path.Combine(_folder, "Letter.diff.html"), "left over");

        await PdfSnapshot.VerifyAsync(Pdf("Hello"), "Letter");

        Assert.False(File.Exists(Path.Combine(_folder, "Letter.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(_folder, "Letter.diff.html")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldFailWithTheChangedLines_WhenTheSnapshotDiffers()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt"), Snapshot("Hello"));

        var error = await Assert.ThrowsAsync<PdfSnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Goodbye"), "Letter"));

        Assert.StartsWith("The PDF's snapshot differs from the verified one.", error.Message);
        Assert.Contains("~ text structure > P (p1)\n    \"[-Hello-] {+Goodbye+}\"", error.Message);
        Assert.Contains($"received: {Path.Combine(_folder, "Letter.received.pdfua.txt")}", error.Message);
        Assert.Equal(Snapshot("Goodbye"), File.ReadAllText(Path.Combine(_folder, "Letter.received.pdfua.txt")));
        Assert.Contains($"report: {Path.Combine(_folder, "Letter.diff.html")}", error.Message);
        Assert.Contains("<mark>", File.ReadAllText(Path.Combine(_folder, "Letter.diff.html")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldOverwriteTheVerifiedFile_WhenAcceptModeIsOn()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt"), Snapshot("Goodbye"));
        File.WriteAllText(Path.Combine(_folder, "Letter.received.pdfua.txt"), "left over");
        File.WriteAllText(Path.Combine(_folder, "Letter.diff.html"), "left over");
        Environment.SetEnvironmentVariable(PdfSnapshot.AcceptVariable, "1");

        await PdfSnapshot.VerifyAsync(Pdf("Hello"), "Letter");

        Assert.Equal(Snapshot("Hello"), File.ReadAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(_folder, "Letter.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(_folder, "Letter.diff.html")));
    }

    [Theory]
    [InlineData("CI", "true")]
    [InlineData("TF_BUILD", "True")]
    public async Task VerifyAsync_ShouldIgnoreAcceptMode_WhenRunningOnCi(string variable, string value)
    {
        Environment.SetEnvironmentVariable(PdfSnapshot.AcceptVariable, "1");
        Environment.SetEnvironmentVariable(variable, value);

        await Assert.ThrowsAsync<PdfSnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Letter"));

        Assert.False(File.Exists(Path.Combine(_folder, "Letter.verified.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven()
    {
        await Assert.ThrowsAsync<PdfSnapshotException>(() => PdfSnapshot.VerifyAsync(Pdf("Hello")));

        var method = nameof(VerifyAsync_ShouldNameTheSnapshotAfterTheTestFileAndMethod_WhenNoNameIsGiven);
        var expected = $"{nameof(PdfSnapshotTests)}.{method}";
        Assert.True(File.Exists(Path.Combine(_folder, $"{expected}.received.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldPassAfterAccepting_WhenTextContainsALineSeparator()
    {
        // A UTF-16 Info entry "A", U+2028, "B": a line separator, which the snapshot keeps as text.
        var pdf = new TestPdf { InfoEntries = "/Note <FEFF004120280042>" };
        pdf.AddPage();
        var bytes = pdf.Build();
        Environment.SetEnvironmentVariable(PdfSnapshot.AcceptVariable, "1");
        await PdfSnapshot.VerifyAsync(new MemoryStream(bytes), "Letter");
        Environment.SetEnvironmentVariable(PdfSnapshot.AcceptVariable, null);

        await PdfSnapshot.VerifyAsync(new MemoryStream(bytes), "Letter");

        Assert.Contains("info Note=\"A\u2028B\"", File.ReadAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt")));
    }

    [Fact]
    public async Task VerifyAsync_ShouldSayToSetTheDirectory_WhenTheTestFilesFolderDoesNotExist()
    {
        PdfSnapshot.Directory = null;

        var error = await Assert.ThrowsAsync<PdfSnapshotException>(
            () => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Letter", testFile: "/_/tests/LetterTests.cs"));

        Assert.Contains("Set PdfSnapshot.Directory", error.Message);
    }

    [Fact]
    public async Task VerifyAsync_ShouldApplyTheOptions_WhenGivenSome()
    {
        await Assert.ThrowsAsync<PdfSnapshotException>(
            () => PdfSnapshot.VerifyAsync(Pdf("Hello"), "Letter", o => o.Ignore(Section.Layout)));

        Assert.Contains("# ignored sections: layout\n", File.ReadAllText(Path.Combine(_folder, "Letter.received.pdfua.txt")));
    }

    private static MemoryStream Pdf(string text)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, text);
        pdf.Structure(new TestElement("P", 0));
        return new MemoryStream(pdf.Build());
    }

    private static string Snapshot(string text) => PdfUaDocument.Load(Pdf(text)).ToSnapshot();
}
