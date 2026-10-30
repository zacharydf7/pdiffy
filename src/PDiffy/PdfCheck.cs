namespace PDiffy;

/// <summary>
/// A rule <see cref="PdfUaDocument.Check(PdfCheck[])"/> can apply: the mistakes generated documents make that keep them
/// from being accessible or tidy. These are fast regression checks, not a PDF/UA validator such as veraPDF or PAC.
/// </summary>
public enum PdfCheck
{
    /// <summary>The document declares a language, so a screen reader reads it with the right voice.</summary>
    DocumentLanguage,

    /// <summary>The document has a title, and the viewer shows it instead of the file name.</summary>
    DocumentTitle,

    /// <summary>Every figure has alt text or replacement text.</summary>
    FigureAltText,

    /// <summary>
    /// All text, images and paths are tagged or marked as artifacts, so none is hidden from a screen reader.
    /// </summary>
    UntaggedContent,

    /// <summary>
    /// Every link has a description: its contents, or the alt text or text of the element that tags it.
    /// </summary>
    LinkDescriptions,

    /// <summary>Every form field has an accessible name (its tooltip, TU).</summary>
    FormFieldNames,

    /// <summary>
    /// Every table header has a scope or is named by the cells under it, and no cell names a header that doesn't exist.
    /// </summary>
    TableHeaders,

    /// <summary>Heading levels don't skip: the first heading is H1, and no H3 comes straight after an H1.</summary>
    HeadingLevels,

    /// <summary>Every font is embedded, so the text looks the same everywhere.</summary>
    FontsEmbedded,

    /// <summary>
    /// Every character code a font's text uses maps to Unicode, so the text can be copied, searched and read aloud.
    /// </summary>
    FontsMapToUnicode,

    /// <summary>
    /// The document has no JavaScript, runs no actions on events such as closing or printing, and does nothing when it
    /// opens but go to a page.
    /// </summary>
    NoActions,

    /// <summary>The document has no embedded files.</summary>
    NoEmbeddedFiles,

    /// <summary>
    /// Every custom structure type maps to a standard one, no mapping loops back on itself, and no standard type is
    /// remapped, so assistive technology knows what each element is.
    /// </summary>
    RoleMap,
}

/// <summary>
/// The family a <see cref="PdfCheck"/> belongs to, such as tables or fonts, so findings can be grouped by family;
/// <see cref="HouseRules"/> holds the policies that aren't accessibility rules.
/// </summary>
public enum PdfCheckCategory
{
    /// <summary>Settings of the whole document, such as its title.</summary>
    Document,

    /// <summary>The language text is read in.</summary>
    Language,

    /// <summary>Alt text for figures.</summary>
    AltText,

    /// <summary>Content that's neither tagged nor marked as an artifact.</summary>
    Content,

    /// <summary>The structure tree and its types, such as the role map.</summary>
    Structure,

    /// <summary>Headings and their levels.</summary>
    Headings,

    /// <summary>Tables and their headers.</summary>
    Tables,

    /// <summary>Links and other annotations.</summary>
    Annotations,

    /// <summary>Form fields.</summary>
    Forms,

    /// <summary>Fonts and the text they can give a screen reader.</summary>
    Fonts,

    /// <summary>Sensible policies that aren't accessibility rules, such as no JavaScript.</summary>
    HouseRules,
}

/// <summary>How much a <see cref="PdfCheckFinding"/> matters.</summary>
public enum PdfCheckSeverity
{
    /// <summary>The document breaks a rule PDF/UA requires (a "shall").</summary>
    Error,

    /// <summary>
    /// The document breaks a rule PDF/UA recommends (a "should"), or a house rule that isn't from a standard.
    /// </summary>
    Warning,
}

/// <summary>
/// Something a <see cref="PdfCheck"/> found: where it is, its page when it has one, and what's wrong. The path names the
/// snapshot section and the item: for a structure element, the path a snapshot diff shows, such as
/// "structure > Document > Figure[2]".
/// </summary>
public sealed record PdfCheckFinding
{
    internal PdfCheckFinding()
    {
    }

    /// <summary>The rule that found it.</summary>
    public required PdfCheck Check { get; init; }

    /// <summary>The family the rule belongs to, such as tables or fonts.</summary>
    public required PdfCheckCategory Category { get; init; }

    /// <summary>How much it matters: an error for a rule PDF/UA requires, a warning otherwise.</summary>
    public required PdfCheckSeverity Severity { get; init; }

    /// <summary>
    /// The clause the rule comes from, such as "ISO 14289-1 7.3" (PDF/UA-1), or null for a house rule. It says where
    /// the rule comes from; it doesn't say the document conforms to the standard.
    /// </summary>
    public required string? Reference { get; init; }

    /// <summary>
    /// The page number, counted from 1; null when the finding is about the whole document, such as a missing language
    /// or a font that isn't embedded, or when its item's page isn't known.
    /// </summary>
    public required int? Page { get; init; }

    /// <summary>
    /// Where it is: a section's name alone, such as "metadata", or with the item, such as "fonts > Helvetica" or "forms
    /// > trail.name".
    /// </summary>
    public required string Path { get; init; }

    /// <summary>What's wrong, as a sentence with no closing period, such as "Font Helvetica isn't embedded".</summary>
    public required string Message { get; init; }

    /// <summary>
    /// The rule's entry on the docs site: what it finds, why it matters to someone using assistive technology, and how
    /// to fix it.
    /// </summary>
    public string HelpUrl => HelpUrlOf(Check);

    internal static string HelpUrlOf(PdfCheck check) =>
        $"https://zacharydf7.github.io/pdiffy/reference/checks.html#{check.ToString().ToLowerInvariant()}";

    /// <summary>Such as "FigureAltText: Figure on p2 has no alt text (structure > Document > Figure[2])".</summary>
    public override string ToString() => $"{Check}: {Message} ({Path})";
}
