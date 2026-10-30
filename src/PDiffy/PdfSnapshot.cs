using System.Runtime.CompilerServices;

namespace PDiffy;

/// <summary>
/// Snapshot tests for PDFs. <c>VerifyAsync</c> compares a PDF's snapshot with the approved one in
/// <c>{name}.verified.pdfua.txt</c> and throws <see cref="SnapshotException"/> when they differ, so it works with any
/// test framework.
/// </summary>
public static class PdfSnapshot
{
    internal const string Extension = "pdfua.txt";

    /// <summary>
    /// Options every PDF snapshot starts with, such as a profile or scrubbers, set once (in a module initializer, say)
    /// instead of in each test. Each call's own <c>configure</c> runs after them and adds to them: it can leave out
    /// more, but not bring back what these leave out, so keep them to what every test agrees on. The snapshot's first
    /// lines list what's ignored, but not what's scrubbed.
    /// </summary>
    public static Action<SnapshotOptions>? DefaultOptions { get; set; }

    /// <summary>
    /// Compares the PDF's snapshot with the verified one. On a mismatch it writes <c>{name}.received.pdfua.txt</c> and an
    /// HTML report, <c>{name}.diff.html</c>, and throws; when they match it deletes both if an earlier run left them.
    /// </summary>
    /// <param name="pdf">The PDF to check. The stream is left open.</param>
    /// <param name="name">The snapshot's file name without extension. Defaults to the test file and method, such as
    /// "TrailMapTests.TrailMap_ShouldMatch". Pass one when a test makes several snapshots, as a theory does.</param>
    /// <param name="configure">What to leave out or scrub, as for <see cref="PdfUaDocument.ToSnapshot"/>.</param>
    /// <param name="directory">The folder for this snapshot, instead of <see cref="Snapshot.Directory"/>.</param>
    /// <param name="testFile">Filled in by the compiler with the calling test's file. Leave it out, or pass on your own
    /// [CallerFilePath] parameter from a helper.</param>
    /// <param name="testName">Filled in by the compiler with the calling test's method. Leave it out, or pass on your own
    /// [CallerMemberName] parameter from a helper.</param>
    /// <param name="variant">Added to the name, default or not, as <c>{name}.{variant}</c>, so each row of a theory gets
    /// its own snapshot and keeps the test's name: <c>variant: trail</c>.</param>
    public static async Task VerifyAsync(Stream pdf, string? name = null, Action<SnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null) =>
        await VerifyAsync(PdfUaDocument.Load(pdf), name, configure, directory, testFile, testName, variant);

    /// <summary>Compares the snapshot of a PDF in a byte array with the verified one, as for a stream.</summary>
    /// <param name="pdf">The PDF to check, as bytes.</param>
    /// <inheritdoc cref="VerifyAsync(Stream, string?, Action{SnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param[@name!='pdf']"/>
    public static async Task VerifyAsync(byte[] pdf, string? name = null, Action<SnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null) =>
        await VerifyAsync(PdfUaDocument.Load(pdf), name, configure, directory, testFile, testName, variant);

    /// <summary>
    /// Compares the snapshot of a PDF already loaded with the verified one, as for a stream, so a test that also asserts
    /// on the document reads it once.
    /// </summary>
    /// <param name="pdf">The PDF to check, already loaded with <see cref="PdfUaDocument.Load(Stream)"/>.</param>
    /// <inheritdoc cref="VerifyAsync(Stream, string?, Action{SnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param[@name!='pdf']"/>
    public static async Task VerifyAsync(PdfUaDocument pdf, string? name = null, Action<SnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        var received = pdf.ToSnapshot(options =>
        {
            DefaultOptions?.Invoke(options);
            configure?.Invoke(options);
        });
        var files = SnapshotWorkflow.Files(name, variant, Extension, directory, testFile, testName);
        await SnapshotWorkflow.VerifyAsync(received, files, "PDF");
    }
}

/// <summary>
/// Thrown when a snapshot check fails: the snapshot differs, its folder can't be found, or JSON can't be read. The
/// message says why.
/// </summary>
public sealed class SnapshotException(string message) : Exception(message);
