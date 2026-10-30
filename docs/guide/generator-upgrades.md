# Catch generator upgrades

The riskiest change to a document is often one nobody made on purpose: upgrading the library that writes it. A new
version of a PDF generator, a report engine or a post-processing step can break a document while the page looks
exactly the same, so a review of the rendered page, or a comparison of page images, lets it through. Common breaks:

- **Text that can't be read.** A font loses the map from its character codes to Unicode. The page looks the same, but
  copying, searching and screen readers get the wrong characters or none.
- **Tags in the wrong place.** A link is tagged under the wrong element, a heading comes out as a paragraph, or a table
  loses the scope on its header cells.
- **Tags stripped.** A step that compresses, merges or signs the file drops the structure tree, so the whole document is
  untagged.
- **Settings reset.** The document language, the title shown in the viewer's title bar or the tab order on each page
  goes missing.

PDiffy reads each of these into the snapshot, so a snapshot test catches them the first time the upgraded generator
runs, and lists the ones that can affect accessibility first.

## What the failure looks like

A test snapshots the guide the generator writes:

```csharp
[Fact]
public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailGuideAsync();

    await PdfSnapshot.VerifyAsync(pdf);
}
```

After the upgrade, the page looks the same, but the test fails:

```text
The PDF's snapshot differs from the verified one.

2 change(s): 1 attribute, 1 changed
by section: structure 1, fonts 1
2 can affect accessibility, listed first
~ changed structure > Document > P[1] (p1) [accessibility]
    type: H1 -> P
~ attribute fonts > Arial [accessibility]
    to-unicode: yes -> no
```

The heading is now a paragraph, so a screen reader's list of headings loses it, and Arial no longer maps to Unicode, so
its text can't be read aloud or searched. Neither shows on the page.

## Compare before and after an upgrade

To check every document a generator writes, not only the ones with snapshot tests, write them all with the current
version, then again after the upgrade, and compare the two folders:

```text
# On the current version.
dotnet run --project tools/GenerateSamples -- before

# After upgrading the generator.
dotnet run --project tools/GenerateSamples -- after

dotnet pdiffy compare before after --html upgrade.html --profile accessibility
```

`pdiffy compare` pairs the files by their path inside each folder and exits with `1` when anything changed. The
`accessibility` profile leaves out how content looks, so only what assistive technology gets is compared; leave it out
to compare everything. `--html` saves a page with a row per file that differs and, next to it, each changed file's
report, accessibility changes first. See [the command reference](../reference/command-line.md#compare-folders) for
every option, and [Compare two documents](comparing.md) for doing the same from a test with `CompareTo`.
