using System.Runtime.CompilerServices;

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

    internal const string Extension = "pdfua.txt";
    internal const string VerifiedSuffix = SnapshotFiles.VerifiedPart + Extension;
    internal const string ReceivedSuffix = SnapshotFiles.ReceivedPart + Extension;

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
        var files = new SnapshotFiles(Directory ?? SnapshotWorkflow.TestFolder(testFile),
            name ?? SnapshotWorkflow.TestName(testFile, testName), Extension);
        await SnapshotWorkflow.VerifyAsync(received, files, "PDF");
    }
}

/// <summary>Thrown when a snapshot check fails: the snapshot differs, or its folder can't be found. The message says why.</summary>
public sealed class PdfSnapshotException(string message) : Exception(message);
