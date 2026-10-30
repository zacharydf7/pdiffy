---
_layout: landing
---

<div class="pd-home">

<section class="pd-hero">
<div class="pd-wrap">
<div>
<div class="pd-eyebrow">Snapshot tests for PDF and Word files in .NET</div>
<h1>Approve a document once. <span>See every change as an edit.</span></h1>
<p class="pd-lede">PDiffy reads what a document says, its structure, text, accessibility and layout, and ignores how
the file happens to be encoded. When a generated PDF or Word file changes, the test says what changed, element by
element, on the page where it happened.</p>
<div class="pd-actions">
<a class="pd-btn pd-btn-primary" href="guide/getting-started.md">Get started <i class="bi bi-arrow-right"></i></a>
<a class="pd-btn pd-btn-secondary" href="samples/TrailGuide.diff.html">See a sample report</a>
<a class="pd-btn pd-btn-secondary" href="https://github.com/zacharydf7/pdiffy"><i class="bi bi-github"></i> GitHub</a>
</div>
<div class="pd-install">$ <b>dotnet add package</b> PDiffy</div>
</div>
<div class="pd-windows">
<div class="pd-window">
<div class="pd-window-bar"><i></i><i></i><i></i><span>TrailGuideTests.cs</span></div>
<pre><code class="lang-csharp">[Fact]
public async Task TrailGuide_ShouldMatchApprovedSnapshot_WhenGenerated()
{
    using Stream pdf = await ReportGenerator.CreateTrailGuideAsync();
    await Snapshot.VerifyPdfAsync(pdf);
}</code></pre>
</div>
<div class="pd-window">
<div class="pd-window-bar"><i></i><i></i><i></i><span>When the guide changes</span></div>
<pre><code class="pd-output nohighlight"><span class="pd-dim">3 change(s): 1 moved, 1 attribute, 1 text</span>
<span class="pd-dim">by section: structure 3</span>
<span class="pd-dim">2 can affect accessibility, listed first</span>
↕ moved structure &gt; Document &gt; Figure (p1) <span class="pd-flag">[accessibility]</span>
    reading position 3 -&gt; 10
~ attribute structure &gt; Document &gt; Table &gt; TR[1] &gt; TH2 (p2) <span class="pd-flag">[accessibility]</span>
    scope: Column -&gt; (missing)
~ text structure &gt; Document &gt; Table &gt; TR[2] &gt; TD[2] (p2)
    "Go <del>with</del> <ins>without</ins> a guide"</code></pre>
</div>
</div>
</div>
</section>

<section>
<div class="pd-wrap">
<div class="pd-section-head">
<h2>One reading, three kinds of test</h2>
<p>PDiffy turns a document into a plain-text snapshot you review in pull requests, then compares, checks and explains
it the same way every time.</p>
</div>
<div class="pd-grid">
<a class="pd-card" href="guide/pdf-snapshots.md">
<h3><i class="bi bi-camera"></i>Snapshot tests</h3>
<p>Compare a PDF, Word document, text or JSON with its approved snapshot, and approve a change with one command.</p>
</a>
<a class="pd-card" href="guide/comparing.md">
<h3><i class="bi bi-arrow-left-right"></i>Comparisons</h3>
<p>Compare two documents, or two folders of them, from a test or the command line, with no snapshot files.</p>
</a>
<a class="pd-card" href="guide/checking-documents.md">
<h3><i class="bi bi-check2-circle"></i>Checks</h3>
<p>Assert on alt text, forms, fonts and settings through typed properties, or run the built-in accessibility
checks.</p>
</a>
<a class="pd-card" href="reference/accessibility-changes.md">
<h3><i class="bi bi-universal-access"></i>Accessibility first</h3>
<p>Lost alt text, a changed reading order or new untagged content is flagged and listed before everything else.</p>
</a>
<a class="pd-card" href="guide/options.md">
<h3><i class="bi bi-sliders"></i>Compare what a test guards</h3>
<p>Profiles, ignored sections and details, tolerances and scrubbers, all as enums. No PDF knowledge needed.</p>
</a>
<a class="pd-card" href="guide/word-documents.md">
<h3><i class="bi bi-file-earmark-word"></i>Word documents too</h3>
<p>Styles, lists, tables, tracked changes and fields, read straight from the file with no Office or SDK.</p>
</a>
</div>
</div>
</section>

<section class="pd-band">
<div class="pd-wrap pd-callout">
<div class="pd-section-head">
<h2>Upgrade without breaking your documents</h2>
<p>A new version of the library that writes your PDFs can drop a font's Unicode map, retag a heading as a paragraph
or strip the tags, while every page looks the same. A snapshot test catches it the first time the upgrade runs, and
<code>pdiffy compare</code> checks a whole folder of documents before and after.</p>
<p><a href="guide/generator-upgrades.md">Catch generator upgrades <i class="bi bi-arrow-right"></i></a></p>
</div>
<div class="pd-window">
<div class="pd-window-bar"><i></i><i></i><i></i><span>Terminal</span></div>
<pre><code class="pd-output nohighlight"><span class="pd-dim">$</span> dotnet pdiffy before.pdf after.pdf
<span class="pd-dim">2 change(s): 1 attribute, 1 changed</span>
<span class="pd-dim">by section: structure 1, fonts 1</span>
<span class="pd-dim">2 can affect accessibility, listed first</span>
~ changed structure &gt; Document &gt; P[1] (p1) <span class="pd-flag">[accessibility]</span>
    type: <del>H1</del> -&gt; <ins>P</ins>
~ attribute fonts &gt; Helvetica <span class="pd-flag">[accessibility]</span>
    to-unicode: <del>yes</del> -&gt; <ins>no</ins></code></pre>
</div>
</div>
</section>

<section>
<div class="pd-wrap">
<div class="pd-section-head">
<h2>A report you can open from CI</h2>
<p>Every failure also writes one HTML file: both snapshots side by side, the changed words highlighted, accessibility
changes first and a map of what moved on each page.</p>
</div>
<div class="pd-grid">
<a class="pd-card" href="samples/TrailGuide.diff.html">
<h3><i class="bi bi-window-split"></i>See a sample report</h3>
<p>The report for the trail guide above, as a test writes it.</p>
</a>
<a class="pd-card" href="guide/ci.md">
<h3><i class="bi bi-gear-wide-connected"></i>Continuous integration</h3>
<p>Keep the received files and reports as build artifacts, and fail fast on a missing snapshot.</p>
</a>
<a class="pd-card" href="reference/command-line.md">
<h3><i class="bi bi-terminal"></i>The pdiffy tool</h3>
<p>Accept snapshots, compare folders, check documents and find unused snapshots from the command line.</p>
</a>
</div>
</div>
</section>

<section class="pd-band">
<div class="pd-wrap">
<div class="pd-section-head">
<h2>Find your way</h2>
<p>The documentation is grouped by what you're trying to do.</p>
</div>
<div class="pd-grid">
<div class="pd-path">
<h3>Write tests</h3>
<ul>
<li><a href="guide/getting-started.md">Getting started</a></li>
<li><a href="guide/pdf-snapshots.md">PDF snapshots</a></li>
<li><a href="guide/word-documents.md">Word documents</a></li>
<li><a href="guide/text-and-json.md">Text and JSON</a></li>
<li><a href="guide/options.md">Ignore and scrub</a></li>
</ul>
</div>
<div class="pd-path">
<h3>Go further</h3>
<ul>
<li><a href="guide/comparing.md">Compare two documents</a></li>
<li><a href="guide/checking-documents.md">Check documents directly</a></li>
<li><a href="guide/generator-upgrades.md">Catch generator upgrades</a></li>
<li><a href="guide/recipes.md">Recipes for generators</a></li>
<li><a href="guide/troubleshooting.md">Troubleshooting</a></li>
</ul>
</div>
<div class="pd-path">
<h3>Look things up</h3>
<ul>
<li><a href="reference/pdf-snapshot-format.md">PDF snapshot format</a> and <a href="reference/docx-snapshot-format.md">Word snapshot format</a></li>
<li><a href="reference/options.md">Every option</a></li>
<li><a href="api/index.md">API reference</a></li>
<li><a href="project/release-notes.md">Release notes</a></li>
<li><a href="project/roadmap.md">Roadmap</a></li>
</ul>
</div>
</div>
</div>
</section>

<section>
<div class="pd-wrap">
<div class="pd-section-head">
<h2>Install</h2>
<p>Both packages target .NET 10 and are fully managed, so a snapshot made on Windows matches one made on Linux. They
work with any test framework.</p>
</div>
<div class="pd-grid">
<div class="pd-card">
<h3><i class="bi bi-box-seam"></i><a href="https://www.nuget.org/packages/PDiffy">PDiffy</a></h3>
<p>The library for your test project.</p>
<pre><code class="nohighlight">dotnet add package PDiffy</code></pre>
</div>
<div class="pd-card">
<h3><i class="bi bi-terminal"></i><a href="https://www.nuget.org/packages/PDiffy.Cli">PDiffy.Cli</a></h3>
<p>The <code>pdiffy</code> command-line tool, as a local tool at the package's version.</p>
<pre><code class="nohighlight">dotnet new tool-manifest
dotnet tool install PDiffy.Cli --version &lt;version&gt;</code></pre>
</div>
</div>
</div>
</section>

</div>
