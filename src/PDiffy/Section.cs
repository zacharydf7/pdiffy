namespace PDiffy;

/// <summary>The sections of a snapshot, in the order it prints them.</summary>
public enum Section
{
    /// <summary>Whether the PDF is tagged, its language, whether the viewer shows the title, and the page count.</summary>
    Document,

    /// <summary>PDF version, page mode and layout, open action, JavaScript, encryption, viewer preferences, layers and attachments.</summary>
    Settings,

    /// <summary>Title, author, subject, keywords, dates, producer, creator, XMP details and other Info entries.</summary>
    Metadata,

    /// <summary>Custom structure types and the standard types they stand for.</summary>
    RoleMap,

    /// <summary>Page sizes, rotation, tab order, page boxes and page labels.</summary>
    Pages,

    /// <summary>The bookmark tree and the page each bookmark goes to.</summary>
    Bookmarks,

    /// <summary>The tag tree: every structure element with its attributes and text.</summary>
    Structure,

    /// <summary>Text marked as decoration, which a screen reader skips.</summary>
    Artifacts,

    /// <summary>Text and graphics that are neither tagged nor marked as decoration.</summary>
    Untagged,

    /// <summary>Each font, whether it's embedded, and the character codes its text uses that don't map to Unicode.</summary>
    Fonts,

    /// <summary>Links, comments, form widgets and other annotations.</summary>
    Annotations,

    /// <summary>Form fields.</summary>
    Forms,

    /// <summary>Every text run, image and path with its position, font, size and color.</summary>
    Layout,
}
