namespace PDiffy;

/// <summary>
/// The standard structure element types, for use with <see cref="PdfUaDocument.Elements(string)"/>.
/// Custom types that a PDF role-maps to one of these are matched too.
/// </summary>
public static class StructureType
{
    // Grouping

    /// <summary>The root of the document's structure (Document).</summary>
    public const string Document = "Document";

    /// <summary>A large division of the document, such as a group of chapters (Part).</summary>
    public const string Part = "Part";

    /// <summary>A body of text that stands on its own, such as one article in a magazine (Art).</summary>
    public const string Article = "Art";

    /// <summary>A section of a part or an article (Sect).</summary>
    public const string Section = "Sect";

    /// <summary>A block that groups other elements and has no meaning of its own (Div).</summary>
    public const string Division = "Div";

    /// <summary>A quoted passage of one or more paragraphs (BlockQuote).</summary>
    public const string BlockQuote = "BlockQuote";

    /// <summary>A caption for a figure, table or other element (Caption).</summary>
    public const string Caption = "Caption";

    /// <summary>A table of contents (TOC).</summary>
    public const string TableOfContents = "TOC";

    /// <summary>A table of contents item (TOCI).</summary>
    public const string TableOfContentsItem = "TOCI";

    /// <summary>An index of terms, with references to where each appears (Index).</summary>
    public const string Index = "Index";

    /// <summary>A grouping with no meaning, whose children count as if they were in its parent (NonStruct).</summary>
    public const string NonStructural = "NonStruct";

    /// <summary>
    /// Content the app that wrote the PDF keeps for itself, with no meaning to other readers (Private).
    /// </summary>
    public const string Private = "Private";

    // Headings and paragraphs

    /// <summary>A heading with no level in its name, so its level comes from how deeply it's nested (H).</summary>
    public const string Heading = "H";

    /// <summary>A level 1 heading (H1).</summary>
    public const string Heading1 = "H1";

    /// <summary>A level 2 heading (H2).</summary>
    public const string Heading2 = "H2";

    /// <summary>A level 3 heading (H3).</summary>
    public const string Heading3 = "H3";

    /// <summary>A level 4 heading (H4).</summary>
    public const string Heading4 = "H4";

    /// <summary>A level 5 heading (H5).</summary>
    public const string Heading5 = "H5";

    /// <summary>A level 6 heading (H6).</summary>
    public const string Heading6 = "H6";

    /// <summary>A paragraph (P).</summary>
    public const string Paragraph = "P";

    // Lists

    /// <summary>A list (L).</summary>
    public const string List = "L";

    /// <summary>One item of a list, holding its label and body (LI).</summary>
    public const string ListItem = "LI";

    /// <summary>A list item's bullet or number, or the label of a note or bibliography entry (Lbl).</summary>
    public const string Label = "Lbl";

    /// <summary>A list item's content, after its label (LBody).</summary>
    public const string ListBody = "LBody";

    // Tables

    /// <summary>A table (Table).</summary>
    public const string Table = "Table";

    /// <summary>A table row (TR).</summary>
    public const string TableRow = "TR";

    /// <summary>A table header cell (TH).</summary>
    public const string TableHeaderCell = "TH";

    /// <summary>A table data cell (TD).</summary>
    public const string TableDataCell = "TD";

    /// <summary>The group of header rows at the top of a table (THead).</summary>
    public const string TableHead = "THead";

    /// <summary>The group of rows that make up the body of a table (TBody).</summary>
    public const string TableBody = "TBody";

    /// <summary>The group of footer rows at the bottom of a table (TFoot).</summary>
    public const string TableFoot = "TFoot";

    // Inline

    /// <summary>A run of inline content, often marked to give it a property such as its language (Span).</summary>
    public const string Span = "Span";

    /// <summary>A quotation within a line of text (Quote).</summary>
    public const string Quote = "Quote";

    /// <summary>A footnote or endnote (Note).</summary>
    public const string Note = "Note";

    /// <summary>
    /// A reference to content elsewhere in the document, such as a page number in an index (Reference).
    /// </summary>
    public const string Reference = "Reference";

    /// <summary>A bibliography entry (BibEntry).</summary>
    public const string BibliographyEntry = "BibEntry";

    /// <summary>Computer code, such as a program listing or a command (Code).</summary>
    public const string Code = "Code";

    /// <summary>A link, holding its link annotation and the text or image it covers (Link).</summary>
    public const string Link = "Link";

    /// <summary>An annotation other than a link or form field, placed in the reading order (Annot).</summary>
    public const string Annotation = "Annot";

    // Ruby and warichu: small annotations set beside or between lines, mostly in East Asian text

    /// <summary>Base text with small text set beside it, such as a pronunciation guide (Ruby).</summary>
    public const string Ruby = "Ruby";

    /// <summary>The base text of a ruby (RB).</summary>
    public const string RubyBaseText = "RB";

    /// <summary>The small text set beside a ruby's base text (RT).</summary>
    public const string RubyText = "RT";

    /// <summary>Punctuation around ruby text, such as parentheses, for viewers that can't show ruby (RP).</summary>
    public const string RubyPunctuation = "RP";

    /// <summary>A comment set in two smaller lines within one line of text (Warichu).</summary>
    public const string Warichu = "Warichu";

    /// <summary>The text of a warichu (WT).</summary>
    public const string WarichuText = "WT";

    /// <summary>Punctuation around a warichu, such as parentheses (WP).</summary>
    public const string WarichuPunctuation = "WP";

    // Illustrations and forms

    /// <summary>An image or drawing (Figure).</summary>
    public const string Figure = "Figure";

    /// <summary>A mathematical formula (Formula).</summary>
    public const string Formula = "Formula";

    /// <summary>A form field, holding its widget annotation (Form).</summary>
    public const string Form = "Form";
}
