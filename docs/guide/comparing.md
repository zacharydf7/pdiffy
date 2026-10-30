# Compare two documents

`CompareTo` gives the same element-by-element comparison as a failing snapshot test, for any two documents and without
snapshot files. It suits one-off checks such as before and after a library upgrade, or a customer's file against
yours.

```csharp
PdfUaDocument before = PdfUaDocument.Load("guide-v1.pdf");
PdfUaDocument after = PdfUaDocument.Load("guide-v2.pdf");

// The same options as a snapshot: here, only the tag structure and text matter.
SnapshotDiff diff = before.CompareTo(after, options => options.Ignore(PdfSection.Layout, PdfSection.Metadata));
```

[`SnapshotDiff.Compare(before, after)`](xref:PDiffy.SnapshotDiff.Compare*) does the same with the earlier document
first, and works for Word documents too, as does [`DocxDocument.CompareTo`](xref:PDiffy.DocxDocument.CompareTo*).
[`SnapshotDiff.CompareSnapshots`](xref:PDiffy.SnapshotDiff.CompareSnapshots*) compares two snapshot texts, such as the
contents of two `.verified.pdfua.txt` files, and [`SnapshotDiff.CompareFiles`](xref:PDiffy.SnapshotDiff.CompareFiles*)
compares two files the way `pdiffy before after` does, reading a PDF or Word document and taking anything else as a
snapshot.

## Assert on the changes

A [`SnapshotDiff`](xref:PDiffy.SnapshotDiff) lists its [`Changes`](xref:PDiffy.SnapshotDiff.Changes). Each
[`SnapshotChange`](xref:PDiffy.SnapshotChange) has:

| Property | Holds | Example |
|---|---|---|
| [`Kind`](xref:PDiffy.SnapshotChange.Kind) | A [`ChangeKind`](xref:PDiffy.ChangeKind): `Added`, `Removed`, `Moved`, `AttributeChanged`, `TextChanged` or `Changed` | `TextChanged` |
| [`SectionName`](xref:PDiffy.SnapshotChange.SectionName) | The section as the snapshot prints it, as in the [JSON output](#json-output); null for text without sections | `"structure"` |
| [`PdfSection`](xref:PDiffy.SnapshotChange.PdfSection) | The [`PdfSection`](xref:PDiffy.PdfSection) it's in; null outside PDF snapshots, and for the snapshot's first lines and whitespace-only changes | `PdfSection.Structure` |
| [`DocxSection`](xref:PDiffy.SnapshotChange.DocxSection) | The [`DocxSection`](xref:PDiffy.DocxSection) it's in; null outside Word snapshots, and for the snapshot's first lines and whitespace-only changes | `DocxSection.Body` |
| [`Path`](xref:PDiffy.SnapshotChange.Path) | Where it is | `structure > Document > Table > TR[2] > TD[1]` |
| [`Page`](xref:PDiffy.SnapshotChange.Page) | The page, when it's on one | `2` |
| [`Detail`](xref:PDiffy.SnapshotChange.Detail) | What changed | `"Go [-with-] {+without+} a guide"` |
| [`AffectsAccessibility`](xref:PDiffy.SnapshotChange.AffectsAccessibility) | Whether it can change what a screen reader gets; null for Word and text, which PDiffy doesn't assess | `true` |

```csharp
// No element changed place in reading order.
Assert.DoesNotContain(diff.Changes, change => change.Kind == ChangeKind.Moved);

// Any change is fine except one that can affect what a screen reader gets.
Assert.DoesNotContain(diff.Changes, change => change.AffectsAccessibility == true);

// Nothing changed at all.
Assert.True(diff.IsEmpty);
```

> [!TIP]
> To fail a test only on accessibility changes, compare with `CompareTo` and assert on `AffectsAccessibility`. A
> snapshot test always fails on any change; the flag only orders and marks the explanation.

### New and fixed problems

When both snapshots have a [`checks` section](checking-documents.md#approve-findings-with-the-snapshot), its changes
read as progress or regression rather than as lines added and removed. A finding only the later snapshot has is a new
problem, an `Added` change in the `checks` section whose path is the rule and whose detail is the finding's path; one
only the earlier snapshot has is fixed, a `Removed` change. A new problem from an accessibility rule, not a house rule,
can affect accessibility, so it's listed first:

```text
2 change(s): 1 new problem, 1 fixed
by section: checks 2
1 can affect accessibility, listed first
+ new problem FigureAltText (p4) [accessibility]
    structure > Document > Figure[3]
- fixed TableHeaders (p3)
    structure > Document > Table > TR > TH[2] > scope
```

A fixed problem still fails the test until the snapshot is accepted, so it can't come back unnoticed. When an upgrade
adds rules to the set, the summary says how many, so new problems can be told apart from changes to the document.
Leaving the section out with `Ignore(PdfSection.Checks)` reads each finding the earlier snapshot recorded as fixed,
next to the header change that says the section is now ignored.

## Print or save the result

| Method | Gives |
|---|---|
| [`Describe(maxChanges)`](xref:PDiffy.SnapshotDiff.Describe*) | The summary a failing test shows: counts by kind and section, then up to `maxChanges` changes, accessibility ones first, with layout lines that moved together shown as one shift |
| [`ToHtml()`](xref:PDiffy.SnapshotDiff.ToHtml) | The side-by-side HTML report |
| [`ToJson()`](xref:PDiffy.SnapshotDiff.ToJson) | The changes as JSON, for scripts and tools outside .NET |

```csharp
Console.WriteLine(diff.Describe());
File.WriteAllText("report.html", diff.ToHtml());
File.WriteAllText("changes.json", diff.ToJson());
```

## JSON output

`ToJson()`, or `pdiffy before.pdf after.pdf --json` on the [command line](../reference/command-line.md#compare), writes
a small, versioned shape:

```json
{
  "version": 2,
  "changes": [
    {
      "kind": "Changed",
      "section": "structure",
      "path": "structure > Document > P[1]",
      "page": 1,
      "detail": "type: H1 -> P",
      "affectsAccessibility": true
    }
  ]
}
```

| Field | Holds |
|---|---|
| `version` | Goes up only when a field is renamed, removed or changes meaning. New fields can be added without it. |
| `kind` | A `ChangeKind` name |
| `section` | The section as the snapshot prints it, such as `structure` or `body`; `header` for the snapshot's first lines, `whitespace` when only blank lines or line endings differ, and `null` for text without sections, such as JSON |
| `path` | Where the change is |
| `page` | The page, or `null` when the change isn't on a page |
| `detail` | What changed |
| `affectsAccessibility` | Whether it can change what a screen reader gets, or `null` for Word and text snapshots |

## See also

- [Changes that affect accessibility](../reference/accessibility-changes.md)
- [Command line](../reference/command-line.md#compare): compare two files without writing code
