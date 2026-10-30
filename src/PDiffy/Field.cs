namespace PDiffy;

/// <summary>A detail that <see cref="SnapshotOptions.Ignore(Field[])"/> leaves out of the snapshot wherever it appears.</summary>
public enum Field
{
    /// <summary>
    /// The document's language, each structure element's language, languages set on marked content inside elements, and
    /// the XMP language.
    /// </summary>
    Language,

    /// <summary>The PDF version in the file header and the catalog.</summary>
    PdfVersion,

    /// <summary>What happens when the file opens.</summary>
    OpenAction,

    /// <summary>What an encrypted document allows.</summary>
    Permissions,

    /// <summary>The size and hash of each embedded file, and whether it could be read.</summary>
    EmbeddedFileContents,

    /// <summary>The document title, from the Info dictionary and XMP.</summary>
    Title,

    /// <summary>The author, from the Info dictionary and the XMP creator.</summary>
    Author,

    /// <summary>The subject, from the Info dictionary and the XMP description.</summary>
    Subject,

    /// <summary>The keywords, from the Info dictionary.</summary>
    Keywords,

    /// <summary>Whether the creation and modification dates are present.</summary>
    Dates,

    /// <summary>Whether the producer (the library that wrote the PDF) is present.</summary>
    Producer,

    /// <summary>Whether the creator (the application the content came from) is present.</summary>
    Creator,

    /// <summary>Info entries other than the standard ones, such as a company name or classification label.</summary>
    CustomInfo,

    /// <summary>The crop, bleed, trim and art boxes and the user unit of each page.</summary>
    PageBoxes,

    /// <summary>Alternate text on structure elements and marked content inside them, such as a figure's description.</summary>
    AltText,

    /// <summary>Replacement text on structure elements and marked content inside them.</summary>
    ActualText,

    /// <summary>
    /// Where text, images, paths and annotations are on the page, the size of images, paths and annotations, and the
    /// positions a link or bookmark opens its page at.
    /// </summary>
    Positions,

    /// <summary>Text color, path fill and stroke colors, and the color of text typed into form fields.</summary>
    Colors,

    /// <summary>The font of each text run in the layout section, and of text typed into form fields.</summary>
    FontNames,

    /// <summary>The font size of each text run in the layout section, and of text typed into form fields.</summary>
    FontSizes,

    /// <summary>The stroke width of each path.</summary>
    LineWidths,

    /// <summary>The pixel size and hash of each image, and the hashes of the images each structure element draws.</summary>
    ImageContents,

    /// <summary>
    /// Where links, bookmarks and other actions go: web addresses, files, action types, destinations and the view a page
    /// opens at. A bookmark's page stays.
    /// </summary>
    LinkTargets,

    /// <summary>The text of annotations, such as a comment or a link's description.</summary>
    AnnotationContents,

    /// <summary>The value and default value of each form field.</summary>
    FormValues,
}
