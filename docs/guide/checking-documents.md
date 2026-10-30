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
[`Severity`](xref:PDiffy.PdfCheckFinding.Severity), `Error` for a rule PDF/UA requires and `Warning` for a practice
another standard, such as WCAG, asks for that PDF/UA doesn't check, or a house rule, a
[`Category`](xref:PDiffy.PdfCheckFinding.Category), the family its rule belongs to (`HouseRules` for policies that
aren't accessibility rules), and a [`Reference`](xref:PDiffy.PdfCheckFinding.Reference) to the clause the rule comes
from, of PDF/UA-1 (ISO 14289-1) or WCAG 2.2, listed under From below. The reference says where a rule comes from, not
that the document conforms.

| PdfCheck | Finds | Category | From |
|---|---|---|---|
| `DocumentLanguage` | A document with no language | `Language` | 7.2 |
| `DocumentTitle` | A document with no title, or one whose viewer shows the file name instead | `Document` | 7.1 |
| `FigureAltText` | A figure with no alt text or replacement text | `AltText` | 7.3 |
| `UntaggedContent` | Text, images and paths that are neither tagged nor artifacts, page by page | `Content` | 7.1 |
| `LinkDescriptions` | A link with no description: no contents, and no alt text or text on the element that tags it | `Annotations` | 7.18.5 |
| `FormFieldNames` | A form field with no accessible name (tooltip) | `Forms` | 7.18.1 |
| `TableHeaders` | A header with no scope that no cell names, and a cell that names a header that doesn't exist | `Tables` | 7.5 |
| `HeadingLevels` | A heading that skips a level, or one that comes before any H1 | `Headings` | 7.4.2 |
| `HeadingTypes` | Unnumbered (H) and numbered (H1 to H6) headings in one document, and an element with more than one H | `Headings` | 7.4.4 |
| `NoteIds` | A note with no ID, and a note with the ID of an earlier one | `Structure` | 7.9 |
| `TableStructure` | A row, cell or row group outside where a table has them, one holding what it can't, a second THead, TFoot or Caption, a THead or TFoot with no TBody, and a caption that isn't first or last | `Tables` | 7.2 |
| `TableGrid` | Cells that overlap, a cell that spans past the last row, and a row with a different number of columns from the first, counting spans | `Tables` | 7.2 |
| `AnnotationTags` | A link not tagged with Link, a widget not tagged with Form, another annotation not tagged with Annot (or not tagged at all), a Form with no field role that holds anything but one widget, a TrapNet annotation, and a tagged printer mark | `Annotations` | 7.18 |
| `AnnotationDescriptions` | An annotation other than a link or form field with no contents and no alt text on the element that tags it | `Annotations` | 7.18.1 |
| `TabOrder` | A page with annotations whose tab order isn't the structure order (S) | `Annotations` | 7.18.3 |
| `LanguageTags` | A document, element or marked-content language that isn't a well-formed language tag, such as `english` or `en_US` | `Language` | 7.2 |
| `TextLanguage` | In a document with no language that sets some on its elements or marked content: text, alt text, annotation contents, form field names, bookmark titles and default-language XMP text with no language to take | `Language` | 7.2 |
| `FormulaAltText` | A formula with no alt text or replacement text | `AltText` | 7.7 |
| `MediaClips` | A media clip a Screen annotation plays with no content type or no alternate text | `AltText` | 7.18.6.2 |
| `ArtifactNesting` | An artifact inside tagged content, or an artifact holding tagged content, page by page | `Content` | 7.1 |
| `LayerConfigurations` | An optional-content configuration with no name, or one that turns layers on or off by itself (`AS`) | `Document` | 7.10 |
| `EmbeddedFileNames` | A file in the document's list of embedded files not named both as `F` and as `UF` | `Document` | 7.11 |
| `NoDynamicXfa` | A dynamic XFA form | `Forms` | 7.15 |
| `AccessibilityPermission` | Encryption that doesn't let assistive technology extract the text | `Document` | 7.16 |
| `NoReferenceXObjects` | A reference XObject, which shows a page of another file, in the pages' resources | `Content` | 7.20 |
| `MeaningfulAltText` | A warning: alt text that's a file name, such as `IMG_2041.jpg`, or a placeholder such as `image` or `graphic 3` | `AltText` | WCAG 1.1.1 |
| `LinkText` | A warning: a link that says only something vague, such as "click here", "read more" or "link", with no other text saying where it goes | `Annotations` | WCAG 2.4.4 |
| `FontsEmbedded` | A font that isn't embedded | `Fonts` | 7.21.4.1 |
| `FontsMapToUnicode` | A font whose text uses codes that don't map to Unicode | `Fonts` | 7.21.7 |
| `NoActions` | JavaScript, actions run on events such as closing or printing, and an open action that does more than go to a page | `HouseRules` | House rule |
| `NoEmbeddedFiles` | Each embedded file | `HouseRules` | House rule |
| `RoleMap` | A custom structure type that doesn't map to a standard one, a mapping that loops back on itself, and a standard type that's remapped | `Structure` | 7.1 |
| `XmpMetadata` | No XMP metadata, or XMP that doesn't say the document is PDF/UA-1 with the `pdfuaid` prefix, or has no `dc:title` | `Document` | 5, 7.1 |
| `TaggedPdf` | A document that isn't marked as tagged, says its tags may be unreliable (`Suspects`), has no structure tree, or has elements with no parent entry | `Structure` | 6.2, 7.1 |
| `PdfVersion` | A file header or catalog version past PDF 1.7 | `Document` | 6.1 |
| `ListStructure` | A list item outside a list or a list body outside an item, a list holding something other than lists, items and a caption, an item holding something other than a label and a body, and a list caption that isn't first | `Lists` | 7.2 |
| `TocStructure` | A table of contents item outside a table of contents, one holding something other than tables of contents, items and a caption, and a caption that isn't first | `Lists` | 7.2 |

`Check()` applies every rule PDiffy has, so an upgrade that adds rules can fail a test that passed. To choose when that
happens, pass a [`PdfCheckSet`](xref:PDiffy.PdfCheckSet): a release's own set, such as `PdfCheckSet.Version0_3`, keeps
the rules that release shipped whatever later releases add, while `PdfCheckSet.Latest` grows with each release, as .NET
analyzers do with `AnalysisLevel`. `Only` and `Except` narrow a set:

```csharp
document.Check(PdfCheckSet.Version0_3);                          // only the rules 0.3 shipped
document.Check(PdfCheckSet.Latest.Only(PdfCheckCategory.Tables));
document.Check(PdfCheckSet.Latest.Except(PdfCheck.HeadingLevels));
```

### Approve findings with the snapshot

A document generated from real data often starts with problems no one can fix at once. Instead of asserting that a check
finds nothing, record what it finds in the snapshot with [`Check`](xref:PDiffy.PdfSnapshotOptions.Check*), and approve
the findings along with everything else:

```csharp
await Snapshot.VerifyPdfAsync(pdf, configure: options => options.Check(PdfCheckSet.Latest));
```

```text
# pdfua-snapshot v1
# checks: latest, 37 rules
...
checks
  FigureAltText p2 structure > Document > Figure[2]
  TableHeaders p3 structure > Document > Table > TR[1] > TH[2] > scope
```

A new problem fails the test, like any other change. A fixed one fails it too until the snapshot is accepted, so the
problem can't come back unnoticed. Each line names the rule, the page and the path, with no message text, so rewording a
message never changes a snapshot. The checks look at the whole document: `OnlyPages` narrows their findings, and
`Ignore` doesn't change them, since it controls what's compared; to leave a rule out, leave it out of the set.
Scrubbers apply to the paths too, so a form field or attachment name gets the same token as in its own section. Without
the option, no snapshot prints a `checks` section.

[Check rules](../reference/checks.md) explains each rule: why it matters to someone using assistive technology and
how to fix it. A finding's `HelpUrl` links to its rule's entry.

Since the approved snapshots hold the findings, [`pdiffy findings --all`](../reference/command-line.md#findings) counts
them across a suite, by rule and by snapshot, without regenerating anything: a quick measure of how many problems a
team's documents have.

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
