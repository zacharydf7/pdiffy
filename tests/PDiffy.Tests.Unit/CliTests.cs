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
        var before = Write("before.txt", "structure\n  P p1 \"Go with a guide\"\n");
        var after = Write("after.txt", "structure\n  P p1 \"Go without a guide\"\n");

        var exitCode = Commands.Run([before, after], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        Assert.Contains("\"Go [-with-] {+without+} a guide\"", _output.ToString());
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
        var before = Write("before.txt", "structure\n  P p1 \"Go with a guide\"\n");
        var after = Write("after.txt", "structure\n  P p1 \"Go without a guide\"\n");
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
        var pdf = WritePdf("guide.pdf", "Hello");
        var file = Path.Combine(_folder, "guide.pdfua.txt");

        var exitCode = Commands.Run(["snapshot", pdf, file], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal(PdfUaDocument.Load(pdf).ToSnapshot(), File.ReadAllText(file));
    }

    [Fact]
    public void Run_ShouldMoveReceivedOverVerifiedAndDeleteTheReport_WhenAcceptingAFolder()
    {
        Write("Guide.received.pdfua.txt", "new");
        Write("Guide.verified.pdfua.txt", "old");
        Write("Guide.diff.html", "report");
        Directory.CreateDirectory(Path.Combine(_folder, "nested"));
        Write(Path.Combine("nested", "Memo.received.pdfua.txt"), "nested");

        var exitCode = Commands.Run(["accept", _folder], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("new", File.ReadAllText(Path.Combine(_folder, "Guide.verified.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(_folder, "Guide.received.pdfua.txt")));
        Assert.False(File.Exists(Path.Combine(_folder, "Guide.diff.html")));
        Assert.True(File.Exists(Path.Combine(_folder, "nested", "Memo.received.pdfua.txt")));
    }

    [Fact]
    public void Run_ShouldAcceptEveryTarget_WhenGivenSeveralFiles()
    {
        var guide = Write("Guide.received.pdfua.txt", "guide");
        var memo = Write("Memo.received.pdfua.txt", "memo");

        Commands.Run(["accept", guide, memo], _output, _error);

        Assert.Equal("guide", File.ReadAllText(Path.Combine(_folder, "Guide.verified.pdfua.txt")));
        Assert.Equal("memo", File.ReadAllText(Path.Combine(_folder, "Memo.verified.pdfua.txt")));
    }

    [Fact]
    public void Run_ShouldAcceptEachFileOnce_WhenTargetsOverlap()
    {
        var guide = Write("Guide.received.pdfua.txt", "guide");

        var exitCode = Commands.Run(["accept", _folder, guide], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("guide", File.ReadAllText(Path.Combine(_folder, "Guide.verified.pdfua.txt")));
    }

    [Fact]
    public void Run_ShouldAcceptEachFileOnce_WhenTargetsDifferOnlyInCaseWhereNamesIgnoreCase()
    {
        Assert.SkipUnless(NamesIgnoreCase(), "The test folder's file names respect case.");
        var guide = Write("Guide.received.pdfua.txt", "guide");

        var exitCode = Commands.Run(["accept", _folder.ToUpperInvariant(), guide], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("guide", File.ReadAllText(Path.Combine(_folder, "Guide.verified.pdfua.txt")));
    }

    [Fact]
    public void Run_ShouldAcceptOnlyTheNamedFile_WhenTwoNamesDifferOnlyInCaseWhereNamesRespectCase()
    {
        Assert.SkipWhen(NamesIgnoreCase(), "The test folder's file names ignore case.");
        Write("Guide.received.txt", "upper");
        var lower = Write("guide.received.txt", "lower");

        var exitCode = Commands.Run(["accept", lower], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("lower", File.ReadAllText(Path.Combine(_folder, "guide.verified.txt")));
        Assert.True(File.Exists(Path.Combine(_folder, "Guide.received.txt")));
    }

    [Fact]
    public void Run_ShouldAcceptNothingAndReturnTwo_WhenATargetsCaseMatchesNoFileWhereNamesRespectCase()
    {
        Assert.SkipWhen(NamesIgnoreCase(), "The test folder's file names ignore case.");
        Write("Guide.received.txt", "upper");

        var exitCode = Commands.Run(["accept", Path.Combine(_folder, "guide.received.txt")], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.False(File.Exists(Path.Combine(_folder, "Guide.verified.txt")));
    }

    // As on Windows, and on macOS by default.
    private bool NamesIgnoreCase() =>
        Directory.Exists(_folder.ToUpperInvariant()) && Directory.Exists(_folder.ToLowerInvariant());

    [Fact]
    public void Run_ShouldPrintThePdfsSnapshot_WhenNoFileIsGiven()
    {
        var pdf = WritePdf("guide.pdf", "Hello");

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
        Write("Export.received.txt", "export");
        Write("Job.received.json", "{}");
        Write("Job.diff.html", "report");

        Commands.Run(["accept", _folder], _output, _error);

        Assert.Equal("export", File.ReadAllText(Path.Combine(_folder, "Export.verified.txt")));
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
        Write("Guide.verified.pdfua.txt", Sectioned("Go with a guide"));

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
        Write("Guide.verified.pdfua.txt", Sectioned("Go with a guide", "H1").Replace("\n", "\r\n"));
        Write("Guide.received.pdfua.txt", Sectioned("Go with a guide"));
        Write("Job.verified.json", "{}\n");
        Write("Job.received.json", "{\n  \"id\": 1\n}\n");
        Write("Memo.received.txt", "new");

        var exitCode = Commands.Run(["report", _folder], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        Assert.StartsWith("3 received snapshot(s): 2 changed, 1 new\n", _output.ToString());
        Assert.Matches(@"changed \S*Job\.received\.json: 3: 2 added, 1 text\n", _output.ToString());
        Assert.Matches(@"changed \S*Guide\.received\.pdfua\.txt: 1: 1 changed; by section: structure 1; "
            + @"1 can affect accessibility\n", _output.ToString());
        Assert.Matches(@"new \S*Memo\.received\.txt\n", _output.ToString());
    }

    [Fact]
    public void Run_ShouldSaveTheSummaryAsMarkdownAndHtml_WhenAskedFor()
    {
        Directory.CreateDirectory(Path.Combine(_folder, "Snapshots"));
        Write(Path.Combine("Snapshots", "Guide.verified.pdfua.txt"), Sectioned("Go with a guide"));
        Write(Path.Combine("Snapshots", "Guide.received.pdfua.txt"), Sectioned("Go without a guide"));
        Write(Path.Combine("Snapshots", "Guide.diff.html"), "report");
        Write(Path.Combine("Snapshots", "Memo.received.txt"), "new");
        var (markdown, html) = (Path.Combine(_folder, "summary.md"), Path.Combine(_folder, "summary.html"));
        Write("summary.md", "Earlier steps\n");

        Commands.Run(["report", _folder, "--all", "--markdown", markdown, "--html", html], _output, _error);

        var table = File.ReadAllText(markdown);
        Assert.StartsWith("Earlier steps\n### PDiffy: 2 received snapshot(s)", table);
        Assert.Contains("| Snapshot | Status | Changes | By section | Can affect accessibility |", table);
        Assert.Matches(@"\| `\S*Guide\.received\.pdfua\.txt` \| changed \| 1: 1 text \| structure 1 \| 0 \|", table);
        Assert.Contains("<a href=\"Snapshots/Guide.diff.html\">", File.ReadAllText(html));
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
        var pdf = WritePdf("guide.pdf", "Hello");
        var shifted = Path.Combine(_folder, "shifted.pdf");
        File.WriteAllBytes(shifted, [0xEF, 0xBB, 0xBF, .. File.ReadAllBytes(pdf)]);

        var exitCode = Commands.Run([pdf, shifted], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
    }

    private string Folder(string name) => Path.Combine(_folder, name);

    private string Write(string name, string text)
    {
        var path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Run_ShouldCompareTheDocumentsAndReturnOne_WhenTwoDocxFilesDiffer()
    {
        var before = WriteDocx("before.docx", "Go with a guide");
        var after = WriteDocx("after.docx", "Go without a guide");

        var exitCode = Commands.Run([before, after], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        Assert.Contains("\"Go [-with-] {+without+} a guide\"", _output.ToString());
    }

    [Fact]
    public void Run_ShouldListEachFileThatDiffersAndReturnOne_WhenComparingFolders()
    {
        WritePdf("before/guide.pdf", "Go with a guide");
        WritePdf("after/guide.pdf", "Go without a guide");
        WritePdf("before/same/guide.pdf", "Hello");
        WritePdf("after/same/guide.pdf", "Hello");
        Write("before/old.txt", "a\n");
        Write("after/new.json", "{}");
        Write("before/broken.pdf", "%PDF-1.7 not really");
        Write("after/broken.pdf", "%PDF-1.7 not really");
        Write("after/logo.png", "not compared");

        var exitCode = Commands.Run(["compare", Folder("before"), Folder("after")], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        var lines = _output.ToString().Split('\n');
        Assert.Equal("4 file(s) differ: 1 changed, 1 added, 1 removed, 1 unreadable; 1 unchanged, 1 not compared.",
            lines[0]);
        Assert.StartsWith("changed guide.pdf: 2: 2 text; by section: structure 1, layout 1", lines[1]);
        Assert.Equal(["added new.json", "removed old.txt"], lines[2..4]);
        Assert.StartsWith("unreadable broken.pdf: ", lines[4]);
    }

    [Fact]
    public void Run_ShouldSayNoFilesDifferAndReturnZero_WhenFoldersMatch()
    {
        WritePdf("before/guide.pdf", "Hello");
        WritePdf("after/guide.pdf", "Hello");

        var exitCode = Commands.Run(["compare", Folder("before"), Folder("after")], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Equal("No files differ: 1 unchanged.\n", _output.ToString());
    }

    [Fact]
    public void Run_ShouldSaveAnIndexWithAReportForEachChangedFileAndTheJson_WhenComparingFoldersWithHtmlAndJson()
    {
        Write("before/notes/a.txt", "a\n");
        Write("after/notes/a.txt", "b\n");
        Write("after/extra.txt", "c\n");
        var (index, json) = (Path.Combine(_folder, "out", "index.html"), Path.Combine(_folder, "changes.json"));

        Commands.Run(["compare", Folder("before"), Folder("after"), "--html", index, "--json", json], _output, _error);

        Assert.Contains("<a href=\"notes/a.txt.diff.html\">notes/a.txt</a>", File.ReadAllText(index));
        Assert.StartsWith("<!doctype html>", File.ReadAllText(Path.Combine(_folder, "out", "notes", "a.txt.diff.html")));
        var files = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(json))!["files"]!.AsArray();
        Assert.Equal(["changed notes/a.txt 1", "added extra.txt 0"],
            files.Select(file => $"{file!["status"]} {file["path"]} {file["changes"]?.AsArray().Count ?? 0}"));
    }

    [Fact]
    public void Run_ShouldSaveTheIndexInANewFolder_WhenComparingFoldersThatMatchWithHtml()
    {
        Write("before/a.txt", "a\n");
        Write("after/a.txt", "a\n");
        var index = Path.Combine(_folder, "out", "index.html");

        var exitCode = Commands.Run(["compare", Folder("before"), Folder("after"), "--html", index], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        Assert.Contains("No files differ: 1 unchanged.", File.ReadAllText(index));
    }

    // Word has no accessibility profile, so a Word document is then compared in full.
    [Theory]
    [InlineData("content", Commands.Same)]
    [InlineData("accessibility", Commands.Different)]
    public void Run_ShouldUseTheWordProfileOfTheSameName_WhenComparingFoldersOfDocxFilesWithAProfile(string profile,
        int expected)
    {
        WriteDocx("before/guide.docx", "Hello", font: "Arial");
        WriteDocx("after/guide.docx", "Hello", font: "Georgia");

        var exitCode = Commands.Run(["compare", Folder("before"), Folder("after"), "--profile", profile], _output, _error);

        Assert.Equal(expected, exitCode);
    }

    [Fact]
    public void Run_ShouldLeaveOutWhatTheProfileLeavesOut_WhenGivenAProfile()
    {
        var before = WritePdf("before.pdf", "Hello", title: "Draft");
        var after = WritePdf("after.pdf", "Hello", title: "Final");

        var exitCode = Commands.Run([before, after, "--profile", "content"], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
    }

    [Theory]
    [InlineData("--profile")]
    [InlineData("--profile", "pixels")]
    [InlineData("--profile", "2")]
    [InlineData("--profile", "content,accessibility")]
    public void Run_ShouldPrintUsageAndReturnTwo_WhenTheProfileIsMissingOrUnknown(params string[] options)
    {
        var (before, after) = (Write("before.txt", "a\n"), Write("after.txt", "a\n"));

        var exitCode = Commands.Run([before, after, .. options], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("Usage:", _error.ToString());
    }

    [Fact]
    public void Run_ShouldReportTheFolderOnTheErrorOutputAndReturnTwo_WhenAFolderIsMissing()
    {
        var exitCode = Commands.Run(["compare", Path.Combine(_folder, "nowhere"), _folder], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("nowhere isn't a folder", _error.ToString());
    }

    [Fact]
    public void Run_ShouldPrintEveryFindingAndReturnOne_WhenCheckingAPdfThatHasFindings()
    {
        var pdf = WritePdf("guide.pdf", "Hello");

        var exitCode = Commands.Run(["check", pdf], _output, _error);

        Assert.Equal(Commands.Different, exitCode);
        var findings = PdfUaDocument.Load(pdf).Check();
        Assert.Equal([$"{findings.Count} finding(s):", .. findings.Select(finding => finding.ToString()), ""],
            _output.ToString().Split(Environment.NewLine));
    }

    [Fact]
    public void Run_ShouldPrintNoFindingsAndReturnZero_WhenCheckingAPdfThatPassesEveryCheck()
    {
        // Written by hand, since TestPdf always adds a font and this file has none to embed.
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R /Lang (en-US) /MarkInfo << /Marked true >> "
                + "/ViewerPreferences << /DisplayDocTitle true >> /StructTreeRoot 5 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            TestPdf.Stream(""),
            "<< /Type /StructTreeRoot /K [6 0 R] >>",
            "<< /Type /StructElem /S /Figure /P 5 0 R /Alt (Logo) >>",
            "<< /Title (Logo) >>",
        ];
        var path = Path.Combine(_folder, "logo.pdf");
        File.WriteAllBytes(path, RawPdf.Build(objects, catalog: 1, "/Info 7 0 R"));

        var exitCode = Commands.Run(["check", path], _output, _error);

        Assert.Equal((Commands.Same, "No findings."), (exitCode, _output.ToString().TrimEnd()));
    }

    [Fact]
    public void Run_ShouldCountWhatTheVerifiedSnapshotsCompare_WhenAskedForCoverage()
    {
        Write("a.verified.pdfua.txt", "# pdfua-snapshot v1\n# ignored sections: layout\n# ignored fields: positions, colors\n");
        Write("b.verified.pdfua.txt", "# pdfua-snapshot v1\n# ignored fields: positions\n# tolerances: font-sizes 0.5pt\n"
            + "# only pages: 1\ndocument\n");
        Write("c.verified.docx.txt", "# docx-snapshot v1\n# ignored sections: styles\n");
        Write("d.verified.json", "{}\n");
        Write("a.received.pdfua.txt", "# pdfua-snapshot v1\n# ignored sections: structure\n");
        var markdown = Path.Combine(_folder, "coverage.md");

        var exitCode = Commands.Run(["coverage", _folder, "--markdown", markdown], _output, _error);

        Assert.Equal(Commands.Same, exitCode);
        var lines = _output.ToString().Split('\n');
        Assert.Equal("2 PDF snapshot(s)", lines[0]);
        Assert.Contains($"  {"structure",-11}  2 of 2", lines);
        Assert.Contains($"  {"layout",-11}  1 of 2, ignored in 1", lines);
        Assert.Contains("  fields ignored: positions in 2, colors in 1", lines);
        Assert.Contains("  limited to some pages: 1", lines);
        Assert.Contains("  with tolerances: 1", lines);
        Assert.Contains("1 Word snapshot(s)", lines);
        Assert.Contains($"  {"styles",-9}  0 of 1, ignored in 1", lines);
        Assert.Contains("1 other snapshot(s), text or JSON, which have no sections", lines);
        Assert.Contains("| `layout` | 1 | 1 |", File.ReadAllText(markdown));
        Assert.Contains("\nLimited to some pages: 1\n\nWith tolerances: 1\n", File.ReadAllText(markdown));
    }

    [Fact]
    public void Run_ShouldSayThereAreNone_WhenAskedForCoverageOfAFolderWithoutVerifiedSnapshots()
    {
        var exitCode = Commands.Run(["coverage", _folder], _output, _error);

        Assert.Equal((Commands.Same, "No verified snapshots.\n"), (exitCode, _output.ToString()));
    }

    [Fact]
    public void Run_ShouldRefuseAndReturnTwo_WhenAskedForCoverageOfAFileThatIsNotVerified()
    {
        var received = Write("a.received.pdfua.txt", "# pdfua-snapshot v1\n");

        var exitCode = Commands.Run(["coverage", received], _output, _error);

        Assert.Equal(Commands.UsageError, exitCode);
        Assert.Contains("isn't a verified snapshot", _error.ToString());
    }

    [Fact]
    public void Run_ShouldPrintTheDocxsSnapshot_WhenGivenADocx()
    {
        var docx = WriteDocx("guide.docx", "Hello");

        Commands.Run(["snapshot", docx], _output, _error);

        Assert.Equal(DocxDocument.Load(docx).ToSnapshot(), _output.ToString());
    }

    private string WriteDocx(string name, string text, string? font = null)
    {
        var path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var properties = font is null ? "" : $"<w:rPr><w:rFonts w:ascii=\"{font}\"/></w:rPr>";
        var docx = new Docx.TestDocx { Body = $"<w:p><w:r>{properties}<w:t>{text}</w:t></w:r></w:p>" };
        File.WriteAllBytes(path, docx.Build().ToArray());
        return path;
    }

    private string WritePdf(string name, string text, string? title = null)
    {
        var pdf = new TestPdf { Title = title };
        pdf.AddPage().Tagged(0, text);
        pdf.Structure(new TestElement("P", 0));
        var path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, pdf.Build());
        return path;
    }
}
