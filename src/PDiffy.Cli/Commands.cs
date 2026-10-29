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
          pdiffy <before> <after> [--html <report.html>]   Compare two PDFs, two DOCX files or two snapshot files
                 [--json [<changes.json>]]                  --json prints the changes as JSON instead, or saves them
          pdiffy snapshot <file.pdf|file.docx> [<out>]      Print or save a PDF's or DOCX's snapshot
          pdiffy accept [<files or folders>] [--all]        Accept received snapshots as verified
          pdiffy report [<files or folders>] [--all] [--html <summary.html>] [--markdown <summary.md>]
                                                            Summarize received snapshots

        compare exits with 0 when nothing changed and 1 when something did; report, with 0 when no snapshot was received
        and 1 when one was.
        accept and report take the current folder by default; --all includes subfolders.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            return args switch
            {
                ["snapshot", var document] => Snapshot(document, null, output),
                ["snapshot", var document, var file] => Snapshot(document, file, output),
                ["accept", .. var rest] => Accept(rest, output),
                ["report", .. var rest] => Report(rest, output),
                [var before, var after, .. var options] => Compare(before, after, options, output, error),
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

    // With --json and no file, the output is only JSON, so a script can read it straight from the pipe.
    private static int Compare(string before, string after, string[] options, TextWriter output, TextWriter error)
    {
        var (report, json, printJson) = ((string?)null, (string?)null, false);
        for (var i = 0; i < options.Length; i++)
        {
            var hasValue = i + 1 < options.Length && !options[i + 1].StartsWith("--", StringComparison.Ordinal);
            if (options[i] == "--html" && hasValue) report = options[++i];
            else if (options[i] == "--json" && hasValue) json = options[++i];
            else if (options[i] == "--json") printJson = true;
            else return ShowUsage(error);
        }

        var diff = SnapshotDiff.Compare(SnapshotOf(before), SnapshotOf(after));
        if (printJson) output.Write(diff.ToJson());
        else output.WriteLine(diff.Describe(int.MaxValue));
        if (json is not null) File.WriteAllText(json, diff.ToJson());
        if (report is not null)
        {
            File.WriteAllText(report, diff.ToHtml());
            if (!printJson) output.WriteLine($"report: {report}");
        }

        return diff.IsEmpty ? Same : Different;
    }

    private static int Snapshot(string document, string? file, TextWriter output)
    {
        var snapshot = IsDocx(document) ? DocxDocument.Load(document).ToSnapshot() : PdfUaDocument.Load(document).ToSnapshot();
        if (file is null) output.Write(snapshot);
        else File.WriteAllText(file, snapshot);
        return Same;
    }

    // Each received snapshot replaces its verified one, and its report goes, as when a test runs in accept mode.
    private static int Accept(string[] args, TextWriter output)
    {
        var pending = PendingSnapshot.Find(args.Where(arg => arg != "--all"), args.Contains("--all"));
        foreach (var snapshot in pending)
        {
            File.Move(snapshot.Received, snapshot.Verified, overwrite: true);
            File.Delete(snapshot.Report);
            output.WriteLine($"accepted {snapshot.Verified}");
        }

        if (pending.Count == 0) output.WriteLine("No received snapshots to accept.");
        return Same;
    }

    // The summary always prints; --html and --markdown also save it, for a build's artifacts or a CI job summary.
    private static int Report(string[] args, TextWriter output)
    {
        var (targets, html, markdown) = (new List<string>(), (string?)null, (string?)null);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--html" or "--markdown" && i + 1 == args.Length)
            {
                throw new ArgumentException($"{args[i]} needs a file.");
            }

            if (args[i] == "--html") html = args[++i];
            else if (args[i] == "--markdown") markdown = args[++i];
            else if (args[i] != "--all") targets.Add(args[i]);
        }

        var pending = PendingSnapshot.Find(targets, args.Contains("--all"));
        var summary = new PendingSummary(pending);
        output.Write(summary.ToText());
        if (html is not null) File.WriteAllText(html, summary.ToHtml(Path.GetDirectoryName(Path.GetFullPath(html))!));
        // A CI job summary file collects what every command in the step writes, so the table is added at its end.
        if (markdown is not null) File.AppendAllText(markdown, summary.ToMarkdown());
        return pending.Count == 0 ? Same : Different;
    }

    // A PDF or a DOCX is read and snapshotted; anything else is taken to be a snapshot already.
    private static string SnapshotOf(string path) =>
        IsPdf(path) ? PdfUaDocument.Load(path).ToSnapshot()
        : IsDocx(path) ? DocxDocument.Load(path).ToSnapshot()
        : File.ReadAllText(path);

    private static bool IsDocx(string path) => Path.GetExtension(path).Equals(".docx", StringComparison.OrdinalIgnoreCase);

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
