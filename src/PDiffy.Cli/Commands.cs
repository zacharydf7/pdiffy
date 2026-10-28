namespace PDiffy.Cli;

/// <summary>
/// The pdiffy commands. Each writes its results to <c>output</c> and any problem or usage text to <c>error</c>, and
/// returns the process exit code.
/// </summary>
public static class Commands
{
    public const int Same = 0;
    public const int Different = 1;
    public const int UsageError = 2;

    public const string Usage = """
        Usage:
          pdiffy <before> <after> [--html <report.html>]   Compare two PDFs or two snapshot files
          pdiffy snapshot <file.pdf> [<out.pdfua.txt>]      Print or save a PDF's snapshot
          pdiffy accept [<files or folders>] [--all]        Accept received snapshots as verified

        compare exits with 0 when nothing changed and 1 when something did.
        accept takes the current folder by default; --all includes subfolders.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            return args switch
            {
                ["snapshot", var pdf] => Snapshot(pdf, null, output),
                ["snapshot", var pdf, var file] => Snapshot(pdf, file, output),
                ["accept", .. var rest] => Accept(rest, output, error),
                [var before, var after] => Compare(before, after, null, output),
                [var before, var after, "--html", var report] => Compare(before, after, report, output),
                _ => ShowUsage(error),
            };
        }
        // At the command line, a missing file or a PDF that can't be opened is reported as a message, not a stack trace.
        catch (Exception problem)
        {
            error.WriteLine($"pdiffy: {problem.Message}");
            return UsageError;
        }
    }

    private static int Compare(string before, string after, string? report, TextWriter output)
    {
        var diff = SnapshotDiff.Compare(SnapshotOf(before), SnapshotOf(after));
        output.WriteLine(diff.Describe(int.MaxValue));
        if (report is not null)
        {
            File.WriteAllText(report, diff.ToHtml());
            output.WriteLine($"report: {report}");
        }

        return diff.IsEmpty ? Same : Different;
    }

    private static int Snapshot(string pdf, string? file, TextWriter output)
    {
        var snapshot = PdfUaDocument.Load(pdf).ToSnapshot();
        if (file is null) output.Write(snapshot);
        else File.WriteAllText(file, snapshot);
        return Same;
    }

    // Each received snapshot replaces its verified one, and its report goes, as when a test runs in accept mode. Several
    // targets are allowed because shells expand a pattern like *.received.* into many. Each file keeps its extension:
    // Letter.received.json becomes Letter.verified.json.
    private static int Accept(string[] args, TextWriter output, TextWriter error)
    {
        var search = args.Contains("--all") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var targets = args.Where(arg => arg != "--all").DefaultIfEmpty(".");
        var pattern = $"*{SnapshotFiles.ReceivedPart}*";
        var received = targets
            .SelectMany(target => File.Exists(target) ? [target] : Directory.GetFiles(target, pattern, search))
            .Select(Path.GetFullPath).Distinct().ToList();
        if (received.FirstOrDefault(file => !Path.GetFileName(file).Contains(SnapshotFiles.ReceivedPart)) is { } wrong)
        {
            error.WriteLine($"pdiffy: {wrong} isn't a received snapshot ({pattern}).");
            return UsageError;
        }

        foreach (var file in received)
        {
            var fileName = Path.GetFileName(file);
            var at = fileName.LastIndexOf(SnapshotFiles.ReceivedPart, StringComparison.Ordinal);
            var name = Path.Combine(Path.GetDirectoryName(file)!, fileName[..at]);
            var verified = name + SnapshotFiles.VerifiedPart + fileName[(at + SnapshotFiles.ReceivedPart.Length)..];
            File.Move(file, verified, overwrite: true);
            File.Delete(name + SnapshotFiles.ReportSuffix);
            output.WriteLine($"accepted {verified}");
        }

        if (received.Count == 0) output.WriteLine("No received snapshots to accept.");
        return Same;
    }

    // A PDF is read and snapshotted; anything else is taken to be a snapshot already.
    private static string SnapshotOf(string path) =>
        IsPdf(path) ? PdfUaDocument.Load(path).ToSnapshot() : File.ReadAllText(path);

    // PDF readers accept the %PDF- header anywhere in the first 1024 bytes, after a byte-order mark or other junk.
    private static bool IsPdf(string path)
    {
        using var file = File.OpenRead(path);
        var start = new byte[1024];
        var length = file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
        return start.AsSpan(0, length).IndexOf("%PDF-"u8) >= 0;
    }

    private static int ShowUsage(TextWriter error)
    {
        error.WriteLine(Usage);
        return UsageError;
    }
}
