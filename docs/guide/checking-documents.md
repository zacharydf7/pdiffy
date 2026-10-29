# Check documents directly

You don't need a snapshot to use PDiffy. [`PdfUaDocument.Load`](xref:PDiffy.PdfUaDocument.Load*) reads the whole PDF
once into a model you can assert on, with no PDF knowledge needed: enums and constant classes list every option.

| To check... | Use | Options listed in |
|---|---|---|
| Tags, alt text, reading order | [`Structure`](xref:PDiffy.PdfUaDocument.Structure), [`Elements(type)`](xref:PDiffy.PdfUaDocument.Elements*) | [`StructureType`](xref:PDiffy.StructureType) |
| Untagged or artifact content | [`TextRuns`](xref:PDiffy.PdfUaDocument.TextRuns), [`Untagged`](xref:PDiffy.PdfUaDocument.Untagged), [`Artifacts`](xref:PDiffy.PdfUaDocument.Artifacts) | [`ContentRole`](xref:PDiffy.ContentRole) |
| Links and comments | [`Annotations`](xref:PDiffy.PdfUaDocument.Annotations) | [`AnnotationFlags`](xref:PDiffy.AnnotationFlags) |
| Form fields | [`FormFields`](xref:PDiffy.PdfUaDocument.FormFields) | [`FormFieldType`](xref:PDiffy.FormFieldType), [`FormFieldFlags`](xref:PDiffy.FormFieldFlags) |
| How the file opens, what it allows | [`Settings`](xref:PDiffy.PdfUaDocument.Settings) | [`PageModes`](xref:PDiffy.PageModes), [`PageLayouts`](xref:PDiffy.PageLayouts), [`DocumentPermissions`](xref:PDiffy.DocumentPermissions) |
| Fonts and copyable text | [`Fonts`](xref:PDiffy.PdfUaDocument.Fonts) | |
| Fonts, colors, hidden text | [`TextRuns`](xref:PDiffy.PdfUaDocument.TextRuns), [`Images`](xref:PDiffy.PdfUaDocument.Images), [`Paths`](xref:PDiffy.PdfUaDocument.Paths) | [`TextRenderMode`](xref:PDiffy.TextRenderMode) |
| Language, title, page count | [`Document`](xref:PDiffy.PdfUaDocument.Document), [`Metadata`](xref:PDiffy.PdfUaDocument.Metadata), [`Pages`](xref:PDiffy.PdfUaDocument.Pages) | |

## Accessibility

```csharp
[Fact]
public async Task PatientLetter_ShouldBeReadableByScreenReaders_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreatePatientLetterAsync();
    PdfUaDocument document = PdfUaDocument.Load(pdf);

    // The document is tagged and declares a language, so a screen reader knows how to pronounce it.
    Assert.True(document.Document.Marked);
    Assert.Equal("en-US", document.Document.Lang);

    // Every figure (logo, chart, signature) has alt text describing it.
    // Elements also finds custom tag types that the PDF maps to Figure.
    Assert.All(document.Elements(StructureType.Figure), figure => Assert.False(string.IsNullOrWhiteSpace(figure.Alt)));

    // Every piece of text is either tagged or marked as decoration. Untagged text is invisible to a screen reader.
    Assert.DoesNotContain(document.TextRuns, run => run.Role == ContentRole.Untagged);

    // Every link has a description a screen reader can announce.
    Assert.All(document.Annotations.Where(annotation => annotation.Subtype == "Link"),
        link => Assert.False(string.IsNullOrWhiteSpace(link.Contents)));
}
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
    var required = document.FormFields.Where(field => field.Flags.HasFlag(FormFieldFlags.Required)).Select(field => field.Name);
    Assert.Equal(["patient.name", "patient.dateOfBirth"], required);

    // The state picker is a drop-down with all 50 states.
    FormField state = document.FormFields.Single(field => field.Name == "patient.address.state");
    Assert.Equal(FormFieldType.ComboBox, state.Type);
    Assert.Equal(50, state.Options.Count);
}
```

## Document settings

[`Settings`](xref:PDiffy.DocumentSettings) covers how the file opens and what it allows. It's where a library upgrade or
a configuration change tends to show up first.

```csharp
[Fact]
public async Task ExportedReport_ShouldOpenSafely_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateReportAsync();
    DocumentSettings settings = PdfUaDocument.Load(pdf).Settings;

    // No scripts and no attached files.
    Assert.False(settings.HasJavaScript);
    Assert.Empty(settings.EmbeddedFiles);

    // If the file is ever encrypted, it must still let screen readers extract the text.
    // Permissions is null for an unencrypted file.
    Assert.True(settings.Permissions is null || settings.Permissions.Value.HasFlag(DocumentPermissions.ExtractForAccessibility));

    // Open with bookmarks showing, one page at a time in a continuous column.
    Assert.Equal(PageModes.UseOutlines, settings.PageMode);
    Assert.Equal(PageLayouts.OneColumn, settings.PageLayout);
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
    Assert.DoesNotContain(document.TextRuns, run => run.RenderMode == TextRenderMode.Invisible);

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
