namespace PDiffy.Cli;

/// <summary>A received snapshot waiting to be accepted, with the verified file it replaces and its report.</summary>
internal sealed record PendingSnapshot(string Received, string Verified, string Report)
{
    /// <summary>The received snapshots in the targets, each once; no target means the current folder.</summary>
    public static List<PendingSnapshot> Find(IEnumerable<string> targets, bool includeSubfolders) =>
        SnapshotFileSearch.Find(targets, includeSubfolders, SnapshotFiles.ReceivedPart).ConvertAll(Of);

    // Each file keeps its extension: Guide.received.json goes with Guide.verified.json and Guide.diff.html.
    private static PendingSnapshot Of(string received)
    {
        var fileName = Path.GetFileName(received);
        var at = fileName.LastIndexOf(SnapshotFiles.ReceivedPart, StringComparison.Ordinal);
        var name = Path.Combine(Path.GetDirectoryName(received)!, fileName[..at]);
        var verified = name + SnapshotFiles.VerifiedPart + fileName[(at + SnapshotFiles.ReceivedPart.Length)..];
        return new PendingSnapshot(received, verified, name + SnapshotFiles.ReportSuffix);
    }
}
