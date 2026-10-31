namespace PDiffy;

/// <summary>Document-wide settings that change how a viewer opens, shows or restricts the file.</summary>
public sealed record PdfSettings
{
    internal PdfSettings()
    {
    }

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
    internal IReadOnlyList<string> OpenActions
    {
        get;
        init => field = ValueList<string>.Of(value);
    } = ValueList<string>.Empty;

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
    public IReadOnlyList<PdfSetting> ViewerPreferences
    {
        get;
        init => field = ValueList<PdfSetting>.Of(value);
    } = ValueList<PdfSetting>.Empty;

    /// <summary>The optional-content layers a viewer can show or hide, in order; empty when there are none.</summary>
    public IReadOnlyList<PdfLayer> Layers
    {
        get;
        init => field = ValueList<PdfLayer>.Of(value);
    } = ValueList<PdfLayer>.Empty;

    /// <summary>
    /// The files embedded in the document: those in its list of attachments, in their order, then those attached through
    /// comments (FileAttachment annotations), page by page; empty when none.
    /// </summary>
    public IReadOnlyList<PdfEmbeddedFile> EmbeddedFiles
    {
        get;
        init => field = ValueList<PdfEmbeddedFile>.Of(value);
    } = ValueList<PdfEmbeddedFile>.Empty;

    /// <summary>
    /// The optional-content configurations: the default one, then the others a viewer can switch to; empty when the
    /// document has no layers.
    /// </summary>
    public IReadOnlyList<PdfLayerConfiguration> LayerConfigurations
    {
        get;
        init => field = ValueList<PdfLayerConfiguration>.Of(value);
    } = ValueList<PdfLayerConfiguration>.Empty;

    /// <summary>
    /// How many reference XObjects (form XObjects that show a page of another file, /Ref) are in the pages' resources,
    /// their annotations' appearances and the resources of the forms in them, whether or not the pages draw them.
    /// </summary>
    public int ReferenceXObjects { get; init; }
}

/// <summary>An optional-content configuration: which layers a viewer shows, under a name it lists.</summary>
public sealed record PdfLayerConfiguration
{
    internal PdfLayerConfiguration()
    {
    }

    /// <summary>The name a viewer lists it under; null when it has none.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Whether it turns layers on or off by itself, such as by zoom or for printing (an /AS entry), which PDF/UA
    /// doesn't allow.
    /// </summary>
    public bool ChangesLayersByItself { get; init; }
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
/// A file attached to the document, in its list of attachments or through a FileAttachment annotation on a page.
/// <see cref="Hash"/> is the first 16 hex digits of a SHA-256 of its contents. <see cref="Unreadable"/> is set when the
/// contents are damaged.
/// </summary>
public sealed record PdfEmbeddedFile
{
    internal PdfEmbeddedFile()
    {
    }

    /// <summary>
    /// The file's key in the document's list of attachments, or for a file attached on a page, its file name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The page of the FileAttachment annotation that holds it; null for a file in the document's list.
    /// </summary>
    public int? Page { get; init; }

    /// <summary>The file name it was attached under; null when it has none.</summary>
    public required string? FileName { get; init; }

    /// <summary>The description the viewer shows for it; null when it has none.</summary>
    public required string? Description { get; init; }

    /// <summary>
    /// How the file relates to the document, such as <c>Source</c>, <c>Data</c> or <c>Supplement</c>; null when it
    /// doesn't say. <see cref="PdfFileRelationships"/> lists them.
    /// </summary>
    public required string? Relationship { get; init; }

    /// <summary>The size of its contents in bytes, once decompressed; null when they're missing or damaged.</summary>
    public required int? Size { get; init; }

    /// <summary>The first 16 hex digits of a SHA-256 of its contents; null when they're missing or damaged.</summary>
    public required string? Hash { get; init; }

    /// <summary>
    /// Whether the contents are damaged, which leaves <see cref="Size"/> and <see cref="Hash"/> null.
    /// </summary>
    public bool Unreadable { get; init; }

    /// <summary>
    /// Whether its file specification gives the file name both ways PDF/UA asks: as /F and as /UF (Unicode).
    /// Snapshots print <c>both-names=no</c> when it doesn't.
    /// </summary>
    public bool NamedBothWays { get; init; }
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
public sealed record PdfSetting
{
    internal PdfSetting()
    {
    }

    /// <summary>The entry's name, such as <c>HideToolbar</c> for a viewer preference.</summary>
    public required string Name { get; init; }

    /// <summary>The entry's value as text, such as <c>true</c>.</summary>
    public required string Value { get; init; }
}

/// <summary>An optional-content layer and whether a viewer shows it when the file opens.</summary>
public sealed record PdfLayer
{
    internal PdfLayer()
    {
    }

    /// <summary>The layer's name as the viewer's layers panel shows it, or <c>(unnamed)</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Whether the viewer shows the layer when the file opens.</summary>
    public required bool VisibleByDefault { get; init; }
}
