using System.Text;
using PDiffy.Diffing;

namespace PDiffy;

/// <summary>
/// The received and verified file lifecycle shared by every kind of snapshot: pass when the received text matches the
/// verified file, overwrite it in accept mode, or write the received file and a report and throw.
/// </summary>
internal static class SnapshotWorkflow
{
    public static async Task VerifyAsync(string received, SnapshotFiles files, string kind)
    {
        var verified = File.Exists(files.Verified) ? Normalize(await File.ReadAllTextAsync(files.Verified)) : null;
        var diff = verified is null || received == verified ? null : SnapshotDiff.Compare(verified, received);

        // A difference the snapshot's tolerances allow passes too, and leaves the verified file as it is, even in accept
        // mode, so the baseline doesn't creep a point at a time.
        if (received == verified || diff is { IsEmpty: true })
        {
            File.Delete(files.Received);
            File.Delete(files.Report);
            return;
        }

        if (AcceptMode())
        {
            Directory.CreateDirectory(files.Folder);
            await File.WriteAllTextAsync(files.Verified, received);
            File.Delete(files.Received);
            File.Delete(files.Report);
            return;
        }

        Directory.CreateDirectory(files.Folder);
        await File.WriteAllTextAsync(files.Received, received);
        if (diff is null) File.Delete(files.Report);
        else await File.WriteAllTextAsync(files.Report, diff.ToHtml());
        var formatChange = verified is null ? null : FormatChange(verified, received);
        throw new SnapshotException(FailureMessage(files, diff, kind, formatChange));
    }

    // The folder is the call's, else Snapshot.Directory, else the test file's; the name defaults to the test file and method.
    public static SnapshotFiles Files(string? name, string extension, string? directory, string testFile, string testName) =>
        new(directory ?? Snapshot.Directory ?? TestFolder(testFile),
            name ?? $"{Path.GetFileNameWithoutExtension(testFile)}.{testName}", extension);

    // A build that maps source paths (deterministic builds, often on CI) records a path like /_/tests/..., which doesn't
    // exist on disk, so snapshots next to the test file can't be found.
    private static string TestFolder(string testFile)
    {
        var folder = Path.GetDirectoryName(testFile);
        if (folder is null || !Directory.Exists(folder))
        {
            throw new SnapshotException($"The test file's folder '{folder}' doesn't exist, which happens when the build maps "
                + "source paths. Set Snapshot.Directory to the folder that holds the snapshots.");
        }

        return folder;
    }

    // A new snapshot has nothing to compare with, so it gets no diff and no report.
    private static string FailureMessage(SnapshotFiles files, SnapshotDiff? diff, string kind, string? formatChange)
    {
        var message = new StringBuilder();
        if (formatChange is not null) message.AppendLine(formatChange).AppendLine();
        message.AppendLine(diff is null
            ? $"New {kind} snapshot. Review the received file, then accept it."
            : $"The {kind}'s snapshot differs from the verified one.");
        if (diff is not null) message.AppendLine().AppendLine(diff.Describe());
        message.AppendLine().AppendLine($"verified: {files.Verified}").AppendLine($"received: {files.Received}");
        if (diff is not null) message.AppendLine($"report: {files.Report}");
        message.Append($"To accept: set {Snapshot.AcceptVariable}=1 and run the test again, ");
        message.Append("or copy the received file over the verified one.");
        return message.ToString();
    }

    // A new format version changes nearly every snapshot, so the failure says that first, before the document's changes.
    private static string? FormatChange(string verified, string received)
    {
        var (before, after) = (verified.Split('\n')[0], received.Split('\n')[0]);
        if (before == after || !SnapshotTree.IsSectioned(verified) || !SnapshotTree.IsSectioned(received)) return null;

        return $"The verified file uses {before[2..]}, and this version of PDiffy writes {after[2..]}. The format "
            + "changes are listed in PDiffy's README; review the received file, then accept it.";
    }

    // Git can check text files out with CRLF on Windows; snapshots are always compared with LF. Only CRLF is converted:
    // other line separators, such as U+2028 in alt text, are part of the content.
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    internal static bool AcceptMode() =>
        Environment.GetEnvironmentVariable(Snapshot.AcceptVariable) is "1" or "true" && !OnCi();

    // CI and TF_BUILD are what most build servers (GitHub Actions, Bitbucket, GitLab, Azure DevOps) set.
    private static bool OnCi() =>
        Environment.GetEnvironmentVariable("CI") is { Length: > 0 } ci && ci != "false" && ci != "0"
        || Environment.GetEnvironmentVariable("TF_BUILD") is { Length: > 0 };
}

/// <summary>A snapshot's three files: <c>{name}.verified.{extension}</c>, <c>{name}.received.{extension}</c> and the report.</summary>
internal sealed record SnapshotFiles(string Folder, string Name, string Extension)
{
    public const string VerifiedPart = ".verified.";
    public const string ReceivedPart = ".received.";
    public const string ReportSuffix = ".diff.html";

    public string Verified => Path.Combine(Folder, Name + VerifiedPart + Extension);

    public string Received => Path.Combine(Folder, Name + ReceivedPart + Extension);

    public string Report => Path.Combine(Folder, Name + ReportSuffix);
}
