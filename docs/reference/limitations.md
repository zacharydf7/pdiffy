# Limitations and guarantees

What you can rely on PDiffy for, and what it doesn't do.

## Guarantees

| PDiffy promises | How it's kept |
|---|---|
| The same document gives the same snapshot on Windows and Linux | Every change is tested on both in CI |
| Encoding-only differences don't change a snapshot: object numbers, compression, object order, cross-reference streams, incremental updates, font subset prefixes, zip order and relationship ids | Equivalence tests write each document both ways and compare the snapshots |
| A small meaningful change does change a snapshot: an edited word, removed alt text, a moved element, a changed color | Mutation tests change one thing at a time and expect the snapshot to change |
| A damaged part is marked, not dropped | A PDF page or attachment that can't be read prints as `unreadable`, as does a Word part. In a Word document, content PDiffy doesn't read yet, in a paragraph or between paragraphs, tables, rows and cells, prints as `unsupported <element> hash=...`, so a change to it still fails the test. Settings and style properties it doesn't read aren't recorded |
| A change to what a snapshot prints is announced | See [Format changes](format-changes.md): a new version number when nearly every snapshot changes, and a changelog entry either way |

## What PDiffy doesn't do

- **It isn't a PDF/UA or WCAG validator.** It records what a document says and shows what changed. To check conformance, use [veraPDF](https://verapdf.org/) or PAC. The accessibility flags mark changes that can affect assistive technology; they don't certify a document.
- **It compares content, not appearance.** A substituted font that looks different but extracts the same text, a clipping path that hides text, or an image drawn over text doesn't change the text a snapshot records. Layout records positions, sizes, fonts and colors, not pixels.
- **It can't read text that isn't text.** Text drawn as vector paths or embedded in an image has no characters to extract; it shows only as a path or an image in the layout.
- **Gradients are recorded only by kind.** A shading-pattern fill prints as `pattern:shading`, so changing a gradient's colors doesn't change the snapshot, and a fill drawn with the `sh` operator isn't recorded at all.
- **It reads what the file declares.** When a font's character codes don't map to Unicode, the extracted text may be wrong; the `fonts` section lists such codes as `unmapped` so you can see it.
- **Word documents are read from the file, not laid out.** There are no page numbers or positions for DOCX, and fields such as PAGE record the result Word last computed, which `DocxDetail.FieldResults` can leave out.

## Compatibility

| | Supported |
|---|---|
| .NET | .NET 10 |
| Operating systems | Windows and Linux, tested in CI. PDiffy is fully managed, with no native binaries. |
| Formats | PDF (any version PdfPig reads, tagged or not) and DOCX (Word 2007 and later) |
| Snapshot formats | `pdfua-snapshot v1`, `docx-snapshot v1` |
| Test frameworks | Any: a failure is a `SnapshotException` |
