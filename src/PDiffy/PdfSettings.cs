namespace PDiffy;

/// <summary>Document-wide settings that change how a viewer opens, shows or restricts the file.</summary>
public sealed record PdfSettings
{
    /// <summary>The version in the file header.</summary>
    public string? PdfVersion { get; init; }

    /// <summary>The catalog's /Version, which a writer can set to upgrade the file. A viewer uses whichever is later.</summary>
    public string? CatalogVersion { get; init; }

    /// <summary>How the file opens: thumbnails, bookmarks, full screen and so on. One of <see cref="PdfPageModes"/>.</summary>
    public string? PageMode { get; init; }

    /// <summary>How pages are arranged: one at a time, a continuous column, two side by side. One of <see cref="PdfPageLayouts"/>.</summary>
    public string? PageLayout { get; init; }

    /// <summary>
    /// What happens when the file opens, such as "p1 Fit" (go to page 1) or "JavaScript". Actions chained after it are
    /// listed in the order they run: "GoTo p1 Fit, Launch".
    /// </summary>
    public string? OpenAction { get; init; }

    // Each action of OpenAction on its own, since a destination's name can hold the ", " that joins them.
    internal IReadOnlyList<string> OpenActions { get; init; } = [];

    /// <summary>
    /// Whether the document has JavaScript in its document-level scripts, open action, document actions, form fields,
    /// links and other annotations, page actions or bookmarks.
    /// </summary>
    public bool HasJavaScript { get; init; }

    /// <summary>Whether the document runs actions on events such as closing, saving or printing.</summary>
    public bool HasAdditionalActions { get; init; }

    /// <summary>Whether the file is encrypted. <see cref="Permissions"/> says what it allows.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>What an encrypted document allows. Null when the document isn't encrypted.</summary>
    public PdfPermissions? Permissions { get; init; }

    /// <summary>Viewer preferences other than DisplayDocTitle, which <see cref="PdfDocumentInfo"/> holds.</summary>
    public IReadOnlyList<PdfSetting> ViewerPreferences { get; init; } = [];

    /// <summary>The optional-content layers a viewer can show or hide, in order; empty when there are none.</summary>
    public IReadOnlyList<PdfLayer> Layers { get; init; } = [];

    /// <summary>The files attached to the document, in the order its list of attachments has them; empty when none.</summary>
    public IReadOnlyList<PdfEmbeddedFile> EmbeddedFiles { get; init; } = [];
}

/// <summary>The standard values of <see cref="PdfSettings.PageMode"/>.</summary>
public static class PdfPageModes
{
    /// <summary>The file opens with no side panel showing, the default.</summary>
    public const string UseNone = "UseNone";

    /// <summary>The file opens with the bookmarks panel showing.</summary>
    public const string UseOutlines = "UseOutlines";

    /// <summary>The file opens with the page thumbnails panel showing.</summary>
    public const string UseThumbs = "UseThumbs";

    /// <summary>The file opens full screen, with no menu bar, toolbars or window controls.</summary>
    public const string FullScreen = "FullScreen";

    /// <summary>The file opens with the layers panel showing.</summary>
    public const string UseOC = "UseOC";

    /// <summary>The file opens with the attachments panel showing.</summary>
    public const string UseAttachments = "UseAttachments";
}

/// <summary>The standard values of <see cref="PdfSettings.PageLayout"/>.</summary>
public static class PdfPageLayouts
{
    /// <summary>One page at a time.</summary>
    public const string SinglePage = "SinglePage";

    /// <summary>The pages in one continuous column.</summary>
    public const string OneColumn = "OneColumn";

    /// <summary>The pages in two continuous columns, with odd-numbered pages on the left.</summary>
    public const string TwoColumnLeft = "TwoColumnLeft";

    /// <summary>The pages in two continuous columns, with odd-numbered pages on the right.</summary>
    public const string TwoColumnRight = "TwoColumnRight";

    /// <summary>Two pages at a time, with odd-numbered pages on the left.</summary>
    public const string TwoPageLeft = "TwoPageLeft";

    /// <summary>Two pages at a time, with odd-numbered pages on the right.</summary>
    public const string TwoPageRight = "TwoPageRight";
}

/// <summary>
/// A file attached to the document. <see cref="Name"/> is its key in the document's list of attachments; <see cref="Hash"/>
/// is the first 16 hex digits of a SHA-256 of its contents. <see cref="Unreadable"/> is set when the contents are damaged.
/// </summary>
/// <param name="Name">The file's key in the document's list of attachments.</param>
/// <param name="FileName">The file name it was attached under; null when it has none.</param>
/// <param name="Description">The description the viewer shows for it; null when it has none.</param>
/// <param name="Relationship">
/// How the file relates to the document, such as <c>Source</c>, <c>Data</c> or <c>Supplement</c>; null when it doesn't say.
/// </param>
/// <param name="Size">The size of its contents in bytes, once decompressed; null when they're missing or
/// damaged.</param>
/// <param name="Hash">The first 16 hex digits of a SHA-256 of its contents; null when they're missing or
/// damaged.</param>
public sealed record PdfEmbeddedFile(string Name, string? FileName, string? Description, string? Relationship, int? Size, string? Hash)
{
    /// <summary>
    /// Whether the contents are damaged, which leaves <see cref="Size"/> and <see cref="Hash"/> null.
    /// </summary>
    public bool Unreadable { get; init; }
}

/// <summary>What an encrypted document lets a reader do.</summary>
[Flags]
public enum PdfPermissions
{
    /// <summary>None of the actions below.</summary>
    None = 0,

    /// <summary>Print the document; at low quality only, unless <see cref="PrintHighQuality"/> is also set.</summary>
    Print = 1 << 0,

    /// <summary>
    /// Change the document in ways that <see cref="Annotate"/>, <see cref="FillForms"/> and <see cref="Assemble"/>
    /// don't cover.
    /// </summary>
    Modify = 1 << 1,

    /// <summary>Copy or extract text and graphics.</summary>
    Copy = 1 << 2,

    /// <summary>
    /// Add and change annotations and fill in form fields; with <see cref="Modify"/>, also add form fields.
    /// </summary>
    Annotate = 1 << 3,

    /// <summary>Fill in form fields and sign the document, even when <see cref="Annotate"/> isn't set.</summary>
    FillForms = 1 << 4,

    /// <summary>
    /// Extract text and graphics for a screen reader or similar tool, even without <see cref="Copy"/>.
    /// </summary>
    ExtractForAccessibility = 1 << 5,

    /// <summary>
    /// Insert, rotate and delete pages and add bookmarks and thumbnails, even without <see cref="Modify"/>.
    /// </summary>
    Assemble = 1 << 6,

    /// <summary>Print at full quality; without it, <see cref="Print"/> allows only a low-quality print.</summary>
    PrintHighQuality = 1 << 7,
}

/// <summary>A named value, as the PDF stores it.</summary>
/// <param name="Name">The entry's name, such as <c>HideToolbar</c> for a viewer preference.</param>
/// <param name="Value">The entry's value as text, such as <c>true</c>.</param>
public sealed record PdfSetting(string Name, string Value);

/// <summary>An optional-content layer and whether a viewer shows it when the file opens.</summary>
/// <param name="Name">The layer's name as the viewer's layers panel shows it, or <c>(unnamed)</c>.</param>
/// <param name="VisibleByDefault">Whether the viewer shows the layer when the file opens.</param>
public sealed record PdfLayer(string Name, bool VisibleByDefault);
