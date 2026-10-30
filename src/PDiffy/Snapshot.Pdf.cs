using System.Runtime.CompilerServices;

namespace PDiffy;

public static partial class Snapshot
{
    internal const string PdfExtension = "pdfua.txt";

    /// <summary>
    /// Options every PDF snapshot starts with, such as a profile or scrubbers, set once (in a module initializer, say)
    /// instead of in each test. Each call's own <c>configure</c> runs after them and adds to them: it can leave out
    /// more, but not bring back what these leave out, so keep them to what every test agrees on. The snapshot's first
    /// lines list what's ignored, but not what's scrubbed.
    /// </summary>
    public static Action<PdfSnapshotOptions>? DefaultPdfOptions { get; set; }

    /// <summary>
    /// Compares the PDF's snapshot with the verified one in <c>{name}.verified.pdfua.txt</c>. On a mismatch it writes
    /// <c>{name}.received.pdfua.txt</c> and an HTML report, <c>{name}.diff.html</c>, and throws
    /// <see cref="SnapshotException"/>; when they match it deletes both if an earlier run left them.
    /// </summary>
    /// <param name="pdf">The PDF to check. The stream is left open.</param>
    /// <param name="name">The snapshot's file name without extension. Defaults to the test file and method, such as
    /// "TrailMapTests.TrailMap_ShouldMatch". Pass one when a test makes several snapshots, as a theory does.</param>
    /// <param name="configure">What to leave out or scrub, as for <see cref="PdfUaDocument.ToSnapshot"/>.</param>
    /// <param name="directory">The folder for this snapshot, instead of <see cref="Directory"/>.</param>
    /// <param name="testFile">Filled in by the compiler with the calling test's file. Leave it out, or pass on your own
    /// [CallerFilePath] parameter from a helper.</param>
    /// <param name="testName">Filled in by the compiler with the calling test's method. Leave it out, or pass on your own
    /// [CallerMemberName] parameter from a helper.</param>
    /// <param name="variant">Added to the name, default or not, as <c>{name}.{variant}</c>, so each row of a theory gets
    /// its own snapshot and keeps the test's name: <c>variant: trail</c>.</param>
    // The work is done before returning, so a failure throws at the call even when the caller forgets await.
    public static Task VerifyPdfAsync(Stream pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        VerifyPdf(pdf, name, configure, directory, testFile, testName, variant);
        return Task.CompletedTask;
    }

    /// <summary>Compares the snapshot of a PDF in a byte array with the verified one, as for a stream.</summary>
    /// <param name="pdf">The PDF to check, as bytes.</param>
    /// <inheritdoc cref="VerifyPdfAsync(Stream, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param[@name!='pdf']"/>
    public static Task VerifyPdfAsync(byte[] pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        VerifyPdf(pdf, name, configure, directory, testFile, testName, variant);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Compares the snapshot of a PDF already loaded with the verified one, as for a stream, so a test that also asserts
    /// on the document reads it once.
    /// </summary>
    /// <param name="pdf">The PDF to check, already loaded with <see cref="PdfUaDocument.Load(Stream)"/>.</param>
    /// <inheritdoc cref="VerifyPdfAsync(Stream, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param[@name!='pdf']"/>
    public static Task VerifyPdfAsync(PdfUaDocument pdf, string? name = null,
        Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        VerifyPdf(pdf, name, configure, directory, testFile, testName, variant);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The synchronous version of <c>VerifyPdfAsync</c>, for a test that isn't async: on a mismatch it writes the
    /// received file and the report, and throws.
    /// </summary>
    /// <inheritdoc cref="VerifyPdfAsync(Stream, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param"/>
    public static void VerifyPdf(Stream pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null) =>
        VerifyPdf(PdfUaDocument.Load(pdf), name, configure, directory, testFile, testName, variant);

    /// <summary>
    /// The synchronous version of <c>VerifyPdfAsync</c>, for a test that isn't async: on a mismatch it writes the
    /// received file and the report, and throws.
    /// </summary>
    /// <inheritdoc cref="VerifyPdfAsync(byte[], string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param"/>
    public static void VerifyPdf(byte[] pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null) =>
        VerifyPdf(PdfUaDocument.Load(pdf), name, configure, directory, testFile, testName, variant);

    /// <summary>
    /// The synchronous version of <c>VerifyPdfAsync</c>, for a test that isn't async: on a mismatch it writes the
    /// received file and the report, and throws.
    /// </summary>
    /// <inheritdoc cref="VerifyPdfAsync(PdfUaDocument, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param"/>
    public static void VerifyPdf(PdfUaDocument pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        var received = pdf.ToSnapshot(options =>
        {
            DefaultPdfOptions?.Invoke(options);
            configure?.Invoke(options);
        });
        var files = SnapshotWorkflow.Files(name, variant, PdfExtension, directory, testFile, testName);
        SnapshotWorkflow.Verify(received, files, "PDF");
    }
}
