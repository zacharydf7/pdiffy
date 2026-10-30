# Command line

The `pdiffy` tool does outside of tests what the library does inside them: compare files, print snapshots, accept
changes and summarize a run.

Install it as a local tool, at the same version as the PDiffy package your tests use, so `pdiffy snapshot` reads
documents exactly as your tests do. The manifest is checked in, and `dotnet tool restore` installs that version
anywhere; run it as `dotnet pdiffy`. A global install (`dotnet tool install --global PDiffy.Cli`) works too, but can
drift from the package.

```text
dotnet new tool-manifest
dotnet tool install PDiffy.Cli --version <the PDiffy package version>
```

`pdiffy --version` prints the version and the snapshot formats it writes, such as
`pdiffy 0.4.0 (pdfua-snapshot v1, docx-snapshot v1)`, and `pdiffy --help` (or `-h`) prints the commands. Both
exit with `0`.

| Command | Does |
|---|---|
| [`pdiffy <before> <after>`](#compare) | Compares two PDFs, two DOCX files or two snapshot files |
| [`pdiffy compare <before> <after>`](#compare-folders) | Compares two folders of documents, file by file |
| [`pdiffy snapshot <file>`](#snapshot) | Prints or saves a PDF's or DOCX's snapshot |
| [`pdiffy check <file.pdf>`](#check) | Runs every built-in check on a PDF |
| [`pdiffy accept [targets]`](#accept) | Accepts received snapshots as verified |
| [`pdiffy report [targets]`](#report) | Summarizes received snapshots, for review or a CI job page |
| [`pdiffy coverage [targets]`](#coverage) | Counts what the verified snapshots compare and leave out |
| [`pdiffy unused [folders] --run-log <folder>`](#unused) | Lists or deletes snapshots no test used in a run |
| `pdiffy --version` | Prints the version and the snapshot formats it writes |
| `pdiffy --help`, `-h` | Prints the commands |

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Nothing changed (comparing files or folders), nothing was found (check), nothing is waiting to be accepted (report), every snapshot was used or `--delete` removed the rest (unused), or the command succeeded |
| `1` | Something changed (comparing files or folders), something was found (check), received snapshots are waiting (report), or some snapshots weren't used (unused) |
| `2` | The arguments don't fit a command, or a file or folder is missing or can't be read. The problem is written to standard error. When comparing folders, a file that can't be read is listed as `unreadable` instead, and counts as a change. |

## Compare

```text
pdiffy <before> <after> [--html <report.html>] [--json [<changes.json>]] [--profile <profile>]
```

Prints what changed between two files, the same summary a failing test shows, and exits with `0` when nothing did and
`1` when something did.

| Option | Does |
|---|---|
| `--html <file>` | Also writes the side-by-side HTML report |
| `--json` | Prints only the changes as [JSON](../guide/comparing.md#json-output), instead of the summary, so a script can read them from the pipe |
| `--json <file>` | Saves the JSON to a file, and prints the summary as usual |
| `--profile <profile>` | Leaves out what a [profile](options.md#pdf-profiles) leaves out: `content`, `accessibility`, `layout` or `strict` (the default). A Word document uses the [Word profile](options.md#word-profiles) of the same name, or `strict` when there's none. A snapshot file is compared as it is |

Each input can be:

- **A PDF**, recognized by its `%PDF-` header, whatever its extension.
- **A DOCX file**, recognized by its `.docx` extension.
- **A snapshot file**, or any other text, compared line by line when it isn't a PDiffy snapshot.

```text
pdiffy guide-v1.pdf guide-v2.pdf
pdiffy guide-v1.pdf guide-v2.pdf --html report.html
pdiffy guide-v1.pdf guide-v2.pdf --json > changes.json
pdiffy TrailMap.verified.pdfua.txt TrailMap.received.pdfua.txt
pdiffy guide-v1.pdf guide-v2.pdf --profile content
```

## Compare folders

```text
pdiffy compare <before> <after> [--html <index.html>] [--json [<changes.json>]] [--profile <profile>]
```

Compares two folders, such as the documents two releases generate, file by file. Files are paired by their path inside
each folder, including subfolders. PDFs and DOCX files are compared as documents, and `.txt` and `.json` files as
snapshots; other files are counted but not compared.

```text
4 file(s) differ: 1 changed, 1 added, 1 removed, 1 unreadable; 12 unchanged, 3 not compared.
changed maps/ridgeline.pdf: 2: 2 text; by section: structure 1, layout 1
added guides/welcome.docx
removed guides/old.pdf
unreadable broken.pdf: <why it couldn't be read>
```

Files that differ are listed changed first, then added, removed and unreadable. Unchanged files are counted, not
listed. A file that can't be read is listed as `unreadable` and counts as a change, so one damaged document doesn't stop
the rest being compared.

| Option | Does |
|---|---|
| `--html <file>` | Saves a page with one row per file that differs, and next to it each changed file's report, at the file's path with `.diff.html` added |
| `--json` | Prints only the result as JSON, instead of the summary |
| `--json <file>` | Saves the JSON to a file, and prints the summary as usual |
| `--profile <profile>` | Leaves out what the profile leaves out, as when comparing two files |

The JSON has one entry per file that differs, each with its `path`, its `status` and either its `changes`, in the same
shape as [comparing two files](../guide/comparing.md#json-output), or the `problem` that stopped it being read:

```json
{
  "version": 1,
  "unchanged": 12,
  "notCompared": 3,
  "files": [
    { "path": "maps/ridgeline.pdf", "status": "changed", "changes": [ ... ] },
    { "path": "guides/welcome.docx", "status": "added" }
  ]
}
```

```text
pdiffy compare release-1.4/out release-1.5/out
pdiffy compare baseline current --profile content --html compare/index.html
```

## Snapshot

```text
pdiffy snapshot <file.pdf|file.docx> [<out>]
```

Prints the file's snapshot, or saves it to `<out>`. Useful for seeing what PDiffy reads from a document.

```text
pdiffy snapshot guide.pdf
pdiffy snapshot guide.pdf guide.pdfua.txt
```

## Check

```text
pdiffy check <file.pdf>
```

Runs every [built-in check](../guide/checking-documents.md#built-in-checks) on a PDF, prints each finding, and exits with
`0` when there are none and `1` when there are, so a pipeline can gate on a generated document without writing a test.

```text
3 finding(s):
DocumentTitle: The document has no title (metadata)
FigureAltText: Figure on p2 has no alt text (structure > Document > Figure[2])
FontsEmbedded: Font Helvetica isn't embedded (fonts > Helvetica)
```

## Accept

```text
pdiffy accept [<files or folders>] [--all]
```

Replaces each received snapshot's verified file with it, keeping its extension (`Guide.received.json` becomes
`Guide.verified.json`), and deletes its report, as a test does in accept mode.

| Argument | Does |
|---|---|
| none | Accepts the received snapshots in the current folder |
| `<folder>` | Accepts the received snapshots in that folder |
| `<file>` | Accepts that received file. Shells expand `*.received.*` into many files, and each is accepted once |
| `--all` | Includes every folder under each folder |

```text
pdiffy accept tests/Snapshots
pdiffy accept --all
```

> [!TIP]
> Run `pdiffy report --all` first to see what you're about to accept.

## Report

```text
pdiffy report [<files or folders>] [--all] [--html <summary.html>] [--markdown <summary.md>]
```

Lists every received snapshot, new or changed, with its changes by kind and by section and how many can affect
accessibility. Exits with `0` when there are none and `1` when there are, so a pipeline step can use it. Finds files
the same way as `accept`.

| Option | Does |
|---|---|
| `--html <file>` | Saves a page with one row per snapshot, linking each one's report |
| `--markdown <file>` | Adds the table to the end of the file, such as GitHub's `$GITHUB_STEP_SUMMARY` |

```text
pdiffy report --all
pdiffy report --all --markdown "$GITHUB_STEP_SUMMARY" --html snapshot-summary.html
```

The printed summary looks like this:

```text
2 received snapshot(s): 1 changed, 1 new
changed tests/Snapshots/TrailMap.received.pdfua.txt: 3: 1 moved, 1 attribute, 1 text; by section: structure 3; 2 can affect accessibility
new tests/Snapshots/Guide.received.docx.txt
```

See [Continuous integration](../guide/ci.md) for complete pipeline examples.

## Coverage

```text
pdiffy coverage [<files or folders>] [--all] [--markdown <coverage.md>]
```

Every snapshot's header says what it leaves out. `coverage` adds those headers up across the verified snapshots, so a
suite that has drifted into ignoring layout everywhere, or fonts in half its tests, shows it. It reads only the headers,
so it's fast and needs no documents.

| Option | Does |
|---|---|
| `<files or folders>` | Which verified snapshots to count. The current folder by default |
| `--all` | Includes every folder under each folder |
| `--markdown <file>` | Adds the counts, as tables, to the end of the file, such as GitHub's `$GITHUB_STEP_SUMMARY` |

```text
42 PDF snapshot(s)
  document     42 of 42
  ...
  layout        6 of 42, ignored in 36
  fields ignored: positions in 30, colors in 12
  limited to some pages: 3
  with tolerances: 2
5 Word snapshot(s)
  ...
```

Each section shows how many snapshots compare it, and the fields line lists every field some snapshot leaves out, most
often first. Text and JSON snapshots have no sections, so they're only counted. `coverage` exits with `0`, or with `2`
when a target is missing or isn't a verified snapshot.

## Unused

```text
pdiffy unused [<folders>] --run-log <folder> [--all] [--delete]
```

A renamed or deleted test leaves its `.verified.*` file behind, and nothing reads it any more. To find such files, set
`PDIFFY_RUN_LOG` (`Snapshot.RunLogVariable`) to an empty folder and run every test: each snapshot call writes down the
verified file it uses, in one log per test process. `unused` then lists the verified and received snapshots, and their
reports, that no call used. Give the folder as a full path, since each test process resolves a relative one from its
own folder.

```bash
PDIFFY_RUN_LOG="$PWD/artifacts/run-log" dotnet test
pdiffy unused tests --all --run-log artifacts/run-log
```

| Option | Does |
|---|---|
| `<folders>` | Which folders to look in. The current folder by default |
| `--all` | Includes every folder under each folder |
| `--run-log <folder>` | The folder `PDIFFY_RUN_LOG` named for the test run |
| `--delete` | Deletes the files it lists |

A test that didn't run, or failed before its snapshot call, looks unused, so run the whole suite, without a filter and
with every test passing, before deleting anything, and start each run with an empty log folder. Every `.verified.`,
`.received.` and `.diff.html` file in the folders counts, so give the snapshot folders rather than a folder that also
holds other tools' files or saved reports. `unused` exits with `0` when every snapshot was used or `--delete` removed the
rest, with `1` when it lists some, and with `2` when the run log folder has no logs.
