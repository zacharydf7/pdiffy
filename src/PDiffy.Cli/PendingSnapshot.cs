namespace PDiffy.Cli;

/// <summary>A received snapshot waiting to be accepted, with the verified file it replaces and its report.</summary>
internal sealed record PendingSnapshot(string Received, string Verified, string Report)
{
    private static readonly string Pattern = $"*{SnapshotFiles.ReceivedPart}*";

    // Windows file names ignore case, so tests/Snaps and tests/snaps reach the same files.
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// The received snapshots in the targets, each once. Several targets are allowed because shells expand a pattern
    /// like *.received.* into many; no target means the current folder.
    /// </summary>
    public static List<PendingSnapshot> Find(IEnumerable<string> targets, bool includeSubfolders)
    {
        var search = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var received = targets.DefaultIfEmpty(".")
            .SelectMany(target => File.Exists(target) ? [target] : Directory.GetFiles(target, Pattern, search))
            .Select(Path.GetFullPath).Distinct(PathComparer).Order(StringComparer.Ordinal).ToList();
        if (received.FirstOrDefault(file => !Path.GetFileName(file).Contains(SnapshotFiles.ReceivedPart)) is { } wrong)
        {
            throw new ArgumentException($"{wrong} isn't a received snapshot ({Pattern}).");
        }

        return received.Select(Of).ToList();
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
