# Moving from Verify

Tests that snapshot PDFs, Word documents or JSON with Verify can move over one test class at a time. Most changes are
one line per test.

## Concept map

| Verify | PDiffy |
|---|---|
| `await Verify(pdfStream, "pdf")` | `await PdfSnapshot.VerifyAsync(pdfStream)` |
| `await Verify(docxStream, "docx")`, or `VerifyXml` of the document's parts | `await DocxSnapshot.VerifyAsync(docxStream)` |
| `await VerifyJson(json)` | `await Snapshot.VerifyJsonAsync(json)` |
| `await Verify(text)` | `await Snapshot.VerifyAsync(text)` |
| `.UseFileName("TrailMap")` | `name: "TrailMap"` |
| `DerivePathInfo(...)` or `UseDirectory(...)` | `Snapshot.Directory = "..."` once, or `directory:` on one call |
| `.UniqueForOSPlatform()` | Nothing: a snapshot is the same text on every OS, so one file covers them all |
| `.ScrubLinesWithReplace(...)` or a custom scrubber | `ScrubText(regex, replacement)` |
| `.ScrubInlineDateTimes(...)`, `.ScrubInlineGuids()` | `ScrubDates()`, `ScrubGuids()` |
| Accepting in a diff tool, or moving `.received.` files by hand | `PDIFFY_ACCEPT=1 dotnet test`, or `pdiffy accept` |

## 1. Seed the new snapshots from the approved files

There's no need to regenerate anything. The approved `.verified.pdf` or `.verified.bin` files are the documents your
tests last approved, so the `pdiffy` tool can turn each one into a PDiffy snapshot. Name the output after the test, as
PDiffy does: `{TestFile}.{TestMethod}.verified.pdfua.txt`, or `{name}.verified.pdfua.txt` when the test passes a name.

```text
pdiffy snapshot TrailMapTests.TrailMap_ShouldMatch.verified.pdf TrailMapTests.TrailMap_ShouldMatch.verified.pdfua.txt
pdiffy snapshot GuideTests.Guide_ShouldMatch.verified.docx GuideTests.Guide_ShouldMatch.verified.docx.txt
```

- A Word document saved as `.verified.bin` needs a `.docx` name first, since that's how the tool tells it from a PDF.
- With `UniqueForOSPlatform`, convert one OS's copy (`...Windows.verified.pdf`) and delete the others.
- Then delete the old files.

## 2. Approve what changes once

- **JSON:** PDiffy writes real JSON, indented two spaces with keys in the order written, to `.verified.json` instead of
  Verify's unquoted style in `.verified.txt`. GUIDs and ISO 8601 timestamps scrub to `Guid_1` and `Date_1` (Verify
  writes `DateTimeOffset_1` or `DateTime_1`). Run the tests once with `PDIFFY_ACCEPT=1`, check the new files and delete
  the old ones.
- **Text:** a `.verified.txt` carries over unchanged. PDiffy reads CRLF as LF and ignores a byte-order mark.

## 3. Update the repository

Add these lines to `.gitattributes`, so a snapshot has the same line endings on every OS:

```text
*.verified.* text eol=lf
*.received.* text eol=lf
```

Add `*.received.*` and `*.diff.html` to `.gitignore`, and to the files your CI keeps after a failed run (see
[Continuous integration](ci.md)).

## 4. Run both for a while, if you like

To build trust before deleting the Verify files, keep both assertions in each test for a few weeks:

- A failure only Verify reports is noise PDiffy leaves out on purpose (see
  [what's left out](../reference/pdf-snapshot-format.md#whats-left-out-on-purpose)).
- A failure only PDiffy reports is a real change Verify missed, or a PDiffy bug, which is worth
  [an issue](https://github.com/zacharydf7/pdiffy/issues).
