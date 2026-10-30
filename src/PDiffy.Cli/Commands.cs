using System.Reflection;

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
                 [--profile <profile>]                      content, accessibility, layout or strict (the default)
          pdiffy compare <before> <after> [--html <index.html>] [--json [<changes.json>]] [--profile <profile>]
                                                            Compare two folders, file by file
          pdiffy snapshot <file.pdf|file.docx> [<out>]      Print or save a PDF's or DOCX's snapshot
          pdiffy check <file.pdf>                           Run every built-in check on a PDF
          pdiffy accept [<files or folders>] [--all]        Accept received snapshots as verified
          pdiffy report [<files or folders>] [--all] [--html <summary.html>] [--markdown <summary.md>]
                                                            Summarize received snapshots
          pdiffy coverage [<files or folders>] [--all] [--markdown <coverage.md>]
                                                            Count what the verified snapshots compare
          pdiffy findings [<files or folders>] [--all] [--html <findings.html>] [--markdown <findings.md>]
                                                            Count the check findings the verified snapshots hold
          pdiffy unused [<folders>] --run-log <folder> [--all] [--delete]
                                                            List or delete snapshots no test used in a run
          pdiffy --version                                  Print the version and the snapshot formats it writes
          pdiffy --help, -h                                 Print this help

        Comparing exits with 0 when nothing changed and 1 when something did; check, with 0 when it finds nothing and 1
        when it finds something; report, with 0 when no snapshot was received and 1 when one was; unused, with 0 when
        every snapshot was used or --delete removed the rest, and 1 when it lists some.
        coverage and findings always exit with 0.
        accept, report, coverage, findings and unused take the current folder by default; --all includes subfolders.
        """;

    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            return args switch
            {
                ["--version"] => Version(output),
                ["--help" or "-h"] => Help(output),
                ["snapshot", var document] => Snapshot(document, null, output),
                ["snapshot", var document, var file] => Snapshot(document, file, output),
                ["check", var document] => Check(document, output),
                ["accept", .. var rest] => Accept(rest, output),
                ["report", .. var rest] => Report(rest, output),
                ["coverage", .. var rest] => Coverage(rest, output),
                ["findings", .. var rest] => Findings(rest, output),
                ["unused", .. var rest] => Unused(rest, output),
                ["compare", var before, var after, .. var options] =>
                    CompareFolders(before, after, options, output, error),
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
        if (CompareOptions.Parse(options) is not { } parsed) return ShowUsage(error);

        var diff = CompareFiles(before, after, parsed.Profile);
        if (parsed.PrintJson) output.Write(diff.ToJson());
        else output.WriteLine(diff.Describe(int.MaxValue));
        if (parsed.Json is not null) File.WriteAllText(parsed.Json, diff.ToJson());
        if (parsed.Html is not null)
        {
            File.WriteAllText(parsed.Html, diff.ToHtml());
            if (!parsed.PrintJson) output.WriteLine($"report: {parsed.Html}");
        }

        return diff.IsEmpty ? Same : Different;
    }

    private static int CompareFolders(string before, string after, string[] options, TextWriter output, TextWriter error)
    {
        if (CompareOptions.Parse(options) is not { } parsed) return ShowUsage(error);

        var comparison = FolderComparison.Run(before, after, parsed.Profile);
        if (parsed.PrintJson) output.Write(comparison.ToJson());
        else output.Write(comparison.ToText());
        if (parsed.Json is not null) File.WriteAllText(parsed.Json, comparison.ToJson());
        if (parsed.Html is not null)
        {
            comparison.SaveHtml(parsed.Html);
            if (!parsed.PrintJson) output.WriteLine($"report: {parsed.Html}");
        }

        return comparison.Differences.Count == 0 ? Same : Different;
    }

    private static int Snapshot(string document, string? file, TextWriter output)
    {
        var snapshot = DocumentFiles.IsDocx(document) ? DocxDocument.Load(document).ToSnapshot()
            : PdfUaDocument.Load(document).ToSnapshot();
        if (file is null) output.Write(snapshot);
        else File.WriteAllText(file, snapshot);
        return Same;
    }

    private static int Check(string document, TextWriter output)
    {
        var findings = PdfUaDocument.Load(document).Check();
        output.WriteLine(findings.Count == 0 ? "No findings." : $"{findings.Count} finding(s):");
        foreach (var finding in findings) output.WriteLine(finding);
        return findings.Count == 0 ? Same : Different;
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
        var (targets, html, markdown) = SummaryOptions(args);
        var pending = PendingSnapshot.Find(targets, args.Contains("--all"));
        var summary = new PendingSummary(pending);
        output.Write(summary.ToText());
        if (html is not null) File.WriteAllText(html, summary.ToHtml(Path.GetDirectoryName(Path.GetFullPath(html))!));
        // A CI job summary file collects what every command in the step writes, so the table is added at its end.
        if (markdown is not null) File.AppendAllText(markdown, summary.ToMarkdown());
        return pending.Count == 0 ? Same : Different;
    }

    // Only the verified snapshots' headers are read, so a whole suite is summarized quickly, without its documents.
    private static int Coverage(string[] args, TextWriter output)
    {
        var (targets, markdown) = (new List<string>(), (string?)null);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--markdown" && i + 1 == args.Length) throw new ArgumentException("--markdown needs a file.");
            if (args[i] == "--markdown") markdown = args[++i];
            else if (args[i] != "--all") targets.Add(args[i]);
        }

        var verified = SnapshotFileSearch.Find(targets, args.Contains("--all"), SnapshotFiles.VerifiedPart);
        var coverage = SnapshotCoverage.Of(verified);
        output.Write(coverage.ToText());
        if (markdown is not null) File.AppendAllText(markdown, coverage.ToMarkdown());
        return Same;
    }

    // A summary, not a gate: the approved snapshots already hold their findings, so nothing is regenerated.
    private static int Findings(string[] args, TextWriter output)
    {
        var (targets, html, markdown) = SummaryOptions(args);
        var verified = SnapshotFileSearch.Find(targets, args.Contains("--all"), SnapshotFiles.VerifiedPart);
        var counts = FindingCounts.Of(verified);
        output.Write(counts.ToText());
        if (html is not null) File.WriteAllText(html, counts.ToHtml());
        if (markdown is not null) File.AppendAllText(markdown, counts.ToMarkdown());
        return Same;
    }

    // The files or folders, and the --html and --markdown files, that report and findings take.
    private static (List<string> Targets, string? Html, string? Markdown) SummaryOptions(string[] args)
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

        return (targets, html, markdown);
    }

    // A test that didn't run looks unused, so this is only meaningful after a full, unfiltered run.
    private static int Unused(string[] args, TextWriter output)
    {
        var (folders, runLog) = (new List<string>(), (string?)null);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--run-log" && i + 1 == args.Length)
            {
                throw new ArgumentException("--run-log needs a folder.");
            }

            if (args[i] == "--run-log") runLog = args[++i];
            else if (args[i] is not ("--all" or "--delete")) folders.Add(args[i]);
        }

        if (runLog is null)
        {
            throw new ArgumentException($"unused needs --run-log, the folder {PDiffy.Snapshot.RunLogVariable} named.");
        }

        var delete = args.Contains("--delete");
        var unused = UnusedSnapshots.Find(folders, args.Contains("--all"), runLog);
        foreach (var file in unused)
        {
            if (delete) File.Delete(file);
            output.WriteLine(delete ? $"deleted {file}" : file);
        }

        if (unused.Count == 0) output.WriteLine("No unused snapshots.");
        return unused.Count == 0 || delete ? Same : Different;
    }

    // A snapshot file is compared as it is, which a profile can't change. A Word document takes the profile of the same
    // name, or Strict when there's none.
    internal static SnapshotDiff CompareFiles(string before, string after, PdfProfile profile)
    {
        var docxProfile = Enum.TryParse<DocxProfile>(profile.ToString(), out var named) ? named : DocxProfile.Strict;
        return SnapshotDiff.CompareFiles(before, after, options => options.Use(profile),
            options => options.Use(docxProfile));
    }

    // The library's version decides how documents are read, so it's the one that has to match the tests' package.
    private static int Version(TextWriter output)
    {
        var version = typeof(PdfUaDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0];
        output.WriteLine($"pdiffy {version} ({SnapshotWriter.FormatHeader[2..]}, {DocxSnapshotWriter.FormatHeader[2..]})");
        return Same;
    }

    private static int Help(TextWriter output)
    {
        output.WriteLine(Usage);
        return Same;
    }

    private static int ShowUsage(TextWriter error)
    {
        error.WriteLine(Usage);
        return UsageError;
    }

    /// <summary>The options both compare commands take.</summary>
    private sealed record CompareOptions(string? Html, string? Json, bool PrintJson, PdfProfile Profile)
    {
        // Null when an option is unknown or is missing its value.
        public static CompareOptions? Parse(string[] options)
        {
            var parsed = new CompareOptions(null, null, false, PdfProfile.Strict);
            for (var i = 0; i < options.Length; i++)
            {
                var value = i + 1 < options.Length && !options[i + 1].StartsWith("--", StringComparison.Ordinal)
                    ? options[i + 1]
                    : null;
                if (options[i] == "--html" && value is not null) parsed = parsed with { Html = options[++i] };
                else if (options[i] == "--json" && value is not null) parsed = parsed with { Json = options[++i] };
                else if (options[i] == "--json") parsed = parsed with { PrintJson = true };
                // Only a name: Enum.TryParse also takes numbers and comma lists, which would pick another profile.
                else if (options[i] == "--profile"
                    && Enum.GetNames<PdfProfile>().Contains(value, StringComparer.OrdinalIgnoreCase)
                    && Enum.TryParse<PdfProfile>(value, ignoreCase: true, out var profile))
                {
                    parsed = parsed with { Profile = profile };
                    i++;
                }
                else return null;
            }

            return parsed;
        }
    }
}
