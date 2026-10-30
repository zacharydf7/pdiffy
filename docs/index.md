---
_layout: landing
---

<section class="pd-hero">
  <img src="images/logo.svg" alt="">
  <h1>PDiffy</h1>
  <p class="lead">Snapshot tests, comparisons and checks for PDF and Word files in .NET. PDiffy reads what a document
  says (its structure, text, accessibility and layout) and ignores how the file happens to be encoded.</p>
  <div class="pd-actions">
    <a class="btn btn-primary" href="guide/getting-started.md">Get started</a>
    <a class="btn btn-outline-secondary" href="samples/TrailGuide.diff.html">See a sample report</a>
    <a class="btn btn-outline-secondary" href="https://github.com/zacharydf7/pdiffy">GitHub</a>
  </div>
  <span class="pd-install">dotnet add package PDiffy</span>
</section>

<h2 class="pd-section">A failure that reads like an edit</h2>
<p class="pd-section-lead">Approve a document once. When it changes, the test says what changed, element by element,
on the page where it happened.</p>

<div class="pd-split">
<div>
<div class="pd-caption">The test</div>

```csharp
[Fact]
public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailGuideAsync();

    await Snapshot.VerifyPdfAsync(pdf);
}
```

</div>
<div>
<div class="pd-caption">When the guide changes</div>

```text
3 change(s): 1 moved, 1 attribute, 1 text
by section: structure 3
2 can affect accessibility, listed first
↕ moved structure > Document > Figure (p1) [accessibility]
    reading position 3 -> 10
~ attribute structure > Document > Table > TR[1] > TH2 (p2) [accessibility]
    scope: Column -> (missing)
~ text structure > Document > Table > TR[2] > TD[2] (p2)
    "Go [-with-] {+without+} a guide"
```

</div>
</div>

<h2 class="pd-section">Upgrade without breaking your documents</h2>
<p class="pd-section-lead">A new version of the library that writes your PDFs can drop a font's Unicode map, retag a
heading as a paragraph or strip the tags, while every page looks the same. A snapshot test catches it the first time the
upgrade runs, and <code>pdiffy compare</code> checks every document before and after.
<a href="guide/generator-upgrades.md">Catch generator upgrades</a></p>

<h2 class="pd-section">One reading, three kinds of test</h2>

<div class="pd-cards">
  <a class="pd-card" href="guide/pdf-snapshots.md">
    <i class="bi bi-camera"></i>
    <h3>Snapshot tests</h3>
    <p>Compare a PDF, Word document, text or JSON with its approved snapshot, a plain-text file you review in pull
    requests.</p>
  </a>
  <a class="pd-card" href="guide/comparing.md">
    <i class="bi bi-arrow-left-right"></i>
    <h3>Comparisons</h3>
    <p>Compare two documents, or two folders of them, from a test or the command line, with no snapshot files.</p>
  </a>
  <a class="pd-card" href="guide/checking-documents.md">
    <i class="bi bi-check2-circle"></i>
    <h3>Checks</h3>
    <p>Assert on alt text, forms, fonts and settings through typed properties, or run the built-in accessibility
    checks.</p>
  </a>
</div>

<h2 class="pd-section">Built for real documents</h2>

<div class="pd-cards">
  <div class="pd-card">
    <i class="bi bi-universal-access"></i>
    <h3>Accessibility first</h3>
    <p>Lost alt text, a changed reading order or new untagged content is flagged and listed before everything
    else.</p>
  </div>
  <div class="pd-card">
    <i class="bi bi-sliders"></i>
    <h3>Compare what a test guards</h3>
    <p>Profiles, ignored sections and fields, tolerances and scrubbers, all as enums. No PDF knowledge needed.</p>
  </div>
  <div class="pd-card">
    <i class="bi bi-window-split"></i>
    <h3>A report you can open from CI</h3>
    <p>One HTML file with both snapshots side by side, the changed words highlighted and a map of what moved.</p>
  </div>
  <div class="pd-card">
    <i class="bi bi-file-earmark-word"></i>
    <h3>Word documents too</h3>
    <p>Styles, lists, tables, tracked changes and fields, read straight from the file with no Office or SDK.</p>
  </div>
</div>

<h2 class="pd-section">Explore the docs</h2>

<div class="pd-cards">
  <div class="pd-card">
    <h3>Write tests</h3>
    <ul>
      <li><a href="guide/getting-started.md">Getting started</a></li>
      <li><a href="guide/pdf-snapshots.md">PDF snapshots</a></li>
      <li><a href="guide/word-documents.md">Word documents</a></li>
      <li><a href="guide/text-and-json.md">Text and JSON</a></li>
      <li><a href="guide/options.md">Ignore and scrub</a></li>
    </ul>
  </div>
  <div class="pd-card">
    <h3>Go further</h3>
    <ul>
      <li><a href="guide/comparing.md">Compare two documents</a></li>
      <li><a href="guide/checking-documents.md">Check documents directly</a></li>
      <li><a href="guide/generator-upgrades.md">Catch generator upgrades</a></li>
      <li><a href="guide/recipes.md">Recipes for generators</a></li>
      <li><a href="guide/ci.md">Continuous integration</a></li>
      <li><a href="guide/troubleshooting.md">Troubleshooting</a></li>
    </ul>
  </div>
  <div class="pd-card">
    <h3>Look things up</h3>
    <ul>
      <li><a href="reference/command-line.md">Command line</a></li>
      <li><a href="reference/pdf-snapshot-format.md">PDF snapshot format</a></li>
      <li><a href="reference/docx-snapshot-format.md">Word snapshot format</a></li>
      <li><a href="reference/options.md">Every option</a></li>
      <li><a href="api/index.md">API reference</a></li>
      <li><a href="project/release-notes.md">Release notes</a></li>
      <li><a href="project/roadmap.md">Roadmap</a></li>
    </ul>
  </div>
</div>

<h2 class="pd-section">Install</h2>

<div class="pd-cards">
  <div class="pd-card">
    <h3><a href="https://www.nuget.org/packages/PDiffy">PDiffy</a></h3>
    <p>The library for your test project.</p>
    <pre><code>dotnet add package PDiffy</code></pre>
  </div>
  <div class="pd-card">
    <h3><a href="https://www.nuget.org/packages/PDiffy.Cli">PDiffy.Cli</a></h3>
    <p>The <code>pdiffy</code> command-line tool, as a local tool at the package's version.</p>
    <pre><code>dotnet new tool-manifest
dotnet tool install PDiffy.Cli --version &lt;version&gt;</code></pre>
  </div>
</div>

<p class="pd-note">Both target .NET 10 and are fully managed, so a snapshot made on Windows matches one made on Linux.
They work with any test framework.</p>
