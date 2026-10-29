namespace PDiffy.Cli;

/// <summary>A received snapshot waiting to be accepted, with the verified file it replaces and its report.</summary>
internal sealed record PendingSnapshot(string Received, string Verified, string Report)
{
    private static readonly string Pattern = $"*{SnapshotFiles.ReceivedPart}*";

    private static readonly EnumerationOptions AnyCase = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = 0,
    };

    /// <summary>
    /// The received snapshots in the targets, each once. Several targets are allowed because shells expand a pattern
    /// like *.received.* into many; no target means the current folder.
    /// </summary>
    public static List<PendingSnapshot> Find(IEnumerable<string> targets, bool includeSubfolders)
    {
        var search = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var received = targets.DefaultIfEmpty(".").Select(SpelledOnDisk)
            .SelectMany(target => File.Exists(target) ? [target] : Directory.GetFiles(target, Pattern, search))
            .Distinct().Order(StringComparer.Ordinal).ToList();
        if (received.FirstOrDefault(file => !Path.GetFileName(file).Contains(SnapshotFiles.ReceivedPart)) is { } wrong)
        {
            throw new ArgumentException($"{wrong} isn't a received snapshot ({Pattern}).");
        }

        return received.Select(Of).ToList();
    }

    // A target spelled as the file system has it, so tests/Snaps and tests/snaps reach the same files where names
    // ignore case (Windows, and macOS by default). A name is respelled only when the volume finds it as typed, so where
    // names respect case a wrong-case target still isn't found. Files found in a folder are already spelled this way.
    private static string SpelledOnDisk(string target)
    {
        var path = Path.GetFullPath(target);
        var root = Path.GetPathRoot(path)!;
        var spelled = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        var parts = path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var typed = Path.Combine(spelled, part);
            var names = Path.Exists(typed)
                ? Directory.EnumerateFileSystemEntries(spelled, part, AnyCase).Select(Path.GetFileName).ToList()
                : [];
            spelled = names.Count == 1 ? Path.Combine(spelled, names[0]!) : typed;
        }

        return spelled;
    }

    // Each file keeps its extension: Letter.received.json goes with Letter.verified.json and Letter.diff.html.
    private static PendingSnapshot Of(string received)
    {
        var fileName = Path.GetFileName(received);
        var at = fileName.LastIndexOf(SnapshotFiles.ReceivedPart, StringComparison.Ordinal);
        var name = Path.Combine(Path.GetDirectoryName(received)!, fileName[..at]);
        var verified = name + SnapshotFiles.VerifiedPart + fileName[(at + SnapshotFiles.ReceivedPart.Length)..];
        return new PendingSnapshot(received, verified, name + SnapshotFiles.ReportSuffix);
    }
}
