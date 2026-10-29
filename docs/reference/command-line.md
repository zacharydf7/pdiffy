# Command line

The `pdiffy` tool does outside of tests what the library does inside them: compare files, print snapshots, accept
changes and summarize a run.

```text
dotnet tool install --global PDiffy.Cli
```

| Command | Does |
|---|---|
| [`pdiffy <before> <after>`](#compare) | Compares two PDFs, two DOCX files or two snapshot files |
| [`pdiffy snapshot <file>`](#snapshot) | Prints or saves a PDF's or DOCX's snapshot |
| [`pdiffy accept [targets]`](#accept) | Accepts received snapshots as verified |
| [`pdiffy report [targets]`](#report) | Summarizes received snapshots, for review or a CI job page |

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Nothing changed (compare), nothing is waiting to be accepted (report), or the command succeeded |
| `1` | Something changed (compare), or received snapshots are waiting (report) |
| `2` | The arguments don't fit a command, or a file is missing or can't be read. The problem is written to standard error. |

## Compare

```text
pdiffy <before> <after> [--html <report.html>] [--json [<changes.json>]]
```

Prints what changed between two files, the same summary a failing test shows, and exits with `0` when nothing did and
`1` when something did.

| Option | Does |
|---|---|
| `--html <file>` | Also writes the side-by-side HTML report |
| `--json` | Prints only the changes as [JSON](../guide/comparing.md#json-output), instead of the summary, so a script can read them from the pipe |
| `--json <file>` | Saves the JSON to a file, and prints the summary as usual |

Each input can be:

- **A PDF**, recognized by its `%PDF-` header, whatever its extension.
- **A DOCX file**, recognized by its `.docx` extension.
- **A snapshot file**, or any other text, compared line by line when it isn't a PDiffy snapshot.

```text
pdiffy statement-v1.pdf statement-v2.pdf
pdiffy statement-v1.pdf statement-v2.pdf --html report.html
pdiffy statement-v1.pdf statement-v2.pdf --json > changes.json
pdiffy Invoice.verified.pdfua.txt Invoice.received.pdfua.txt
```

## Snapshot

```text
pdiffy snapshot <file.pdf|file.docx> [<out>]
```

Prints the file's snapshot, or saves it to `<out>`. Useful for seeing what PDiffy reads from a document, and for
[seeding snapshots](../guide/migrating-from-verify.md#1-seed-the-new-snapshots-from-the-approved-files) from existing
files.

```text
pdiffy snapshot statement.pdf
pdiffy snapshot statement.pdf statement.pdfua.txt
```

## Accept

```text
pdiffy accept [<files or folders>] [--all]
```

Replaces each received snapshot's verified file with it, keeping its extension (`Letter.received.json` becomes
`Letter.verified.json`), and deletes its report, as a test does in accept mode.

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
changed tests/Snapshots/Invoice.received.pdfua.txt: 3: 1 moved, 1 attribute, 1 text; by section: structure 3; 2 can affect accessibility
new tests/Snapshots/Letter.received.docx.txt
```

See [Continuous integration](../guide/ci.md) for complete pipeline examples.
