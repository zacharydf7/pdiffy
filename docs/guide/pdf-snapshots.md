# PDF snapshots

[`PdfSnapshot.VerifyAsync`](xref:PDiffy.PdfSnapshot.VerifyAsync*) is the core of PDiffy: one call prints the PDF's
snapshot and compares it with the approved `.verified.pdfua.txt` file.

```csharp
await PdfSnapshot.VerifyAsync(pdf);
```

| Parameter | Use it to | Default |
|---|---|---|
| `pdf` | Pass the PDF, as any readable stream. It's left open. | Required |
| `name` | Name the snapshot, when one test makes several | `{TestFile}.{TestMethod}` |
| `configure` | [Ignore or scrub](options.md) what the test doesn't care about | Everything is compared |
| `directory` | Keep this snapshot in another folder | [`Snapshot.Directory`](xref:PDiffy.Snapshot.Directory), else the test file's folder |

## What a snapshot looks like

A snapshot is plain text: one line per element, in a fixed order, the same on every OS. Here is a shortened one:

```text
# pdfua-snapshot v1
document
  marked=yes lang=en-US display-doc-title=yes pages=3
structure
  Document lang=en-US
    H1 p1 "Trail Table"
    Figure p1 alt="Park logo"
    Table p2-3 summary="Trail details"
      TR
        TH1 p2 scope=Column "Trail"
        TH2 p2 scope=Column "Length"
      TR
        TD p2 headers=[TH1] "Ridgeline"
        TD p2-3 headers=[TH2] "Cedars"
untagged
  p1 "Stray text"
fonts
  Arial TrueType embedded=yes to-unicode=yes
layout
  p1 text tagged at=72,720 font=Arial size=18 color=gray:0 "Trail Table"
  p1 image tagged at=72,600 size=120x40 pixels=600x200 hash=3f9c0a1b2d4e5f60
```

The [PDF snapshot format](../reference/pdf-snapshot-format.md) reference describes every section and key.

## Reading a failure

When the snapshot differs, the test throws a [`SnapshotException`](xref:PDiffy.SnapshotException) whose message
explains the change:

```text
The PDF's snapshot differs from the verified one.

3 change(s): 1 moved, 1 attribute, 1 text
by section: structure 3
2 can affect accessibility, listed first
↕ moved structure > Document > Figure (p1) [accessibility]
    reading position 3 -> 10
~ attribute structure > Document > Table > TR[1] > TH2 (p2) [accessibility]
    scope: Column -> (missing)
~ text structure > Document > Table > TR[2] > TD[2] (p2)
    "Go [-with-] {+without+} a guide"

verified: Snapshots/TrailGuideTests.TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated.verified.pdfua.txt
received: Snapshots/TrailGuideTests.TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated.received.pdfua.txt
report: Snapshots/TrailGuideTests.TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated.diff.html
To accept: set PDIFFY_ACCEPT=1 and run the test again, or copy the received file over the verified one.
```

- **The counts** come first: by kind, then by section, so a reflow that moves 200 layout lines doesn't hide one change
  to the structure. The message lists up to 20 changes; the report lists them all.
- **Accessibility changes come first**, tagged `[accessibility]`. See
  [Changes that affect accessibility](../reference/accessibility-changes.md) for the rules.
- **Each change** has a symbol, a kind, a path to the element and its page, then the detail.

| Symbol | Kind | Example detail |
|---|---|---|
| `+` | Added | The new line |
| `-` | Removed | The old line |
| `↕` | Moved | `reading position 3 -> 10` |
| `~` | Attribute | `scope: Column -> (missing)` |
| `~` | Text | `"Go [-with-] {+without+} a guide"`: removed words in `[- -]`, added in `{+ +}` |
| `~` | Changed | `type: H2 -> H3`, or a changed page range |

Elements are matched one to one, so:

- An edited paragraph is one change, not a removed line and an added one.
- An element that moved in reading order is reported as moved, along with anything that changed on it.
- A heading that became another level or a paragraph is one change: `type: H2 -> P`.
- A one-word value replaced on an otherwise unchanged line, such as a number in a table cell, is changed, not removed
  and added. A paragraph replaced by entirely different words is removed and added.
- Inserted or removed pages are one change, such as `p2 inserted; p2-10 -> p3-11`. Pages are lined up by their text,
  and each element is compared with the page it lines up with, so a new cover page doesn't report every later element.

## The HTML report

A failing test also writes `{name}.diff.html` next to the received file. It's one self-contained page that opens
straight from a build artifact. Open the [sample report](../samples/TrailGuide.diff.html)
to see one, built from two versions of a small trail guide. It has:

- The list of changes, accessibility changes first, filterable by kind or to only those that affect accessibility.
- Links to each page that changed.
- Each section side by side, the verified snapshot on the left and the received one on the right, with edited lines
  paired and the changed words highlighted. Long runs of unchanged lines fold away.
- A map of each page with a layout change, drawn from the layout lines: unchanged things in gray, a dashed outline
  where something was and a filled box where it is now. Hover over a box to see its line. It's a sketch of positions, not a
  picture of the page, so it needs no renderer and works from the snapshots alone.

[`SnapshotDiff.ToHtml`](xref:PDiffy.SnapshotDiff.ToHtml) gives the same page for any comparison.

## Naming and folders

One snapshot per test is named after the test file and method. When a test makes several, as a theory does, name each:

```csharp
[Theory]
[InlineData("ridgeline")]
[InlineData("lakeshore")]
public async Task TrailMap_ShouldMatchApprovedSnapshot_WhenGeneratedForATrail(string trail)
{
    using Stream pdf = await ReportGenerator.CreateTrailMapAsync(trail);

    await PdfSnapshot.VerifyAsync(pdf, $"TrailMap.{trail}");
}
```

Each snapshot has up to three files:

- `{name}.verified.pdfua.txt` is the approved snapshot, written when you accept one. Commit it.
- `{name}.received.pdfua.txt` is written when the test fails. Don't commit it.
- `{name}.diff.html` is the report, written when the test fails and a verified file exists. Don't commit it.

To keep every snapshot in one folder, set [`Snapshot.Directory`](xref:PDiffy.Snapshot.Directory) once (see
[Getting started](getting-started.md#keep-snapshots-in-one-folder)). To place one snapshot elsewhere, pass
`directory:`.

## Accepting changes

After reviewing a failure, accept the new snapshot:

- `pdiffy accept --all` replaces every verified file with its received one under the current folder
  ([command line](../reference/command-line.md#accept)).
- `PDIFFY_ACCEPT=1 dotnet test` makes every failing snapshot pass and overwrite its verified file. It's ignored when
  `CI` or `TF_BUILD` is set.
- Or copy the received file over the verified one.

## Damaged files

A page that can't be loaded (for example, one with a damaged font) comes back marked unreadable, and its snapshot line
says so, instead of the whole read failing. The same goes for an attachment whose contents can't be decoded, and for XMP
metadata that isn't well-formed XML or has a DTD (`xmp=unreadable`). A file that can't be opened at all throws.

PDiffy also reads structure elements and bookmarks only 256 levels deep, so a file built to overflow the stack can't
crash the test run. An element whose children are deeper gets `unread-children=N`, and their content counts as untagged.
Bookmarks nested deeper, or that loop back on themselves, print `unreadable`. A Flate-compressed stream that would
decompress to more than 256 MB isn't decompressed: a page whose content it is comes back unreadable, an image is hashed
as stored, and XMP prints `xmp=unreadable`. See [Limitations](../reference/limitations.md) for what PDiffy can't guard
against.

## See also

- [Ignore and scrub](options.md)
- [PDF snapshot format](../reference/pdf-snapshot-format.md)
- [`PdfUaDocument`](xref:PDiffy.PdfUaDocument), the model behind the snapshot
