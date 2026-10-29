namespace PDiffy;

/// <summary>Document-wide settings that change how a viewer opens, shows or restricts the file.</summary>
public sealed record DocumentSettings
{
    /// <summary>The version in the file header.</summary>
    public string? PdfVersion { get; init; }

    /// <summary>The catalog's /Version, which a writer can set to upgrade the file. A viewer uses whichever is later.</summary>
    public string? CatalogVersion { get; init; }

    /// <summary>How the file opens: thumbnails, bookmarks, full screen and so on. One of <see cref="PageModes"/>.</summary>
    public string? PageMode { get; init; }

    /// <summary>How pages are arranged: one at a time, a continuous column, two side by side. One of <see cref="PageLayouts"/>.</summary>
    public string? PageLayout { get; init; }

    /// <summary>
    /// What happens when the file opens, such as "p1 Fit" (go to page 1) or "JavaScript". Actions chained after it are
    /// listed in the order they run: "GoTo p1 Fit, Launch".
    /// </summary>
    public string? OpenAction { get; init; }

    // Each action of OpenAction on its own, since a destination's name can hold the ", " that joins them.
    internal IReadOnlyList<string> OpenActions { get; init; } = [];

    public bool HasJavaScript { get; init; }

    /// <summary>Whether the document runs actions on events such as closing, saving or printing.</summary>
    public bool HasAdditionalActions { get; init; }

    public bool IsEncrypted { get; init; }

    /// <summary>What an encrypted document allows. Null when the document isn't encrypted.</summary>
    public DocumentPermissions? Permissions { get; init; }

    /// <summary>Viewer preferences other than DisplayDocTitle, which <see cref="DocumentInfo"/> holds.</summary>
    public IReadOnlyList<Setting> ViewerPreferences { get; init; } = [];

    public IReadOnlyList<Layer> Layers { get; init; } = [];
    public IReadOnlyList<EmbeddedFile> EmbeddedFiles { get; init; } = [];
}

/// <summary>The standard values of <see cref="DocumentSettings.PageMode"/>.</summary>
public static class PageModes
{
    public const string UseNone = "UseNone";
    public const string UseOutlines = "UseOutlines";
    public const string UseThumbs = "UseThumbs";
    public const string FullScreen = "FullScreen";
    public const string UseOC = "UseOC";
    public const string UseAttachments = "UseAttachments";
}

/// <summary>The standard values of <see cref="DocumentSettings.PageLayout"/>.</summary>
public static class PageLayouts
{
    public const string SinglePage = "SinglePage";
    public const string OneColumn = "OneColumn";
    public const string TwoColumnLeft = "TwoColumnLeft";
    public const string TwoColumnRight = "TwoColumnRight";
    public const string TwoPageLeft = "TwoPageLeft";
    public const string TwoPageRight = "TwoPageRight";
}

/// <summary>
/// A file attached to the document. <see cref="Name"/> is its key in the document's list of attachments; <see cref="Hash"/>
/// is the first 16 hex digits of a SHA-256 of its contents. <see cref="Unreadable"/> is set when the contents are damaged.
/// </summary>
public sealed record EmbeddedFile(string Name, string? FileName, string? Description, string? Relationship, int? Size, string? Hash)
{
    public bool Unreadable { get; init; }
}

/// <summary>What an encrypted document lets a reader do.</summary>
[Flags]
public enum DocumentPermissions
{
    None = 0,
    Print = 1 << 0,
    Modify = 1 << 1,
    Copy = 1 << 2,
    Annotate = 1 << 3,
    FillForms = 1 << 4,
    ExtractForAccessibility = 1 << 5,
    Assemble = 1 << 6,
    PrintHighQuality = 1 << 7,
}

/// <summary>A named value, as the PDF stores it.</summary>
public sealed record Setting(string Name, string Value);

/// <summary>An optional-content layer and whether a viewer shows it when the file opens.</summary>
public sealed record Layer(string Name, bool VisibleByDefault);
