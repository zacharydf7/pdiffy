using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using PDiffy.Diffing;

namespace PDiffy;

/// <summary>
/// The received and verified file lifecycle shared by every kind of snapshot: pass when the received text matches the
/// verified file, overwrite it in accept mode, or write the received file and a report and throw.
/// </summary>
internal static class SnapshotWorkflow
{
    // What each snapshot file received so far in this run, as a hash. Case is ignored, since names that differ only in
    // case are one file on Windows and macOS.
    private static readonly ConcurrentDictionary<string, string> ReceivedThisRun = new(StringComparer.OrdinalIgnoreCase);

    // The start of each run log's name; the rest is the process ID, since test assemblies run in parallel.
    public const string RunLogPrefix = "pdiffy-run-";

    private static readonly Lock RunLogLock = new();

    public static void Verify(string received, SnapshotFiles files, string kind)
    {
        CheckNotVerifiedBefore(received, files);
        LogRun(files);
        var verified = File.Exists(files.Verified) ? Normalize(File.ReadAllText(files.Verified)) : null;
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
            File.WriteAllText(files.Verified, received);
            File.Delete(files.Received);
            File.Delete(files.Report);
            return;
        }

        Directory.CreateDirectory(files.Folder);
        File.WriteAllText(files.Received, received);
        if (diff is null) File.Delete(files.Report);
        else File.WriteAllText(files.Report, diff.ToHtml());
        var formatChange = verified is null ? null : FormatChange(verified, received);
        throw new SnapshotException(FailureMessage(received, files, diff, kind, formatChange));
    }

    // Two calls with one name write one file, so in accept mode the last silently wins and the others fail next run. The
    // same content again is fine: a retried test.
    private static void CheckNotVerifiedBefore(string received, SnapshotFiles files)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(received)));
        if (ReceivedThisRun.GetOrAdd(Path.GetFullPath(files.Verified), hash) != hash)
        {
            throw new SnapshotException($"Snapshot '{files.Name}' was already verified in this run with different "
                + "content, so both calls would share one file. Give each call its own name with name: or variant:, or, "
                + "in a helper method, take [CallerFilePath] and [CallerMemberName] parameters and pass them on as "
                + "testFile and testName.");
        }
    }

    // Tests in one process verify in parallel, so writes to its log take turns.
    private static void LogRun(SnapshotFiles files)
    {
        if (Environment.GetEnvironmentVariable(Snapshot.RunLogVariable) is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        var log = Path.Combine(folder, $"{RunLogPrefix}{Environment.ProcessId}.log");
        lock (RunLogLock) File.AppendAllText(log, Path.GetFullPath(files.Verified) + "\n");
    }

    // The folder is the call's, else Snapshot.Directory, else the test file's; the name defaults to the test file and
    // method. A variant is added to either.
    public static SnapshotFiles Files(string? name, string? variant, string extension, string? directory, string testFile,
        string testName)
    {
        var baseName = name ?? $"{Path.GetFileNameWithoutExtension(testFile)}.{testName}";
        var folder = directory ?? Snapshot.Directory ?? TestFolder(testFile);
        return new(folder, variant is null ? baseName : $"{baseName}.{variant}", extension);
    }

    // A build that maps source paths (deterministic builds, often on CI) records a path like /_/tests/..., which doesn't
    // exist on disk, so the folder is looked for again under the project folders the test assemblies recorded.
    private static string TestFolder(string testFile)
    {
        var folder = Path.GetDirectoryName(testFile);
        if (folder is not null && Directory.Exists(folder)) return folder;
        if (folder is not null && ProjectFolders.Find(folder, ProjectFolders.Loaded()) is { } found) return found;
        throw new SnapshotException($"The test file's folder '{folder}' doesn't exist, which happens when the build maps "
            + "source paths. Set Snapshot.Directory to the folder that holds the snapshots, such as "
            + "Snapshot.ProjectFolder(\"Snapshots\").");
    }

    // A new snapshot has nothing to compare with, so it gets no diff and no report.
    private static string FailureMessage(string received, SnapshotFiles files, SnapshotDiff? diff, string kind,
        string? formatChange)
    {
        var message = new StringBuilder();
        if (formatChange is not null) message.AppendLine(formatChange).AppendLine();
        message.AppendLine(diff is null
            ? $"New {kind} snapshot. Review the received file, then accept it."
            : $"The {kind}'s snapshot differs from the verified one.");
        if (diff is null && SnapshotBreakdown.Of(received) is { } breakdown) message.AppendLine(breakdown);
        if (diff is not null && FontSubstitution.Hint(diff) is { } hint) message.AppendLine(hint);
        if (diff is null && OnCi())
        {
            message.AppendLine("This is a build server, so the verified file was probably expected: it isn't in this "
                + "checkout. Was it committed, and does the snapshot folder point into the repository?");
        }

        if (diff is not null) message.AppendLine().AppendLine(diff.Describe());
        message.AppendLine().AppendLine($"verified: {files.Verified}").AppendLine($"received: {files.Received}");
        if (diff is not null) message.AppendLine($"report: {files.Report}");
        // A command reads the same in every shell, unlike setting the variable, and the full path works from any folder.
        message.AppendLine("To accept, run:");
        message.AppendLine($"  dotnet pdiffy accept \"{Path.GetFullPath(files.Received)}\"");
        message.Append("(pdiffy accept with a global install), ");
        message.Append($"or set {Snapshot.AcceptVariable}=1 and run the test again, ");
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
