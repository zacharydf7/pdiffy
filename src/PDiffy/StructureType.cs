namespace PDiffy;

/// <summary>
/// The standard structure element types, for use with <see cref="PdfUaDocument.Elements(string)"/>.
/// Custom types that a PDF role-maps to one of these are matched too.
/// </summary>
public static class StructureType
{
    // Grouping
    public const string Document = "Document";
    public const string Part = "Part";
    public const string Article = "Art";
    public const string Section = "Sect";
    public const string Division = "Div";
    public const string BlockQuote = "BlockQuote";
    public const string Caption = "Caption";
    public const string TableOfContents = "TOC";
    public const string TableOfContentsItem = "TOCI";
    public const string Index = "Index";
    public const string NonStructural = "NonStruct";
    public const string Private = "Private";

    // Headings and paragraphs
    public const string Heading = "H";
    public const string Heading1 = "H1";
    public const string Heading2 = "H2";
    public const string Heading3 = "H3";
    public const string Heading4 = "H4";
    public const string Heading5 = "H5";
    public const string Heading6 = "H6";
    public const string Paragraph = "P";

    // Lists
    public const string List = "L";
    public const string ListItem = "LI";
    public const string Label = "Lbl";
    public const string ListBody = "LBody";

    // Tables
    public const string Table = "Table";
    public const string TableRow = "TR";
    public const string TableHeaderCell = "TH";
    public const string TableDataCell = "TD";
    public const string TableHead = "THead";
    public const string TableBody = "TBody";
    public const string TableFoot = "TFoot";

    // Inline
    public const string Span = "Span";
    public const string Quote = "Quote";
    public const string Note = "Note";
    public const string Reference = "Reference";
    public const string BibliographyEntry = "BibEntry";
    public const string Code = "Code";
    public const string Link = "Link";
    public const string Annotation = "Annot";

    // Ruby and warichu: small annotations set beside or between lines, mostly in East Asian text
    public const string Ruby = "Ruby";
    public const string RubyBaseText = "RB";
    public const string RubyText = "RT";
    public const string RubyPunctuation = "RP";
    public const string Warichu = "Warichu";
    public const string WarichuText = "WT";
    public const string WarichuPunctuation = "WP";

    // Illustrations and forms
    public const string Figure = "Figure";
    public const string Formula = "Formula";
    public const string Form = "Form";
}
