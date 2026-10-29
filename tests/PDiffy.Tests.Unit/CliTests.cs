using PDiffy.Cli;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Tests.Unit;

public sealed class CliTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pdiffy-cli-").FullName;
    private readonly StringWriter _output = new();
    private readonly StringWriter _error = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Run_ShouldPrintTheChangesAndReturnOne_WhenTwoSnapshotFilesDiffer()
    {
        var before = Write("before.txt", "structure\n  P p1 \"Take with food\"\n");
        var after = Write("after.txt", "structure\n  P p1 \"Take after food\"\n");

        var exitCode = Commands.Run([before, after], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        Assert.Contains("\"Take [-with-] {+after+} food\"", _output.ToString());
    }

    [Fact]
    public void Run_ShouldReturnZeroAndWriteTheReport_WhenTwoPdfsMatch()
    {
        var before = WritePdf("before.pdf", "Hello");
        var after = WritePdf("after.pdf", "Hello");
        var report = Path.Combine(_folder, "report.html");

        var exitCode = Commands.Run([before, after, "--html", report], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.StartsWith("No changes.", _output.ToString());
        Assert.StartsWith("<!doctype html>", File.ReadAllText(report));
    }

    [Fact]
    public void Run_ShouldPrintOnlyJson_WhenAskedForJsonWithoutAFile()
    {
        var before = Write("before.txt", "structure\n  P p1 \"Take with food\"\n");
        var after = Write("after.txt", "structure\n  P p1 \"Take after food\"\n");
        var report = Path.Combine(_folder, "report.html");

        var exitCode = Commands.Run([before, after, "--json", "--html", report], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        var diff = SnapshotDiff.Compare(File.ReadAllText(before), File.ReadAllText(after));
        Assert.Equal(diff.ToJson(), _output.ToString());
        Assert.True(File.Exists(report));
    }

    [Fact]
    public void Run_ShouldSaveTheJsonAndPrintTheSummary_WhenGivenAFileForTheJson()
    {
        var before = Write("before.txt", "a\n");
        var after = Write("after.txt", "b\n");
        var json = Path.Combine(_folder, "changes.json");

        Commands.Run([before, after, "--json", json], _output, _error);

        Assert.StartsWith("1 change(s)", _output.ToString());
        Assert.StartsWith("{\n  \"version\": 1,", File.ReadAllText(json));
    }

    [Fact]
    public void Run_ShouldSaveThePdfsSnapshot_WhenAskedForASnapshot()
    {
        var pdf = WritePdf("letter.pdf", "Hello");
        var file = Path.Combine(_folder, "letter.pdfua.txt");

        var exitCode = Commands.Run(["snapshot", pdf, file], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal(PdfUaDocument.Load(pdf).ToSnapshot(), File.ReadAllText(file));
    }

    [Fact]
    public void Run_ShouldMoveReceivedOverVerifiedAndDeleteTheReport_WhenAcceptingAFolder()
    {
        Write("Letter.received.pdfua.txt", "new");
        Write("Letter.verified.pdfua.txt", "old");
        Write("Letter.diff.html", "report");
        Directory.CreateDirectory(Path.Combine(_folder, "nested"));
        Write(Path.Combine("nested", "Memo.received.pdfua.txt"), "nested");

        var exitCode = Commands.Run(["accept", _folder], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(_folder, "Letter.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(_folder, "Letter.diff.html")));
        Assert.True(File.Exists(Path.Combine(_folder, "nested", "Memo.received.pdfua.txt")));
    }

    [Fact]
    public void Run_ShouldAcceptEveryTarget_WhenGivenSeveralFiles()
    {
        var letter = Write("Letter.received.pdfua.txt", "letter");
        var memo = Write("Memo.received.pdfua.txt", "memo");

        Commands.Run(["accept", letter, memo], _output, _error);

        Assert.Equal("letter", File.ReadAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt")));
        Assert.Equal("memo", File.ReadAllText(Path.Combine(_folder, "Memo.verified.pdfua.txt")));
    }

    [Fact]
    public void Run_ShouldAcceptEachFileOnce_WhenTargetsOverlap()
    {
        var letter = Write("Letter.received.pdfua.txt", "letter");

        var exitCode = Commands.Run(["accept", _folder, letter], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("letter", File.ReadAllText(Path.Combine(_folder, "Letter.verified.pdfua.txt")));
    }

    [Fact]
    public void Run_ShouldPrintThePdfsSnapshot_WhenNoFileIsGiven()
    {
        var pdf = WritePdf("letter.pdf", "Hello");

        Commands.Run(["snapshot", pdf], _output, _error);

        Assert.Equal(PdfUaDocument.Load(pdf).ToSnapshot(), _output.ToString());
    }

    [Fact]
    public void Run_ShouldAcceptSubfolders_WhenGivenAll()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "nested"));
        Write(Path.Combine("nested", "Memo.received.pdfua.txt"), "nested");

        Commands.Run(["accept", _folder, "--all"], _output, _error);

        Assert.Equal("nested", File.ReadAllText(Path.Combine(_folder, "nested", "Memo.verified.pdfua.txt")));
    }

    [Theory]
    [InlineData]
    [InlineData("--html")]
    [InlineData("snapshot")]
    [InlineData("before.txt", "after.txt", "--html")]
    [InlineData("before.txt", "after.txt", "--color")]
    public void Run_ShouldPrintUsageAndReturnTwo_WhenTheArgumentsDontFitACommand(params string[] args)
    {
        var exitCode = Commands.Run(args, _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("Usage:", _error.ToString());
        Assert.Empty(_output.ToString());
    }

    [Fact]
    public void Run_ShouldReportTheFileOnTheErrorOutputAndReturnTwo_WhenAFileIsMissing()
    {
        var other = Write("other.txt", "text");

        var exitCode = Commands.Run([Path.Combine(_folder, "missing.pdf"), other], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("missing.pdf", _error.ToString());
        Assert.Empty(_output.ToString());
    }

    [Fact]
    public void Run_ShouldKeepEachFilesExtension_WhenAcceptingTextAndJsonSnapshots()
    {
        Write("Braille.received.txt", "braille");
        Write("Job.received.json", "{}");
        Write("Job.diff.html", "report");

        Commands.Run(["accept", _folder], _output, _error);

        Assert.Equal("braille", File.ReadAllText(Path.Combine(_folder, "Braille.verified.txt")));
        Assert.Equal("{}", File.ReadAllText(Path.Combine(_folder, "Job.verified.json")));
        Assert.False(File.Exists(Path.Combine(_folder, "Job.diff.html")));
    }

    [Fact]
    public void Run_ShouldRefuseAndReturnTwo_WhenAcceptingAFileThatIsNotReceived()
    {
        var notReceived = Write("notes.txt", "text");

        var exitCode = Commands.Run(["accept", notReceived], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("isn't a received snapshot", _error.ToString());
    }

    [Fact]
    public void Run_ShouldSayNothingIsPendingAndReturnZero_WhenReportingAFolderWithoutReceivedSnapshots()
    {
        Write("Letter.verified.pdfua.txt", Sectioned("Take with food"));

        var markdown = Path.Combine(_folder, "summary.md");

        var exitCode = Commands.Run(["report", _folder, "--markdown", markdown], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("No received snapshots.\n", _output.ToString());
        Assert.Equal("### PDiffy: No received snapshots.\n", File.ReadAllText(markdown));
    }

    [Fact]
    public void Run_ShouldListEachReceivedSnapshotAndReturnOne_WhenReporting()
    {
        // Git can check a verified file out with CRLF on Windows.
        Write("Letter.verified.pdfua.txt", Sectioned("Take with food", "H1").Replace("\n", "\r\n"));
        Write("Letter.received.pdfua.txt", Sectioned("Take with food"));
        Write("Job.verified.json", "{}\n");
        Write("Job.received.json", "{\n  \"id\": 1\n}\n");
        Write("Memo.received.txt", "new");

        var exitCode = Commands.Run(["report", _folder], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        Assert.StartsWith("3 received snapshot(s): 2 changed, 1 new\n", _output.ToString());
        Assert.Matches(@"changed \S*Job\.received\.json: 3: 2 added, 1 text\n", _output.ToString());
        Assert.Matches(@"changed \S*Letter\.received\.pdfua\.txt: 1: 1 changed; by section: structure 1; "
            + @"1 can affect accessibility\n", _output.ToString());
        Assert.Matches(@"new \S*Memo\.received\.txt\n", _output.ToString());
    }

    [Fact]
    public void Run_ShouldSaveTheSummaryAsMarkdownAndHtml_WhenAskedFor()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "Snapshots"));
        Write(Path.Combine("Snapshots", "Letter.verified.pdfua.txt"), Sectioned("Take with food"));
        Write(Path.Combine("Snapshots", "Letter.received.pdfua.txt"), Sectioned("Take after food"));
        Write(Path.Combine("Snapshots", "Letter.diff.html"), "report");
        Write(Path.Combine("Snapshots", "Memo.received.txt"), "new");
        var (markdown, html) = (Path.Combine(_folder, "summary.md"), Path.Combine(_folder, "summary.html"));
        Write("summary.md", "Earlier steps\n");

        Commands.Run(["report", _folder, "--all", "--markdown", markdown, "--html", html], _output, _error);

        var table = File.ReadAllText(markdown);
        Assert.StartsWith("Earlier steps\n### PDiffy: 2 received snapshot(s)", table);
        Assert.Contains("| Snapshot | Status | Changes | By section | Can affect accessibility |", table);
        Assert.Matches(@"\| `\S*Letter\.received\.pdfua\.txt` \| changed \| 1: 1 text \| structure 1 \| 0 \|", table);
        Assert.Contains("<a href=\"Snapshots/Letter.diff.html\">", File.ReadAllText(html));
        Assert.Matches(@"<tr><td>[^<]*Memo\.received\.txt</td><td>new</td>", File.ReadAllText(html));
    }

    [Fact]
    public void Run_ShouldReturnTwo_WhenReportingWithoutAFileForTheHtml()
    {
        var exitCode = Commands.Run(["report", _folder, "--html"], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("--html needs a file", _error.ToString());
    }

    private static string Sectioned(string text, string type = "P") =>
        $"# pdfua-snapshot v1\nstructure\n  {type} p1 \"{text}\"\n";

    [Fact]
    public void Run_ShouldTreatAFileAsAPdf_WhenItsHeaderComesAfterOtherBytes()
    {
        var pdf = WritePdf("letter.pdf", "Hello");
        var shifted = Path.Combine(_folder, "shifted.pdf");
        File.WriteAllBytes(shifted, [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(pdf)]);

        var exitCode = Commands.Run([pdf, shifted], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Run_ShouldCompareTheDocumentsAndReturnOne_WhenTwoDocxFilesDiffer()
    {
        var before = WriteDocx("before.docx", "Take with food");
        var after = WriteDocx("after.docx", "Take after food");

        var exitCode = Commands.Run([before, after], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        Assert.Contains("\"Take [-with-] {+after+} food\"", _output.ToString());
    }

    [Fact]
    public void Run_ShouldPrintTheDocxsSnapshot_WhenGivenADocx()
    {
        var docx = WriteDocx("letter.docx", "Hello");

        Commands.Run(["snapshot", docx], _output, _error);

        Assert.Equal(DocxDocument.Load(docx).ToSnapshot(), _output.ToString());
    }

    private string WriteDocx(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, new Docx.TestDocx { Body = $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>" }.Build().ToArray());
        return path;
    }

    private string WritePdf(string name, string text)
    {
        var pdf = new TestPdf();
        pdf.AddPage().Tagged(0, text);
        pdf.Structure(new TestElement("P", 0));
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, pdf.Build());
        return path;
    }
}
