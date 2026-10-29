namespace PDiffy;

/// <summary>
/// A ready-made set of sections and fields to leave out of a PDF snapshot, named after what a test guards. Apply one with
/// <see cref="SnapshotOptions.Use(SnapshotProfile)"/>. The snapshot's header lists what it leaves out, so a profile that
/// leaves out more in a later release shows up as a changed snapshot rather than a test that quietly checks less.
/// </summary>
public enum SnapshotProfile
{
    /// <summary>Compares everything, the same as using no profile.</summary>
    Strict,

    /// <summary>
    /// The text and its structure: tags, reading order, bookmarks, annotations and forms. Leaves out the settings,
    /// metadata, fonts and layout sections, page boxes, and where annotations are on the page.
    /// </summary>
    Content,

    /// <summary>
    /// What assistive technology uses: tags, alt text, language, reading order, untagged content, artifacts, annotations,
    /// forms and whether fonts map to Unicode. Leaves out how content looks (positions, colors, font names and sizes, line
    /// widths, image contents and page boxes), settings that don't change what's read out (PDF version, open action and
    /// attachments' contents) and metadata other than the title (author, subject, keywords, dates, producer, creator and
    /// custom Info entries). Permissions stay, since they can stop a screen reader from reading the text.
    /// </summary>
    Accessibility,

    /// <summary>
    /// Everything on the page, including where it's drawn and how it looks. Leaves out the settings and metadata sections.
    /// </summary>
    Layout,
}
