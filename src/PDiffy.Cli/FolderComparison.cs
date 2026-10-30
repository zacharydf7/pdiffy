using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PDiffy.Cli;

/// <summary>How a file in one folder compares with the file at the same path in the other.</summary>
internal enum FileStatus { Changed, Added, Removed, Unreadable }

/// <summary>A file that differs between the folders: its changes, or why it couldn't be read.</summary>
internal sealed record FileDifference(string Path, FileStatus Status, SnapshotDiff? Diff = null, string? Problem = null);

/// <summary>
/// Two folders compared file by file, paired by each file's path inside its folder. PDFs and DOCX files are compared
/// as documents and .txt and .json files as snapshots; other files are counted but not compared.
/// </summary>
internal sealed class FolderComparison
{
    private static readonly HashSet<string> ComparedExtensions = new([".pdf", ".docx", ".txt", ".json"],
        StringComparer.OrdinalIgnoreCase);

    private FolderComparison(List<FileDifference> differences, int unchanged, int notCompared) =>
        (Differences, Unchanged, NotCompared) = (differences, unchanged, notCompared);

    /// <summary>Changed files first, then added, removed and unreadable ones, each by path.</summary>
    public IReadOnlyList<FileDifference> Differences { get; }

    public int Unchanged { get; }

    public int NotCompared { get; }

    public static FolderComparison Run(string before, string after, PdfProfile profile)
    {
        var (beforeFiles, afterFiles) = (FilesIn(before), FilesIn(after));
        var paths = beforeFiles.Keys.Union(afterFiles.Keys).ToList();
        var differences = new List<FileDifference>();
        var unchanged = 0;
        foreach (var path in paths.Where(path => ComparedExtensions.Contains(Path.GetExtension(path))))
        {
            var (beforeFile, afterFile) = (beforeFiles.GetValueOrDefault(path), afterFiles.GetValueOrDefault(path));
            var difference = Compare(path, beforeFile, afterFile, profile);
            if (difference is null) unchanged++;
            else differences.Add(difference);
        }

        differences = differences.OrderBy(difference => difference.Status)
            .ThenBy(difference => difference.Path, StringComparer.Ordinal).ToList();
        var notCompared = paths.Count(path => !ComparedExtensions.Contains(Path.GetExtension(path)));
        return new FolderComparison(differences, unchanged, notCompared);
    }

    public string Headline
    {
        get
        {
            var rest = $"{Unchanged} unchanged" + (NotCompared > 0 ? $", {NotCompared} not compared" : "");
            if (Differences.Count == 0) return $"No files differ: {rest}.";
            var counts = Differences.GroupBy(file => file.Status).Select(group => $"{group.Count()} {NameOf(group.Key)}");
            return $"{Differences.Count} file(s) differ: {string.Join(", ", counts)}; {rest}.";
        }
    }

    public string ToText() =>
        string.Concat(Differences.Select(file => ChangeSummary.TextLine(RowOf(file)) + "\n").Prepend(Headline + "\n"));

    /// <summary>
    /// One entry per file that differs, with a changed file's changes as <see cref="SnapshotDiff.ToJson"/> writes them.
    /// </summary>
    public string ToJson()
    {
        var files = new JsonArray();
        foreach (var file in Differences)
        {
            var entry = new JsonObject { ["path"] = file.Path, ["status"] = NameOf(file.Status) };
            if (file.Diff is not null) entry["changes"] = JsonNode.Parse(file.Diff.ToJson())!["changes"]!.DeepClone();
            if (file.Problem is not null) entry["problem"] = file.Problem;
            files.Add(entry);
        }

        var root = new JsonObject
        {
            ["version"] = 1,
            ["unchanged"] = Unchanged,
            ["notCompared"] = NotCompared,
            ["files"] = files,
        };
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, Snapshot.WriterOptions)) root.WriteTo(writer);
        return Encoding.UTF8.GetString(output.ToArray()) + "\n";
    }

    /// <summary>
    /// Saves a page at <paramref name="index"/> with one row per file that differs, and, next to it, each changed
    /// file's report at the file's path with .diff.html added.
    /// </summary>
    public void SaveHtml(string index)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(index))!;
        Directory.CreateDirectory(folder);
        var rows = new List<SummaryRow>();
        foreach (var file in Differences)
        {
            var report = file.Diff is null ? null : Path.Combine(folder, file.Path + SnapshotFiles.ReportSuffix);
            if (report is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(report)!);
                File.WriteAllText(report, file.Diff!.ToHtml());
            }

            rows.Add(RowOf(file) with { Report = report });
        }

        File.WriteAllText(index, ChangeSummary.Page("Compared folders", Headline, "File", rows, folder));
    }

    private static SummaryRow RowOf(FileDifference file) => new(NameOf(file.Status), file.Path, file.Diff, file.Problem);

    // A file that can't be read is a difference, so one damaged document doesn't stop the rest being compared.
    private static FileDifference? Compare(string path, string? before, string? after, PdfProfile profile)
    {
        if (before is null) return new FileDifference(path, FileStatus.Added);
        if (after is null) return new FileDifference(path, FileStatus.Removed);
        try
        {
            var diff = SnapshotDiff.Compare(Commands.SnapshotOf(before, profile), Commands.SnapshotOf(after, profile));
            return diff.IsEmpty ? null : new FileDifference(path, FileStatus.Changed, diff);
        }
        catch (Exception problem)
        {
            return new FileDifference(path, FileStatus.Unreadable, Problem: problem.Message);
        }
    }

    private static string NameOf(FileStatus status) => status.ToString().ToLowerInvariant();

    // Keyed by the path inside the folder, with forward slashes, so the same layout pairs up on every OS.
    private static Dictionary<string, string> FilesIn(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException($"{folder} isn't a folder.");
        return Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/'));
    }
}
