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
    public void Run_ShouldRefuseAndReturnTwo_WhenAcceptingAFileThatIsNotReceived()
    {
        var notReceived = Write("notes.txt", "text");

        var exitCode = Commands.Run(["accept", notReceived], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("isn't a received snapshot", _error.ToString());
    }

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
