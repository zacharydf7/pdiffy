# Continuous integration

Snapshot tests run in CI like any other test: `dotnet test` fails when a snapshot differs. Three things make a failed
run easy to act on: keep what the run received, publish the HTML reports, and summarize the run on the job page.

## What PDiffy does on a build server

- **Accept mode is ignored.** When `CI` or `TF_BUILD` is set, as GitHub Actions, GitLab, Bitbucket and Azure DevOps all
  do, `PDIFFY_ACCEPT=1` has no effect, so a pipeline can never approve its own snapshots.
- **One snapshot for every OS.** A snapshot is the same text on Windows and Linux, so one verified file serves a matrix
  build.
- **Deterministic builds.** If your build maps source paths (`ContinuousIntegrationBuild`, common on CI), the test
  file's path doesn't exist on disk. Set [`Snapshot.Directory`](xref:PDiffy.Snapshot.Directory) so PDiffy can find the
  snapshots (see [Getting started](getting-started.md#keep-snapshots-in-one-folder)).

## GitHub Actions

This workflow runs the tests, and when they fail, adds a table of every changed snapshot to the job summary and keeps the
received files and reports as an artifact:

```yaml
name: Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x

      - run: dotnet test

      # Only after a failure: summarize the changed snapshots and keep them.
      - name: Summarize snapshot changes
        if: failure()
        run: |
          dotnet tool install --global PDiffy.Cli
          pdiffy report --all --markdown "$GITHUB_STEP_SUMMARY" --html snapshot-summary.html
      - name: Keep received snapshots and reports
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: snapshots
          path: |
            **/*.received.*
            **/*.diff.html
            snapshot-summary.html
```

The job summary then shows a table like this:

| Snapshot | Status | Changes | By section | Can affect accessibility |
|---|---|---|---|---|
| `tests/Snapshots/Invoice.received.pdfua.txt` | changed | 3: 1 moved, 1 attribute, 1 text | structure 3 | 2 |
| `tests/Snapshots/Letter.received.docx.txt` | new | | | |

Download the artifact to open each report, or `snapshot-summary.html`, which links them all. Each report looks like
the [sample report](../samples/DrugGuide.diff.html).

> [!NOTE]
> `pdiffy report` exits with 1 when it finds received snapshots. After `if: failure()` that doesn't matter, since the
> job has already failed.

## Azure DevOps

```yaml
steps:
  - task: UseDotNet@2
    inputs:
      version: 10.0.x

  - script: dotnet test
    displayName: Test

  - script: |
      dotnet tool install --global PDiffy.Cli
      pdiffy report --all --markdown $(Agent.TempDirectory)/snapshots.md --html snapshot-summary.html
      echo "##vso[task.uploadsummary]$(Agent.TempDirectory)/snapshots.md"
    displayName: Summarize snapshot changes
    condition: failed()

  - publish: $(System.DefaultWorkingDirectory)
    artifact: snapshots
    condition: failed()
```

Narrow the published folder to your snapshots folder to keep the artifact small.

## GitLab CI

```yaml
test:
  image: mcr.microsoft.com/dotnet/sdk:10.0
  script:
    - dotnet test
  after_script:
    - dotnet tool install --global PDiffy.Cli
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - pdiffy report --all --html snapshot-summary.html || true
  artifacts:
    when: on_failure
    paths:
      - "**/*.received.*"
      - "**/*.diff.html"
      - snapshot-summary.html
```

## Review and accept locally

CI shows what changed; accepting is done on a developer machine, so the change goes through code review:

1. Run the failing tests locally, or download the artifact into the same folders.
2. Review the reports, or run `pdiffy report --all` for a summary.
3. Run `pdiffy accept --all` and commit the updated `.verified.*` files.

## See also

- [Command line](../reference/command-line.md): `report` and `accept` in full
