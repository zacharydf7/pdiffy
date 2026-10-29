# Word documents

[`DocxSnapshot.VerifyAsync`](xref:PDiffy.DocxSnapshot.VerifyAsync*) snapshots DOCX files the same way, with the same
received and verified files, accept mode and report. It reads the file directly: no Word, Office or Open XML SDK needed.

```csharp
[Fact]
public async Task Letter_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream docx = await LetterGenerator.CreateAsync();

    await DocxSnapshot.VerifyAsync(docx);
}
```

Snapshots are saved as `{name}.verified.docx.txt`. The `name`, `configure` and `directory` parameters work as for
[PDF snapshots](pdf-snapshots.md#naming-and-folders).

## What a snapshot looks like

A DOCX snapshot reads like the document: one line per paragraph with its runs merged, and only what differs from its
style listed.

```text
# docx-snapshot v1
document
  title="Cheese list" lang=en-US compat=15 track-changes=no protection=none even-odd-headers=no created=<present> modified=<present>
sections
  s1 letter margins=72,72,72,72 header=36 footer=36 columns=1 break=nextPage headers=default
styles
  ListParagraph paragraph "List Paragraph" font="Arial" size=12 indent-left=36
  TableGrid table "Table Grid"
numbering
  list 1 definition=3
    level 0 format=upperLetter text="%1." start=1 indent=36 hanging=18
body
  P style=ListParagraph list=1:0 label="A." "Cheese has a long history"
    span "long" bold
  Table style=TableGrid widths=144,216
    Row header
      Cell
        P "Drug"
      Cell
        P "Interacts with"
  P "Visit example.com"
    link "example.com" -> https://example.com at=6
  P
    image inline size=120x40 alt=(missing) type=png hash=3f9c0a1b2d4e5f60 at=0
headers
  s1 default
    P "Page 3"
      field "PAGE" result="3" at=5
footers
  (none)
notes
  (none)
```

The [Word snapshot format](../reference/docx-snapshot-format.md) reference lists every section and line.

## What's recorded

- Document settings, each section's page setup, and headers and footers.
- The styles in use, with their properties resolved through the styles they're based on. Styles nothing uses are left
  out, since they can't change the document.
- List labels as Word numbers them.
- Paragraph layout and text formatting.
- Tables: grid, header rows, merged cells, shading, widths, margins and borders, and content controls around rows and
  cells.
- Images and charts with their alt text and a hash of the image; text boxes with their paragraphs; equations;
  embedded objects and ActiveX controls.
- Links, bookmarks, symbols, fields with their results, and form controls.
- Tracked changes to text, paragraphs and their marks, tables, rows, cells and section settings.
- Footnotes, endnotes, comments, and inserted documents.

Relationship ids, editing-session ids and the zip's layout are left out, so regenerating the same document gives the
same snapshot.

Anything PDiffy doesn't read yet, such as a phonetic guide (`w:ruby`), prints as `unsupported <element> hash=...`, so a
change to it still fails the test.

## Leave out what changes

```csharp
await DocxSnapshot.VerifyAsync(docx, configure: options => options
    // A PAGE field's cached result depends on where Word last laid out the page.
    .Ignore(DocxDetail.FieldResults)
    // The template's styles are tested elsewhere.
    .Ignore(DocxSection.Styles)
    .ScrubDates());
```

See [Ignore and scrub](options.md#word-documents) for every option.

## Assert on the model

[`DocxDocument.Load`](xref:PDiffy.DocxDocument.Load*) gives the same content as properties:

```csharp
DocxDocument letter = DocxDocument.Load("letter.docx");

// Every image has alt text.
Assert.All(letter.Images(), image => Assert.NotNull(image.AltText));

// No tracked changes shipped.
Assert.Empty(letter.Revisions());

// The first paragraph uses the Title style.
Assert.Equal("Title", letter.Paragraphs().First().Style);
```

| Member | Gives |
|---|---|
| [`Paragraphs()`](xref:PDiffy.DocxDocument.Paragraphs) | Every paragraph in the body, including those in tables, content controls, text boxes and inserted documents |
| [`Images()`](xref:PDiffy.DocxDocument.Images) | Every image, chart and shape in the body, headers, footers and notes |
| [`Revisions()`](xref:PDiffy.DocxDocument.Revisions) | Every tracked change, in reading order |
| [`Body`](xref:PDiffy.DocxDocument.Body), [`Headers`](xref:PDiffy.DocxDocument.Headers), [`Footers`](xref:PDiffy.DocxDocument.Footers), [`Notes`](xref:PDiffy.DocxDocument.Notes) | The content, as blocks |
| [`Styles`](xref:PDiffy.DocxDocument.Styles), [`Lists`](xref:PDiffy.DocxDocument.Lists), [`PageSetups`](xref:PDiffy.DocxDocument.PageSetups) | Styles in use, lists and section setups |
| [`CompareTo(other)`](xref:PDiffy.DocxDocument.CompareTo*) | A [comparison](comparing.md) with another document |

## See also

- [Word snapshot format](../reference/docx-snapshot-format.md)
- [`DocxDocument`](xref:PDiffy.DocxDocument) in the API reference
