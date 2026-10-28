using System.Runtime.CompilerServices;
using System.Text;

namespace PDiffy;

/// <summary>
/// Snapshot tests for PDFs. <see cref="VerifyAsync"/> compares a PDF's snapshot with the approved one in
/// <c>{name}.verified.pdfua.txt</c> and throws <see cref="PdfSnapshotException"/> when they differ, so it works with any
/// test framework.
/// </summary>
public static class PdfSnapshot
{
    /// <summary>Set PDIFFY_ACCEPT=1 to accept every received snapshot as the new verified one. Ignored on CI.</summary>
    public const string AcceptVariable = "PDIFFY_ACCEPT";

    internal const string VerifiedSuffix = ".verified.pdfua.txt";
    internal const string ReceivedSuffix = ".received.pdfua.txt";
    internal const string ReportSuffix = ".diff.html";

    /// <summary>
    /// The folder that holds snapshot files. When null, each snapshot sits next to the test file that made it.
    /// </summary>
    public static string? Directory { get; set; }

    /// <summary>
    /// Compares the PDF's snapshot with the verified one. On a mismatch it writes <c>{name}.received.pdfua.txt</c> and an
    /// HTML report, <c>{name}.diff.html</c>, and throws; when they match it deletes both if an earlier run left them.
    /// </summary>
    /// <param name="pdf">The PDF to check. The stream is left open.</param>
    /// <param name="name">The snapshot's file name without extension. Defaults to the test file and method, such as
    /// "InvoiceTests.Invoice_ShouldMatch". Pass one when a test makes several snapshots, as a theory does.</param>
    /// <param name="configure">What to leave out or scrub, as for <see cref="PdfUaDocument.ToSnapshot"/>.</param>
    /// <param name="testFile">Filled in by the compiler with the calling test's file; leave it out.</param>
    /// <param name="testName">Filled in by the compiler with the calling test's method; leave it out.</param>
    public static async Task VerifyAsync(Stream pdf, string? name = null, Action<SnapshotOptions>? configure = null,
        [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "")
    {
        var received = PdfUaDocument.Load(pdf).ToSnapshot(configure);
        var folder = Directory ?? TestFolder(testFile);
        var files = new SnapshotFiles(folder, name ?? $"{Path.GetFileNameWithoutExtension(testFile)}.{testName}");
        var verified = File.Exists(files.Verified) ? Normalize(await File.ReadAllTextAsync(files.Verified)) : null;

        if (received == verified)
        {
            File.Delete(files.Received);
            File.Delete(files.Report);
            return;
        }

        if (AcceptMode())
        {
            System.IO.Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(files.Verified, received);
            File.Delete(files.Received);
            File.Delete(files.Report);
            return;
        }

        System.IO.Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(files.Received, received);
        var diff = verified is null ? null : SnapshotDiff.Compare(verified, received);
        if (diff is null) File.Delete(files.Report);
        else await File.WriteAllTextAsync(files.Report, diff.ToHtml());
        throw new PdfSnapshotException(FailureMessage(files, diff));
    }

    // A new snapshot has nothing to compare with, so it gets no diff and no report.
    private static string FailureMessage(SnapshotFiles files, SnapshotDiff? diff)
    {
        var message = new StringBuilder();
        message.AppendLine(diff is null
            ? "New PDF snapshot. Review the received file, then accept it."
            : "The PDF's snapshot differs from the verified one.");
        if (diff is not null) message.AppendLine().AppendLine(diff.Describe());
        message.AppendLine().AppendLine($"verified: {files.Verified}").AppendLine($"received: {files.Received}");
        if (diff is not null) message.AppendLine($"report: {files.Report}");
        message.Append($"To accept: set {AcceptVariable}=1 and run the test again, ");
        message.Append("or copy the received file over the verified one.");
        return message.ToString();
    }

    // Git can check text files out with CRLF on Windows; snapshots are always compared with LF. Only CRLF is converted:
    // other line separators, such as U+2028 in alt text, are part of the content.
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    // A build that maps source paths (deterministic builds, often on CI) records a path like /_/tests/..., which doesn't
    // exist on disk, so snapshots next to the test file can't be found.
    private static string TestFolder(string testFile)
    {
        var folder = Path.GetDirectoryName(testFile);
        if (folder is null || !System.IO.Directory.Exists(folder))
        {
            throw new PdfSnapshotException($"The test file's folder '{folder}' doesn't exist, which happens when the build maps "
                + "source paths. Set PdfSnapshot.Directory to the folder that holds the snapshots.");
        }

        return folder;
    }

    private static bool AcceptMode() => Environment.GetEnvironmentVariable(AcceptVariable) is "1" or "true" && !OnCi();

    // CI and TF_BUILD are what most build servers (GitHub Actions, Bitbucket, GitLab, Azure DevOps) set.
    private static bool OnCi() =>
        Environment.GetEnvironmentVariable("CI") is { Length: > 0 } ci && ci != "false" && ci != "0"
        || Environment.GetEnvironmentVariable("TF_BUILD") is { Length: > 0 };

    private sealed record SnapshotFiles(string Folder, string Name)
    {
        public string Verified => Path.Combine(Folder, Name + VerifiedSuffix);
        public string Received => Path.Combine(Folder, Name + ReceivedSuffix);
        public string Report => Path.Combine(Folder, Name + ReportSuffix);
    }
}

/// <summary>Thrown when a snapshot check fails: the snapshot differs, or its folder can't be found. The message says why.</summary>
public sealed class PdfSnapshotException(string message) : Exception(message);
