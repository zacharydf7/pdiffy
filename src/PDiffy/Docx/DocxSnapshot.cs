using System.Runtime.CompilerServices;

namespace PDiffy;

/// <summary>
/// Snapshot tests for Word documents. <c>VerifyAsync</c> compares a DOCX's snapshot with the approved one in
/// <c>{name}.verified.docx.txt</c> and throws <see cref="SnapshotException"/> when they differ, so it works with any
/// test framework.
/// </summary>
public static class DocxSnapshot
{
    internal const string Extension = "docx.txt";

    /// <summary>
    /// Options every DOCX snapshot starts with, such as a profile or scrubbers, set once (in a module initializer, say)
    /// instead of in each test. Each call's own <c>configure</c> runs after them and adds to them: it can leave out
    /// more, but not bring back what these leave out, so keep them to what every test agrees on. The snapshot's first
    /// lines list what's ignored, but not what's scrubbed.
    /// </summary>
    public static Action<DocxSnapshotOptions>? DefaultOptions { get; set; }

    /// <summary>
    /// Compares the DOCX's snapshot with the verified one. On a mismatch it writes <c>{name}.received.docx.txt</c> and an
    /// HTML report, <c>{name}.diff.html</c>, and throws; when they match it deletes both if an earlier run left them.
    /// </summary>
    /// <param name="docx">The DOCX to check. The stream is left open.</param>
    /// <param name="name">The snapshot's file name without extension. Defaults to the test file and method.</param>
    /// <param name="configure">What to leave out or scrub, as for <see cref="DocxDocument.ToSnapshot"/>.</param>
    /// <param name="directory">The folder for this snapshot, instead of <see cref="Snapshot.Directory"/>.</param>
    /// <param name="testFile">Filled in by the compiler with the calling test's file. Leave it out, or pass on your own
    /// [CallerFilePath] parameter from a helper.</param>
    /// <param name="testName">Filled in by the compiler with the calling test's method. Leave it out, or pass on your own
    /// [CallerMemberName] parameter from a helper.</param>
    /// <param name="variant">Added to the name, default or not, as <c>{name}.{variant}</c>, so each row of a theory gets
    /// its own snapshot and keeps the test's name: <c>variant: trail</c>.</param>
    // The work is done before returning, so a failure throws at the call even when the caller forgets await.
    public static Task VerifyAsync(Stream docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        Verify(docx, name, configure, directory, testFile, testName, variant);
        return Task.CompletedTask;
    }

    /// <summary>Compares the snapshot of a DOCX in a byte array with the verified one, as for a stream.</summary>
    /// <param name="docx">The DOCX to check, as bytes.</param>
    /// <inheritdoc cref="VerifyAsync(Stream, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param[@name!='docx']"/>
    public static Task VerifyAsync(byte[] docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        Verify(docx, name, configure, directory, testFile, testName, variant);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Compares the snapshot of a DOCX already loaded with the verified one, as for a stream, so a test that also asserts
    /// on the document reads it once.
    /// </summary>
    /// <param name="docx">The DOCX to check, already loaded with <see cref="DocxDocument.Load(Stream)"/>.</param>
    /// <inheritdoc cref="VerifyAsync(Stream, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param[@name!='docx']"/>
    public static Task VerifyAsync(DocxDocument docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        Verify(docx, name, configure, directory, testFile, testName, variant);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The synchronous version of <c>VerifyAsync</c>, for a test that isn't async: on a mismatch it writes the received
    /// file and the report, and throws.
    /// </summary>
    /// <inheritdoc cref="VerifyAsync(Stream, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param"/>
    public static void Verify(Stream docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null) =>
        Verify(DocxDocument.Load(docx), name, configure, directory, testFile, testName, variant);

    /// <summary>
    /// The synchronous version of <c>VerifyAsync</c>, for a test that isn't async: on a mismatch it writes the received
    /// file and the report, and throws.
    /// </summary>
    /// <inheritdoc cref="VerifyAsync(byte[], string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param"/>
    public static void Verify(byte[] docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null) =>
        Verify(DocxDocument.Load(docx), name, configure, directory, testFile, testName, variant);

    /// <summary>
    /// The synchronous version of <c>VerifyAsync</c>, for a test that isn't async: on a mismatch it writes the received
    /// file and the report, and throws.
    /// </summary>
    /// <inheritdoc cref="VerifyAsync(DocxDocument, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"
    ///     path="/param"/>
    public static void Verify(DocxDocument docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "", string? variant = null)
    {
        var received = docx.ToSnapshot(options =>
        {
            DefaultOptions?.Invoke(options);
            configure?.Invoke(options);
        });
        var files = SnapshotWorkflow.Files(name, variant, Extension, directory, testFile, testName);
        SnapshotWorkflow.Verify(received, files, "DOCX");
    }
}
