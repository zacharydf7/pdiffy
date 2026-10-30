# Troubleshooting

## "The test file's folder doesn't exist"

Your build maps source paths (deterministic builds, `ContinuousIntegrationBuild`), so the path the compiler records for
the test file, such as `/_/tests/...`, isn't on disk, and PDiffy couldn't find the folder in the test project either.
That happens when the project references PDiffy's source rather than its package, so PDiffy's build file didn't record
the project folder, or when the test file sits outside the project folder. Set
[`Snapshot.Directory`](xref:PDiffy.Snapshot.Directory) to the folder that holds your snapshots, such as
`Snapshot.ProjectFolder("Snapshots")`, for example in a module initializer. See
[Getting started](getting-started.md#keep-snapshots-in-one-folder).

## `PDIFFY_ACCEPT=1` doesn't accept anything

- Accept mode is ignored when `CI` or `TF_BUILD` is set. Check your shell doesn't set them, or use
  `pdiffy accept --all` instead.
- The value must be `1` or `true`.
- In PowerShell, set it with `$env:PDIFFY_ACCEPT=1` before `dotnet test`.

## A snapshot fails only on one OS

Snapshots are compared with LF line endings, and a CRLF verified file is read as LF, so line endings alone don't fail a
test. If Git converts files on checkout, add `*.verified.* text eol=lf` to `.gitattributes` so the files stay identical
in the repository too.

If the difference is real content, such as a font that's only installed on one machine, the snapshot is doing its job:
the documents really differ.

## Hundreds of layout and font changes on the build agent

A generator that can't find a font on the machine running it uses another, often DejaVu or Liberation on Linux. The
fonts section then loses one font and gains another, and every layout line that used it changes its `font=` and often
moves a little. When that's most of what changed, the failure starts with a hint such as "Arial was replaced by
DejaVuSans in 312 line(s)". Embed the font in the PDF, so it no longer depends on the machine, or install it on the
build agent.

## Every snapshot changed after upgrading PDiffy

A newer PDiffy can print some snapshots differently when it records something new or reads something more accurately.
Each such release lists what changed in [Format changes](../reference/format-changes.md), and when nearly every
snapshot changes, the format version goes up and a failing test says so first. Review the received files and accept
them with `pdiffy accept --all`.

## The test fails on a date, id or page number

Scrub it, or leave out the field it's in:

- Dates, GUIDs and your own patterns: [Scrub values](options.md#scrub-values-that-change-on-every-run).
- A Word field that stores its last result, such as PAGE or DATE: `Ignore(DocxDetail.FieldResults)`.
- The PDF's creation date is never printed, only whether it's present. The producer is printed only with
  `ShowProducerAndCreator()`.

## A page shows as unreadable

A page that can't be loaded, for example one with a damaged font, is marked unreadable in the snapshot instead of the
whole read failing. A file that can't be opened at all throws. Open the PDF in a validator to find the damage.

## `pdiffy accept` says a file isn't a received snapshot

`accept` only moves files whose names contain `.received.`. Pass a folder, or a received file, such as
`TrailMap.received.pdfua.txt`.

## Still stuck?

[Open an issue](https://github.com/zacharydf7/pdiffy/issues) with the snapshot lines you didn't expect.
