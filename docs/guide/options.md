# Ignore and scrub

A snapshot records everything, so a test fails on the smallest change. When part of a document changes on every run, or
doesn't matter to a test, leave it out or replace it. Every option is an enum value or a method, so you never need to
know PDF or DOCX internals.

Options are passed as `configure`, and chain:

```csharp
await PdfSnapshot.VerifyAsync(pdf, configure: options => options
    .Ignore(Section.Metadata)
    .Ignore(Field.Positions)
    .ScrubDates()
    .OnlyPages(1, 2));
```

| To... | Use | Example |
|---|---|---|
| Compare only what a test guards | `Use(SnapshotProfile...)` | Only what assistive technology uses |
| Leave out a whole section | `Ignore(Section...)` | Layout the design team changes freely |
| Leave out one detail everywhere | `Ignore(Field...)` | Positions, colors, the title |
| Replace values that change on every run | `ScrubDates()`, `ScrubGuids()`, `ScrubText(...)` | Print dates, order numbers, transaction ids |
| Snapshot part of a long document | `OnlyPages(...)` | The summary on pages 3 and 4 |

The snapshot's first lines always list what was ignored, so nobody reading it mistakes a missing section for an empty
one.

## Use a profile

Most tests guard one thing: the content, its accessibility or how the page looks. A
[`SnapshotProfile`](xref:PDiffy.SnapshotProfile) leaves out everything else in one call, and further `Ignore` calls leave
out more:

```csharp
await PdfSnapshot.VerifyAsync(pdf, configure: options => options
    .Use(SnapshotProfile.Accessibility)
    .Ignore(Field.Language));
```

The snapshot's header lists the sections and fields a profile leaves out, not the profile's name. If a later release
changes what a profile leaves out, the header changes too, so the test fails and says so instead of quietly checking
less.

[Every profile](../reference/options.md#pdf-profiles), and what it leaves out, is listed in the reference.

## Leave out sections

Pass the sections to leave out. They print as `(ignored)` instead of their contents.

```csharp
[Fact]
public async Task InvoiceTemplate_ShouldKeepItsTagStructure_WhenStylingChanges()
{
    using Stream pdf = await ReportGenerator.CreateInvoiceAsync();

    // This test guards the tag tree and the text, so leave out the metadata, the document settings
    // and the layout (positions, fonts and colors), which the design team changes freely.
    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        .Ignore(Section.Metadata, Section.Settings, Section.Layout));
}
```

The snapshot then starts like this:

```text
# pdfua-snapshot v1
# ignored sections: settings, metadata, layout
document
  marked=yes lang=en-US display-doc-title=yes pages=2
settings
  (ignored)
metadata
  (ignored)
...
```

[Every `Section`](../reference/options.md#pdf-sections) is listed in the reference.

## Leave out one detail everywhere

For something narrower than a section, ignore a [`Field`](xref:PDiffy.Field). It's left out everywhere it appears, and
the rest of each line is still compared.

```csharp
[Fact]
public async Task MonthlyStatement_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateStatementAsync(month: 6);

    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        // The title includes the month, so it changes every time.
        .Ignore(Field.Title)
        // Text reflows when the statement has more rows, so where things land doesn't matter here.
        // Fonts, sizes and colors are still compared.
        .Ignore(Field.Positions));
}
```

With `Field.Positions` ignored, a layout line such as

```text
p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Statement"
```

becomes

```text
p1 text tagged font=Arial size=18 color=gray:0 "Statement"
```

[Every `Field`](../reference/options.md#pdf-fields) is listed in the reference.

## Allow small numeric differences

Positions are rounded to whole points, and font sizes and line widths to 0.1 pt. A value close to a rounding boundary
can flip after a font or library update: text at 72.49 pt prints `at=72,...`, and moved by 0.02 pt it prints
`at=73,...`. Ignoring `Field.Positions` would also hide a heading that moved 40 pt, so tolerate the noise instead:

```csharp
await PdfSnapshot.VerifyAsync(pdf, configure: options => options
    .Tolerate(Field.Positions, points: 1)
    .Tolerate(Field.FontSizes, points: 0.5));
```

- A number that differs from the verified one by no more than its tolerance doesn't fail the test, and the verified
  file stays as it is, even in accept mode, so the baseline doesn't creep a point at a time.
- `Positions` covers where text, images, paths and annotations are and the size of images, paths and annotations;
  `FontSizes` a text run's size; `LineWidths` a path's width. Other fields aren't numbers and can't be tolerated.
- The snapshot's header lists each tolerance (`# tolerances: positions 1pt, font-sizes 0.5pt`), so a reader knows its
  numbers are approximate. Comparing two snapshots, with `SnapshotDiff.Compare` or `pdiffy`, applies the tolerances
  their header lists.

## Scrub values that change on every run

A generated document often prints today's date, an order number or a GUID. Scrubbers replace those inside quoted text
with a stable placeholder, so the rest of the text is still compared.

```csharp
[Fact]
public async Task PharmacyReceipt_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateReceiptAsync();

    await PdfSnapshot.VerifyAsync(pdf, configure: options => options
        // "Printed 10/27/2026" becomes "Printed Date_1". The same text always gets the same number,
        // so the snapshot still shows that the print date and the fill date match.
        .ScrubDates()
        // Transaction IDs become Guid_1, Guid_2 and so on.
        .ScrubGuids()
        // Anything else that changes, with your own pattern. "$1" keeps the first group.
        .ScrubText(new Regex(@"(Rx) #\d+"), "$1 #<number>"));
}
```

> [!NOTE]
> Scrubbers only touch quoted text: element text, alt text, titles, link addresses, form values, bookmark titles and
> so on. Tag types, keys and numbers are never changed.

`ScrubDates()` recognizes `2026-10-27`, `10/27/2026`, `October 27, 2026` and `27 Oct 2026`, each with an optional time,
and PDF dates such as `D:20261027120000`.

## Snapshot only some pages

For a long document where a test is about one part, keep only those pages:

```csharp
[Fact]
public async Task AnnualReport_ShouldKeepTheSummaryTable_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateAnnualReportAsync();

    // The summary table is on pages 3 and 4; the other 120 pages have their own tests.
    await PdfSnapshot.VerifyAsync(pdf, configure: options => options.OnlyPages(3, 4));
}
```

- A structure element or bookmark stays when it, or anything under it, is on one of those pages.
- The page count, page labels and fonts still describe the whole file.
- Asking for a page past the end throws.

## Word documents

DOCX snapshots have their own sections and details, with the same scrubbers:

```csharp
await DocxSnapshot.VerifyAsync(docx, configure: options => options
    .Ignore(DocxSection.Styles)
    .Ignore(DocxDetail.FieldResults, DocxDetail.RevisionAuthors)
    .ScrubDates());
```

- [`DocxProfile`](xref:PDiffy.DocxProfile): `Use(DocxProfile.Content)` compares the text and its structure without
  page setup, styles or formatting details. See [Word profiles](../reference/options.md#word-profiles).
- [`DocxSection`](xref:PDiffy.DocxSection): `Document`, `Sections`, `Styles`, `Numbering`, `Body`, `Headers`,
  `Footers`, `Notes`.
- [`DocxDetail`](xref:PDiffy.DocxDetail): image hashes, field results, fonts, font sizes, colors, spacing, indents,
  revision and comment authors. See [the reference](../reference/options.md#word-details) for exactly what each leaves
  out.
- DOCX scrubbers also apply to link addresses.

> [!TIP]
> `DocxDetail.FieldResults` is the one most Word tests want: a PAGE or DATE field, or a table of contents, stores the
> last result Word computed, which changes whenever the document is laid out again.

## Text and JSON

Text and JSON snapshots scrub the whole text rather than quoted values. See [Text and JSON](text-and-json.md#options).

## See also

- [Options at a glance](../reference/options.md), every value in one place
- [`SnapshotOptions`](xref:PDiffy.SnapshotOptions), [`DocxSnapshotOptions`](xref:PDiffy.DocxSnapshotOptions),
  [`TextSnapshotOptions`](xref:PDiffy.TextSnapshotOptions)
