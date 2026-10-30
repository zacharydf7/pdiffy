# Ignore and scrub

A snapshot records everything, so a test fails on the smallest change. When part of a document changes on every run, or
doesn't matter to a test, leave it out or replace it. Every option is an enum value or a method, so you never need to
know PDF or DOCX internals.

Options are passed as `configure`, and chain:

```csharp
await Snapshot.VerifyPdfAsync(pdf, configure: options => options
    .Ignore(PdfSection.Metadata)
    .Ignore(PdfDetail.Positions)
    .ScrubDates()
    .OnlyPages(1, 2));
```

- `Use(PdfProfile...)` compares only what a test guards, such as what assistive technology uses.
- `Ignore(PdfSection...)` leaves out a whole section, such as a layout the design team changes freely.
- `Ignore(PdfDetail...)` leaves out one detail everywhere, such as positions, colors or the title.
- `ScrubDates()`, `ScrubGuids()` and `ScrubText(...)` replace values that change on every run, such as print dates,
  permit numbers and booking ids.
- `OnlyPages(...)` snapshots part of a long document, such as the summary on pages 3 and 4.
- `Check(PdfCheckSet.Latest)` records what the [built-in checks](checking-documents.md#approve-findings-with-the-snapshot)
  find, so the findings are approved with the snapshot.

The snapshot's first lines always list what was ignored, so nobody reading it mistakes a missing section for an empty
one.

## Use a profile

Most tests guard one thing: the content, its accessibility or how the page looks. A
[`PdfProfile`](xref:PDiffy.PdfProfile) leaves out everything else in one call, and further `Ignore` calls leave
out more:

```csharp
await Snapshot.VerifyPdfAsync(pdf, configure: options => options
    .Use(PdfProfile.Accessibility)
    .Ignore(PdfDetail.Language));
```

The snapshot's header lists the sections and fields a profile leaves out, not the profile's name. If a later release
changes what a profile leaves out, the header changes too, so the test fails and says so instead of quietly checking
less.

[Every profile](../reference/options.md#pdf-profiles), and what it leaves out, is listed in the reference.

## Set options for the whole suite

A suite whose tests share options, such as a profile or scrubbers, can set them once beside the folder, with
`Snapshot.DefaultPdfOptions`, `Snapshot.DefaultDocxOptions` or `Snapshot.DefaultTextOptions` for text and JSON. Each call's
own `configure` runs after them and adds to them. A PDF or Word call can leave out more but can't bring back what the
defaults leave out (its `Strict` profile doesn't undo a default one), so keep the defaults to what
every test agrees on and set the rest in each test. A text or JSON call's `KeepDates()` or `KeepGuids()` does undo a
default scrub. A PDF or Word snapshot's first lines list what's ignored, but not what's scrubbed.

```csharp
Snapshot.DefaultPdfOptions = options => options.Use(PdfProfile.Accessibility).ScrubDates();
```

Set them in a module initializer, as for [`Snapshot.Directory`](getting-started.md#keep-snapshots-in-one-folder).

## Leave out sections

Pass the sections to leave out. They print as `(ignored)` instead of their contents.

```csharp
[Fact]
public async Task TrailMapTemplate_ShouldKeepItsTagStructure_WhenStylingChanges()
{
    using Stream pdf = await ReportGenerator.CreateTrailMapAsync();

    // This test guards the tag tree and the text, so leave out the metadata, the document settings
    // and the layout (positions, fonts and colors), which the design team changes freely.
    await Snapshot.VerifyPdfAsync(pdf, configure: options => options
        .Ignore(PdfSection.Metadata, PdfSection.Settings, PdfSection.Layout));
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

[Every `PdfSection`](../reference/options.md#pdf-sections) is listed in the reference.

## Leave out one detail everywhere

For something narrower than a section, ignore a [`PdfDetail`](xref:PDiffy.PdfDetail). It's left out everywhere it appears, and
the rest of each line is still compared.

```csharp
[Fact]
public async Task SeasonalGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateSeasonalGuideAsync(month: 6);

    await Snapshot.VerifyPdfAsync(pdf, configure: options => options
        // The title includes the month, so it changes every time.
        .Ignore(PdfDetail.Title)
        // Text reflows when the guide has more rows, so where things land doesn't matter here.
        // Fonts, sizes and colors are still compared.
        .Ignore(PdfDetail.Positions));
}
```

With `PdfDetail.Positions` ignored, a layout line such as

```text
p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Trails"
```

becomes

```text
p1 text tagged font=Arial size=18 color=gray:0 "Trails"
```

[Every `PdfDetail`](../reference/options.md#pdf-details) is listed in the reference.

## Allow small numeric differences

Positions are rounded to whole points, and font sizes and line widths to 0.1 pt. A value close to a rounding boundary
can flip after a font or library update: text at 72.49 pt prints `at=72,...`, and moved by 0.02 pt it prints
`at=73,...`. Ignoring `PdfDetail.Positions` would also hide a heading that moved 40 pt, so tolerate the noise instead:

```csharp
await Snapshot.VerifyPdfAsync(pdf, configure: options => options
    .Tolerate(PdfTolerance.Positions, points: 1)
    .Tolerate(PdfTolerance.FontSizes, points: 0.5));
```

- A number that differs from the verified one by no more than its tolerance doesn't fail the test, and the verified
  file stays as it is, even in accept mode, so the baseline doesn't creep a point at a time.
- `Positions` covers where text, images, paths and annotations are, the size of images, paths and annotations, and
  the positions a link or bookmark opens its page at;
  `FontSizes` a text run's size; `LineWidths` a path's width. Other fields aren't numbers and can't be tolerated.
- The snapshot's header lists each tolerance (`# tolerances: positions 1pt, font-sizes 0.5pt`), so a reader knows its
  numbers are approximate. Comparing two snapshots, with `SnapshotDiff.CompareSnapshots` or `pdiffy`, applies the tolerances
  their header lists.

## Scrub values that change on every run

A generated document often prints today's date, a permit number or a GUID. Scrubbers replace those inside quoted text
with a stable placeholder, so the rest of the text is still compared.

```csharp
[Fact]
public async Task TrailPass_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailPassAsync();

    await Snapshot.VerifyPdfAsync(pdf, configure: options => options
        // "Printed 10/27/2026" becomes "Printed Date_1". The same text always gets the same number,
        // so the snapshot still shows that the print date and the issue date match.
        .ScrubDates()
        // Booking IDs become Guid_1, Guid_2 and so on.
        .ScrubGuids()
        // Anything else that changes, with your own pattern. "$1" keeps the first group.
        .ScrubText(new Regex(@"(Permit) #\d+"), "$1 #<number>"));
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
    await Snapshot.VerifyPdfAsync(pdf, configure: options => options.OnlyPages(3, 4));
}
```

- A structure element or bookmark stays when it, or anything under it, is on one of those pages.
- The page count, page labels and fonts still describe the whole file.
- Asking for a page past the end throws.

## Word documents

DOCX snapshots have their own sections and details, with the same scrubbers:

```csharp
await Snapshot.VerifyDocxAsync(docx, configure: options => options
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
- [`PdfSnapshotOptions`](xref:PDiffy.PdfSnapshotOptions), [`DocxSnapshotOptions`](xref:PDiffy.DocxSnapshotOptions),
  [`TextSnapshotOptions`](xref:PDiffy.TextSnapshotOptions)
