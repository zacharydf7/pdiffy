# Check documents directly

You don't need a snapshot to use PDiffy. [`PdfUaDocument.Load`](xref:PDiffy.PdfUaDocument.Load*) reads the whole PDF
once into a model you can assert on, with no PDF knowledge needed: enums and constant classes list every option.

- **Tags, alt text and reading order:** [`Structure`](xref:PDiffy.PdfUaDocument.Structure) and
  [`Elements(type)`](xref:PDiffy.PdfUaDocument.Elements*), with types listed in [`StructureType`](xref:PDiffy.StructureType).
- **Untagged and artifact content:** [`TextRuns`](xref:PDiffy.PdfUaDocument.TextRuns),
  [`Untagged`](xref:PDiffy.PdfUaDocument.Untagged) and [`Artifacts`](xref:PDiffy.PdfUaDocument.Artifacts), with roles
  listed in [`PdfContentRole`](xref:PDiffy.PdfContentRole).
- **Links and comments:** [`Annotations`](xref:PDiffy.PdfUaDocument.Annotations), with types listed in
  [`PdfAnnotationTypes`](xref:PDiffy.PdfAnnotationTypes) and flags in [`PdfAnnotationFlags`](xref:PDiffy.PdfAnnotationFlags).
- **Form fields:** [`FormFields`](xref:PDiffy.PdfUaDocument.FormFields), with types and flags listed in
  [`PdfFormFieldType`](xref:PDiffy.PdfFormFieldType) and [`PdfFormFieldFlags`](xref:PDiffy.PdfFormFieldFlags).
- **How the file opens and what it allows:** [`Settings`](xref:PDiffy.PdfUaDocument.Settings), with options listed in
  [`PdfPageModes`](xref:PDiffy.PdfPageModes), [`PdfPageLayouts`](xref:PDiffy.PdfPageLayouts) and
  [`PdfPermissions`](xref:PDiffy.PdfPermissions).
- **Fonts and copyable text:** [`Fonts`](xref:PDiffy.PdfUaDocument.Fonts).
- **Fonts, colors and hidden text on the page:** [`TextRuns`](xref:PDiffy.PdfUaDocument.TextRuns),
  [`Images`](xref:PDiffy.PdfUaDocument.Images) and [`Paths`](xref:PDiffy.PdfUaDocument.Paths), with render modes listed
  in [`PdfTextRenderMode`](xref:PDiffy.PdfTextRenderMode).
- **Language, title and page count:** [`Document`](xref:PDiffy.PdfUaDocument.Document),
  [`Metadata`](xref:PDiffy.PdfUaDocument.Metadata) and [`Pages`](xref:PDiffy.PdfUaDocument.Pages).

## Built-in checks

[`Check`](xref:PDiffy.PdfUaDocument.Check*) applies the rules generated documents most often break and lists what it
finds. Each [`PdfCheckFinding`](xref:PDiffy.PdfCheckFinding) has the check, the page, a path and a message, so a failed test
says which figure on which page. The path names the snapshot section and the item; for a structure element, it's the
path a snapshot diff shows:

```csharp
IReadOnlyList<PdfCheckFinding> findings = document.Check(PdfCheck.DocumentLanguage, PdfCheck.FigureAltText);

// On failure: "FigureAltText: Figure on p2 has no alt text (structure > Document > Figure[2])".
Assert.Empty(findings);
```

`Check()` with no arguments applies every check. Pick the ones a test guards. Each finding has a
[`Severity`](xref:PDiffy.PdfCheckFinding.Severity), `Error` for a rule PDF/UA requires and `Warning` for a house rule, and a
[`Reference`](xref:PDiffy.PdfCheckFinding.Reference) to the clause of PDF/UA-1 (ISO 14289-1) the rule comes from, listed under From
below. The reference says where a rule comes from, not that the document conforms.

| PdfCheck | Finds | From |
|---|---|---|
| `DocumentLanguage` | A document with no language | 7.2 |
| `DocumentTitle` | A document with no title, or one whose viewer shows the file name instead | 7.1 |
| `FigureAltText` | A figure with no alt text or replacement text | 7.3 |
| `UntaggedContent` | Text, images and paths that are neither tagged nor artifacts, page by page | 7.1 |
| `LinkDescriptions` | A link with no description: no contents, and no alt text or text on the element that tags it | 7.18.5 |
| `FormFieldNames` | A form field with no accessible name (tooltip) | 7.18.1 |
| `TableHeaders` | A header with no scope that no cell names, and a cell that names a header that doesn't exist | 7.5 |
| `HeadingLevels` | A heading that skips a level, or one that comes before any H1 | 7.4.2 |
| `FontsEmbedded` | A font that isn't embedded | 7.21.4.1 |
| `FontsMapToUnicode` | A font whose text uses codes that don't map to Unicode | 7.21.7 |
| `NoActions` | JavaScript, actions run on events such as closing or printing, and an open action that does more than go to a page | House rule |
| `NoEmbeddedFiles` | Each embedded file | House rule |

These are fast regression checks for the mistakes generated documents make, not a PDF/UA validator: for conformance,
use [veraPDF](https://verapdf.org/) or PAC. On the command line, [`pdiffy check`](../reference/command-line.md#check)
runs them all. For a rule of your own, assert on the model, as the rest of this page shows.

## Accessibility

```csharp
[Fact]
public async Task TrailMap_ShouldBeReadableByScreenReaders_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailMapAsync();
    PdfUaDocument document = PdfUaDocument.Load(pdf);

    // The document is tagged and declares a language, so a screen reader knows how to pronounce it.
    Assert.True(document.Document.Marked);
    Assert.Equal("en-US", document.Document.Language);

    // Every figure (logo, chart, signature) has alt text describing it.
    // Elements also finds custom tag types that the PDF maps to Figure.
    Assert.All(document.Elements(StructureType.Figure), figure => Assert.False(string.IsNullOrWhiteSpace(figure.AltText)));

    // Every piece of text is either tagged or marked as decoration. Untagged text is invisible to a screen reader.
    Assert.DoesNotContain(document.TextRuns, run => run.Role == PdfContentRole.Untagged);

    // Every link has a description a screen reader can announce.
    Assert.All(document.Annotations.Where(annotation => annotation.Subtype == PdfAnnotationTypes.Link),
        link => Assert.False(string.IsNullOrWhiteSpace(link.Contents)));
}
```

## Walk the structure tree

The document's [`Elements(type)`](xref:PDiffy.PdfUaDocument.Elements*) throws for a type that's neither standard, nor in
the role map, nor used in the document, with a hint such as "Did you mean 'Figure'?", so a typo can't make an
`Assert.All` over no elements pass. Each element has:

- `Text`, the text of its own marked content, and `AllText`, its text with its descendants', such as a table cell's
  paragraph or a paragraph's link, joined by position as the snapshot joins text.
- `TextRuns`, the text runs its own marked content draws, with each one's font, size and color, such as for checking
  that every `H1` is 18 pt.
- `Descendants()`, and `Elements(type)` for one type. An element's `Elements` doesn't check the type, so use the
  [`StructureType`](xref:PDiffy.StructureType) constants.
- `Path`, the path a check finding shows, such as `structure > Document > Table > TR[2]`.

[`Headings()`](xref:PDiffy.PdfUaDocument.Headings) lists every heading with its level, role-mapped types included.

```csharp
PdfUaDocument document = PdfUaDocument.Load("trail-guide.pdf");

// Headings go down one level at a time, so a screen reader's outline has no gaps.
var levels = document.Headings().Select(heading => heading.Level ?? 1).ToList();
Assert.All(levels.Zip(levels.Skip(1)), pair => Assert.True(pair.Second <= pair.First + 1));

// Every table cell says something.
PdfStructureElement table = document.Elements(StructureType.Table).Single();
Assert.All(table.Elements(StructureType.TableDataCell), cell => Assert.False(string.IsNullOrWhiteSpace(cell.AllText)));
```

## Forms

Each form field has its full name, type, accessible name, value and flags.

```csharp
[Fact]
public async Task EnrollmentForm_ShouldLabelAndRequireTheRightFields_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateEnrollmentFormAsync();
    PdfUaDocument document = PdfUaDocument.Load(pdf);

    // Every field has a name a screen reader announces (the "tooltip" in most PDF editors).
    Assert.All(document.FormFields, field => Assert.False(string.IsNullOrWhiteSpace(field.AccessibleName)));

    // Find the required fields. Names include their parents, joined with dots.
    var required = document.FormFields.Where(field => field.Flags.HasFlag(PdfFormFieldFlags.Required)).Select(field => field.Name);
    Assert.Equal(["hiker.name", "hiker.dateOfBirth"], required);

    // The state picker is a drop-down with all 50 states.
    PdfFormField state = document.FormFields.Single(field => field.Name == "hiker.address.state");
    Assert.Equal(PdfFormFieldType.ComboBox, state.Type);
    Assert.Equal(50, state.Options.Count);
}
```

## Document settings

[`Settings`](xref:PDiffy.PdfSettings) covers how the file opens and what it allows. It's where a library upgrade or
a configuration change tends to show up first.

```csharp
[Fact]
public async Task ExportedReport_ShouldOpenSafely_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateReportAsync();
    PdfSettings settings = PdfUaDocument.Load(pdf).Settings;

    // No scripts and no attached files.
    Assert.False(settings.HasJavaScript);
    Assert.Empty(settings.EmbeddedFiles);

    // If the file is ever encrypted, it must still let screen readers extract the text.
    // Permissions is null for an unencrypted file.
    Assert.True(settings.Permissions is null || settings.Permissions.Value.HasFlag(PdfPermissions.ExtractForAccessibility));

    // Open with bookmarks showing, one page at a time in a continuous column.
    Assert.Equal(PdfPageModes.UseOutlines, settings.PageMode);
    Assert.Equal(PdfPageLayouts.OneColumn, settings.PageLayout);
}
```

## How text is drawn

The layout lists every text run, image and path with its position and style. It's useful for rules that are hard to
see by eye, such as hidden text or off-brand colors.

```csharp
[Fact]
public async Task Brochure_ShouldUseOnlyBrandFontsAndColors_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateBrochureAsync();
    PdfUaDocument document = PdfUaDocument.Load(pdf);

    // No invisible text. It usually means an OCR layer or text hidden behind an image.
    Assert.DoesNotContain(document.TextRuns, run => run.RenderMode == PdfTextRenderMode.Invisible);

    // Only the two brand fonts. Subset prefixes such as "ABCDEF+" are already removed.
    Assert.All(document.TextRuns, run => Assert.Contains(run.Font, new[] { "OpenSans", "OpenSans-Bold" }));

    // Body text is black or brand navy. Colors are written as gray:0 (black), #rrggbb or cmyk:c,m,y,k.
    Assert.All(document.TextRuns.Where(run => run.Size <= 12), run => Assert.Contains(run.Color, new[] { "gray:0", "#1f3a5f" }));
}
```

## Fonts that can't be read aloud

A font whose character codes don't map to Unicode produces text that can't be copied, searched or read aloud:

```csharp
Assert.All(document.Fonts, font => Assert.Empty(font.UnmappedCodes));
```

## Word documents

[`DocxDocument`](xref:PDiffy.DocxDocument) works the same way for DOCX files. See
[Word documents](word-documents.md#assert-on-the-model).

## See also

- [`PdfUaDocument`](xref:PDiffy.PdfUaDocument) in the API reference
- [PDF snapshot format](../reference/pdf-snapshot-format.md): what each property records
