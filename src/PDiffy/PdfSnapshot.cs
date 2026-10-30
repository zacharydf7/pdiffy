using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PDiffy;

/// <summary>Replaced by <c>Snapshot.VerifyPdfAsync</c> and <c>Snapshot.VerifyPdf</c>; kept for one release.</summary>
[Obsolete("Use Snapshot.VerifyPdfAsync, Snapshot.VerifyPdf and Snapshot.DefaultPdfOptions.")]
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PdfSnapshot
{
    /// <inheritdoc cref="Snapshot.DefaultPdfOptions"/>
    public static Action<PdfSnapshotOptions>? DefaultOptions
    {
        get => Snapshot.DefaultPdfOptions;
        set => Snapshot.DefaultPdfOptions = value;
    }

    /// <inheritdoc cref="Snapshot.VerifyPdfAsync(Stream, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"/>
    public static Task VerifyAsync(Stream pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyPdfAsync(pdf, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyPdfAsync(byte[], string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"/>
    public static Task VerifyAsync(byte[] pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyPdfAsync(pdf, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyPdfAsync(PdfUaDocument, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"/>
    public static Task VerifyAsync(PdfUaDocument pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyPdfAsync(pdf, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyPdf(Stream, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"/>
    public static void Verify(Stream pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyPdf(pdf, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyPdf(byte[], string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"/>
    public static void Verify(byte[] pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyPdf(pdf, name, configure, directory, testFile, testName, variant);

    /// <inheritdoc cref="Snapshot.VerifyPdf(PdfUaDocument, string?, Action{PdfSnapshotOptions}?, string?, string, string, string?)"/>
    public static void Verify(PdfUaDocument pdf, string? name = null, Action<PdfSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "",
        string? variant = null) =>
        Snapshot.VerifyPdf(pdf, name, configure, directory, testFile, testName, variant);
}
