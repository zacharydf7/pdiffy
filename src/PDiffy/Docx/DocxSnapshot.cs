using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PDiffy;

/// <summary>Replaced by <c>Snapshot.VerifyDocxAsync</c> and <c>Snapshot.VerifyDocx</c>; kept for one release.</summary>
[Obsolete("Use Snapshot.VerifyDocxAsync, Snapshot.VerifyDocx and Snapshot.DefaultDocxOptions.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DocxSnapshot
{
    /// <inheritdoc cref="Snapshot.DefaultDocxOptions"/>
    public static Action<DocxSnapshotOptions>? DefaultOptions
    {
        get => Snapshot.DefaultDocxOptions;
        set => Snapshot.DefaultDocxOptions = value;
    }

    /// <inheritdoc cref="Snapshot.VerifyDocxAsync(Stream, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"/>
    public static Task VerifyAsync(Stream docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyDocxAsync(docx, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyDocxAsync(byte[], string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"/>
    public static Task VerifyAsync(byte[] docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyDocxAsync(docx, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyDocxAsync(DocxDocument, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"/>
    public static Task VerifyAsync(DocxDocument docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyDocxAsync(docx, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyDocx(Stream, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"/>
    public static void Verify(Stream docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyDocx(docx, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyDocx(byte[], string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"/>
    public static void Verify(byte[] docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyDocx(docx, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyDocx(DocxDocument, string?, Action{DocxSnapshotOptions}?, string?, string, string, string?)"/>
    public static void Verify(DocxDocument docx, string? name = null, Action<DocxSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyDocx(docx, name, configure, directory, testFile, testName, variant);
}
