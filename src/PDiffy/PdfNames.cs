namespace PDiffy;

/// <summary>The standard values of <see cref="PdfAnnotation.Subtype"/>.</summary>
public static class PdfAnnotationTypes
{
    /// <summary>The PDF doesn't say what kind of annotation it is; PDiffy's value, not a PDF name.</summary>
    public const string None = "(none)";

    /// <summary>A note icon that opens a pop-up (Text).</summary>
    public const string Text = "Text";

    /// <summary>A link to a page, a web address or another action (Link).</summary>
    public const string Link = "Link";

    /// <summary>Text shown on the page itself, such as a callout (FreeText).</summary>
    public const string FreeText = "FreeText";

    /// <summary>A straight line (Line).</summary>
    public const string Line = "Line";

    /// <summary>A rectangle (Square).</summary>
    public const string Square = "Square";

    /// <summary>An ellipse (Circle).</summary>
    public const string Circle = "Circle";

    /// <summary>A closed shape (Polygon).</summary>
    public const string Polygon = "Polygon";

    /// <summary>An open shape (PolyLine).</summary>
    public const string PolyLine = "PolyLine";

    /// <summary>Highlighted text (Highlight).</summary>
    public const string Highlight = "Highlight";

    /// <summary>Underlined text (Underline).</summary>
    public const string Underline = "Underline";

    /// <summary>Text with a wavy underline (Squiggly).</summary>
    public const string Squiggly = "Squiggly";

    /// <summary>Struck-out text (StrikeOut).</summary>
    public const string StrikeOut = "StrikeOut";

    /// <summary>A rubber stamp, such as "Approved" (Stamp).</summary>
    public const string Stamp = "Stamp";

    /// <summary>A mark where text should be inserted (Caret).</summary>
    public const string Caret = "Caret";

    /// <summary>A freehand drawing (Ink).</summary>
    public const string Ink = "Ink";

    /// <summary>The pop-up window of another annotation (Popup).</summary>
    public const string Popup = "Popup";

    /// <summary>An attached file (FileAttachment).</summary>
    public const string FileAttachment = "FileAttachment";

    /// <summary>A sound (Sound).</summary>
    public const string Sound = "Sound";

    /// <summary>A movie (Movie).</summary>
    public const string Movie = "Movie";

    /// <summary>A form field's appearance on the page (Widget).</summary>
    public const string Widget = "Widget";

    /// <summary>An area that plays media (Screen).</summary>
    public const string Screen = "Screen";

    /// <summary>A mark for the printer, such as a crop mark (PrinterMark).</summary>
    public const string PrinterMark = "PrinterMark";

    /// <summary>Trapping for print production (TrapNet).</summary>
    public const string TrapNet = "TrapNet";

    /// <summary>A watermark (Watermark).</summary>
    public const string Watermark = "Watermark";

    /// <summary>A 3D model (3D).</summary>
    public const string ThreeD = "3D";

    /// <summary>Content marked for removal (Redact).</summary>
    public const string Redact = "Redact";

    /// <summary>A projection of 3D content or a geospatial image (Projection).</summary>
    public const string Projection = "Projection";

    /// <summary>Rich media, such as video or Flash content (RichMedia).</summary>
    public const string RichMedia = "RichMedia";
}

/// <summary>The standard values of <see cref="PdfAnnotation.Action"/>.</summary>
public static class PdfActionTypes
{
    /// <summary>Go to a place in the document (GoTo).</summary>
    public const string GoTo = "GoTo";

    /// <summary>Go to a place in another PDF (GoToR).</summary>
    public const string GoToRemote = "GoToR";

    /// <summary>Go to a place in an embedded file (GoToE).</summary>
    public const string GoToEmbedded = "GoToE";

    /// <summary>Go to a document part (GoToDp).</summary>
    public const string GoToDocumentPart = "GoToDp";

    /// <summary>Open a file or run an application (Launch).</summary>
    public const string Launch = "Launch";

    /// <summary>Start reading an article thread (Thread).</summary>
    public const string Thread = "Thread";

    /// <summary>Open a web address that the link doesn't name (URI).</summary>
    public const string Uri = "URI";

    /// <summary>Play a sound (Sound).</summary>
    public const string Sound = "Sound";

    /// <summary>Play a movie (Movie).</summary>
    public const string Movie = "Movie";

    /// <summary>Show or hide annotations (Hide).</summary>
    public const string Hide = "Hide";

    /// <summary>Run a viewer command, such as next page (Named).</summary>
    public const string Named = "Named";

    /// <summary>Send the form's data (SubmitForm).</summary>
    public const string SubmitForm = "SubmitForm";

    /// <summary>Reset the form's fields (ResetForm).</summary>
    public const string ResetForm = "ResetForm";

    /// <summary>Fill the form from a file (ImportData).</summary>
    public const string ImportData = "ImportData";

    /// <summary>Show or hide layers (SetOCGState).</summary>
    public const string SetLayerState = "SetOCGState";

    /// <summary>Play or control media (Rendition).</summary>
    public const string Rendition = "Rendition";

    /// <summary>Update the display with a transition (Trans).</summary>
    public const string Transition = "Trans";

    /// <summary>Show a view of a 3D model (GoTo3DView).</summary>
    public const string GoTo3DView = "GoTo3DView";

    /// <summary>Run a script (JavaScript).</summary>
    public const string JavaScript = "JavaScript";

    /// <summary>Send a command to rich media (RichMediaExecute).</summary>
    public const string RichMediaExecute = "RichMediaExecute";
}

/// <summary>The standard values of <see cref="PdfDestination.View"/>.</summary>
public static class PdfDestinationViews
{
    /// <summary>A position on the page, and optionally a zoom (XYZ).</summary>
    public const string Position = "XYZ";

    /// <summary>The whole page (Fit).</summary>
    public const string Fit = "Fit";

    /// <summary>The page's width, from a given top (FitH).</summary>
    public const string FitWidth = "FitH";

    /// <summary>The page's height, from a given left (FitV).</summary>
    public const string FitHeight = "FitV";

    /// <summary>A rectangle on the page (FitR).</summary>
    public const string FitRectangle = "FitR";

    /// <summary>Everything drawn on the page (FitB).</summary>
    public const string FitContent = "FitB";

    /// <summary>The width of everything drawn on the page, from a given top (FitBH).</summary>
    public const string FitContentWidth = "FitBH";

    /// <summary>The height of everything drawn on the page, from a given left (FitBV).</summary>
    public const string FitContentHeight = "FitBV";
}

/// <summary>The standard values of <see cref="PdfPage.Tabs"/>.</summary>
public static class PdfTabOrders
{
    /// <summary>Row by row (R).</summary>
    public const string Rows = "R";

    /// <summary>Column by column (C).</summary>
    public const string Columns = "C";

    /// <summary>In structure order, which PDF/UA requires on a page with annotations (S).</summary>
    public const string Structure = "S";

    /// <summary>In the order the page lists its annotations (A).</summary>
    public const string Annotations = "A";

    /// <summary>In the order the page lists its form fields, then the other annotations (W).</summary>
    public const string Widgets = "W";
}

/// <summary>The standard values of <see cref="PdfStructureElement.Scope"/>.</summary>
public static class PdfTableScopes
{
    /// <summary>The header cell is for its row.</summary>
    public const string Row = "Row";

    /// <summary>The header cell is for its column.</summary>
    public const string Column = "Column";

    /// <summary>The header cell is for its row and its column.</summary>
    public const string Both = "Both";
}

/// <summary>The standard values of <see cref="PdfStructureElement.ListNumbering"/>.</summary>
public static class PdfListNumberings
{
    /// <summary>No marker.</summary>
    public const string None = "None";

    /// <summary>A filled round bullet.</summary>
    public const string Disc = "Disc";

    /// <summary>An open round bullet.</summary>
    public const string Circle = "Circle";

    /// <summary>A square bullet.</summary>
    public const string Square = "Square";

    /// <summary>1, 2, 3.</summary>
    public const string Decimal = "Decimal";

    /// <summary>I, II, III.</summary>
    public const string UpperRoman = "UpperRoman";

    /// <summary>i, ii, iii.</summary>
    public const string LowerRoman = "LowerRoman";

    /// <summary>A, B, C.</summary>
    public const string UpperAlpha = "UpperAlpha";

    /// <summary>a, b, c.</summary>
    public const string LowerAlpha = "LowerAlpha";

    /// <summary>A bullet the PDF doesn't name.</summary>
    public const string Unordered = "Unordered";

    /// <summary>A number the PDF doesn't name.</summary>
    public const string Ordered = "Ordered";

    /// <summary>A list of terms and their descriptions.</summary>
    public const string Description = "Description";
}

/// <summary>The standard values of <see cref="PdfPageLabelRange.Style"/>.</summary>
public static class PdfPageLabelStyles
{
    /// <summary>1, 2, 3 (D).</summary>
    public const string Decimal = "D";

    /// <summary>I, II, III (R).</summary>
    public const string UpperRoman = "R";

    /// <summary>i, ii, iii (r).</summary>
    public const string LowerRoman = "r";

    /// <summary>A, B, C (A).</summary>
    public const string UpperLetters = "A";

    /// <summary>a, b, c (a).</summary>
    public const string LowerLetters = "a";
}

/// <summary>The standard values of <see cref="PdfPageText.Type"/>.</summary>
public static class PdfArtifactTypes
{
    /// <summary>Running headers, footers and page numbers.</summary>
    public const string Pagination = "Pagination";

    /// <summary>Typographic decoration, such as rules between columns.</summary>
    public const string Layout = "Layout";

    /// <summary>Marks for production, such as crop marks.</summary>
    public const string Page = "Page";

    /// <summary>Background images and colors.</summary>
    public const string Background = "Background";
}

/// <summary>The standard values of <see cref="PdfPageText.Subtype"/>.</summary>
public static class PdfArtifactSubtypes
{
    /// <summary>A running header.</summary>
    public const string Header = "Header";

    /// <summary>A running footer.</summary>
    public const string Footer = "Footer";

    /// <summary>A watermark.</summary>
    public const string Watermark = "Watermark";

    /// <summary>A page number.</summary>
    public const string PageNumber = "PageNum";

    /// <summary>A Bates number, as in legal documents.</summary>
    public const string Bates = "Bates";

    /// <summary>A line number.</summary>
    public const string LineNumber = "LineNum";

    /// <summary>A redaction.</summary>
    public const string Redaction = "Redaction";
}

/// <summary>The standard values of <see cref="PdfFont.Subtype"/>.</summary>
public static class PdfFontTypes
{
    /// <summary>A composite font, such as one for many scripts (Type0).</summary>
    public const string Type0 = "Type0";

    /// <summary>A PostScript font (Type1).</summary>
    public const string Type1 = "Type1";

    /// <summary>A multiple-master PostScript font (MMType1).</summary>
    public const string MultipleMaster = "MMType1";

    /// <summary>A font whose glyphs the PDF draws itself (Type3).</summary>
    public const string Type3 = "Type3";

    /// <summary>A TrueType font (TrueType).</summary>
    public const string TrueType = "TrueType";
}

/// <summary>The standard values of <see cref="PdfEmbeddedFile.Relationship"/>.</summary>
public static class PdfFileRelationships
{
    /// <summary>The file the document was made from, such as a spreadsheet.</summary>
    public const string Source = "Source";

    /// <summary>Data the document shows, such as a table's values.</summary>
    public const string Data = "Data";

    /// <summary>Another version of the document's content, such as audio.</summary>
    public const string Alternative = "Alternative";

    /// <summary>A file that adds to the document, such as a font.</summary>
    public const string Supplement = "Supplement";

    /// <summary>An encrypted document the PDF carries.</summary>
    public const string EncryptedPayload = "EncryptedPayload";

    /// <summary>The data of the document's form.</summary>
    public const string FormData = "FormData";

    /// <summary>A schema for the document's data.</summary>
    public const string Schema = "Schema";

    /// <summary>No relationship given.</summary>
    public const string Unspecified = "Unspecified";
}
