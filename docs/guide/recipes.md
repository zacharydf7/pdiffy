# Recipes for document generators

Three common setups, each with a first test, how to keep its output the same from run to run, and which profile to
start with. Whatever writes your documents, the first snapshot is worth reading through before you accept it: it is
the baseline every later change is measured against.

## HTML to PDF with Playwright or Chromium

Chromium prints a page to PDF, and with tagging turned on it writes the structure tree from the HTML: headings,
paragraphs, lists, tables and images with their `alt` text. Playwright 1.42 and later take `Tagged` and `Outline` for
`PdfAsync`:

```csharp
[Fact]
public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenPrintedFromHtml()
{
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync();
    var page = await browser.NewPageAsync();
    await page.SetContentAsync(TrailGuideHtml);
    await page.EvaluateAsync("document.fonts.ready");

    var pdf = await page.PdfAsync(new PagePdfOptions { Format = "Letter", Tagged = true, Outline = true });

    await PdfSnapshot.VerifyAsync(pdf);
}
```

`Outline = true` turns the headings into bookmarks. From the command line, headless Chrome takes `--export-tagged-pdf`
for the tags and `--generate-pdf-document-outline` for the bookmarks, with `--print-to-pdf=guide.pdf`.

To keep the output the same from run to run:

- **Fonts.** Load web fonts from files in your project with `@font-face`, and wait for `document.fonts.ready` before
  printing. A font the machine doesn't have is replaced by another, which changes every line that uses it; see
  [Troubleshooting](troubleshooting.md).
- **Dates and ids.** A date printed in the page, such as in a footer template, changes every day. Use `ScrubDates()`,
  or pass a fixed date into the page in tests. The PDF's own creation and modification dates don't matter: the snapshot
  records only whether they're present.
- **The browser.** Each Playwright version pins its own Chromium build, so upgrading Playwright is a
  [generator upgrade](generator-upgrades.md): run the tests before and after.

Start with `Use(SnapshotProfile.Accessibility)` if the tags are what you want to guard, or `SnapshotProfile.Content` for
the text and structure alone. A Chromium upgrade can move lines by a point or two, so leave layout in (`Strict`) only
where the page's look is what you're testing, and pin the browser there.

In the first snapshot, check the `structure` section: each HTML heading, paragraph, list and table should be there as
an element. Text in the `untagged` section usually means tagging is off.

## PDFsharp

PDFsharp 6 tags content through its `UAManager`. Content drawn between `BeginElement` and `End` is tagged, content
between `BeginArtifact` and `End` is an artifact, and anything else is untagged:

```csharp
[Fact]
public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenDrawnWithPdfSharp()
{
    var document = new PdfDocument();
    document.Info.Title = "Trail Guide";
    var accessibility = UAManager.ForDocument(document);
    accessibility.SetDocumentLanguage("en-US");
    var tags = accessibility.StructureBuilder;
    var page = XGraphics.FromPdfPage(document.AddPage());

    tags.BeginElement(PdfBlockLevelElementTag.Heading1);
    page.DrawString("Trail Guide", new XFont("Liberation Sans", 20), XBrushes.Black, 72, 72);
    tags.End();
    tags.BeginArtifact();
    page.DrawString("Page 1", new XFont("Liberation Sans", 9), XBrushes.Black, 72, 760);
    tags.End();

    using var pdf = new MemoryStream();
    document.Save(pdf);

    await PdfSnapshot.VerifyAsync(pdf);
}
```

PDFsharp finds no fonts by itself on Linux, so set `GlobalFontSettings.FontResolver` to a resolver that returns font
files from your project. Every machine then draws with the same font, and the same document gives the same snapshot,
layout included, so `Strict` is a good start. MigraDoc draws through PDFsharp, so the same font resolver applies.

Text that shows up in the first snapshot's `untagged` section was drawn outside any element. `PdfCheck.UntaggedContent`
finds it too; see [Check documents directly](checking-documents.md).

## Word with the Open XML SDK

A document written with the Open XML SDK can be snapshotted straight from its stream:

```csharp
[Fact]
public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenWrittenWithOpenXml()
{
    using var docx = new MemoryStream();
    using (var word = WordprocessingDocument.Create(docx, WordprocessingDocumentType.Document))
    {
        var main = word.AddMainDocumentPart();
        var heading = new ParagraphProperties(new ParagraphStyleId { Val = "Heading1" });
        main.Document = new Document(new Body(
            new Paragraph(heading, new Run(new Text("Trail Guide"))),
            new Paragraph(new Run(new Text("Start at the lodge.")))));
    }

    await DocxSnapshot.VerifyAsync(docx);
}
```

The DOCX snapshot doesn't print what changes from save to save without changing the document: editing-session ids
(`w:rsid...`), relationship ids, and the created and modified dates, which it records only as present. What's left to
keep steady is your own content: scrub dates and ids your code writes into the text with `ScrubDates()` and
`ScrubGuids()`.

Start with `DocxProfile.Content` to guard the text and its structure, or with no profile to include styles, fonts and
spacing. A document built on a template also snapshots the template's styles; leave them out with
`Ignore(DocxSection.Styles)` if the template is tested elsewhere. See [Word documents](word-documents.md).
