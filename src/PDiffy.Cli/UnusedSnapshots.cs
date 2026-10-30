namespace PDiffy.Cli;

/// <summary>
/// Verified and received snapshots, and their reports, that no snapshot call used in a test run, from the logs the run
/// wrote with <see cref="Snapshot.RunLogVariable"/> set.
/// </summary>
internal static class UnusedSnapshots
{
    public static List<string> Find(IReadOnlyList<string> folders, bool includeSubfolders, string runLog)
    {
        if (folders.FirstOrDefault(File.Exists) is { } file)
        {
            throw new ArgumentException($"{file} is a file; unused takes folders.");
        }

        var logs = Directory.Exists(runLog) ? Directory.GetFiles(runLog, $"{SnapshotWorkflow.RunLogPrefix}*.log") : [];
        if (logs.Length == 0)
        {
            throw new ArgumentException(
                $"{runLog} has no run logs. Set {Snapshot.RunLogVariable} to it and run every test first.");
        }

        // Names ignore case, as they do on Windows and macOS, the same way a run spots two calls sharing a file. A
        // test process stopped mid-write can leave a cut-off last line, which is skipped.
        var used = logs.SelectMany(File.ReadLines).Where(path => path.Contains(SnapshotFiles.VerifiedPart))
            .Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedNames = used.Select(path => NameOf(path, SnapshotFiles.VerifiedPart))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var verified = SnapshotFileSearch.Find(folders, includeSubfolders, SnapshotFiles.VerifiedPart)
            .Where(path => !used.Contains(Path.GetFullPath(path)));
        var received = PendingSnapshot.Find(folders, includeSubfolders)
            .Where(snapshot => !used.Contains(Path.GetFullPath(snapshot.Verified))).Select(snapshot => snapshot.Received);
        // A report's name has no extension, so it goes with any verified file of that name.
        var reports = SnapshotFileSearch.Find(folders, includeSubfolders, SnapshotFiles.ReportSuffix)
            .Where(path => !usedNames.Contains(NameOf(path, SnapshotFiles.ReportSuffix)));
        return verified.Concat(received).Concat(reports).Order(StringComparer.Ordinal).ToList();
    }

    // A snapshot's folder and name, without its extension: what its report is named after.
    private static string NameOf(string path, string part)
    {
        var name = Path.GetFileName(path);
        var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
        return Path.Combine(folder, name[..name.LastIndexOf(part, StringComparison.Ordinal)]);
    }
}
