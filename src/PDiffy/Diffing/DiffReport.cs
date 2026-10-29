using System.Net;
using System.Text;

namespace PDiffy.Diffing;

/// <summary>
/// A self-contained HTML page showing two snapshots side by side: the change list at the top, then each section with
/// before on the left and after on the right. Edited lines share a row with the changed words highlighted, and long
/// runs of unchanged lines fold away.
/// </summary>
internal static class DiffReport
{
    // Unchanged lines kept visible around each change. Runs of at least MinFoldedLines others fold into one block.
    private const int Context = 3;
    private const int MinFoldedLines = 4;

    public static string Write(string before, string after, SnapshotDiff diff)
    {
        var changes = diff.Changes;
        var plain = !SnapshotTree.IsSectioned(before) || !SnapshotTree.IsSectioned(after);
        var html = new StringBuilder();
        html.Append(Head);
        html.Append($"<h1>Snapshot differences</h1><p class=\"summary\">{Encode(Summary(diff, plain))}</p>");
        WriteChangeList(html, diff.AccessibilityFirst().ToList());
        WritePageIndex(html, changes);

        var beforeSections = Sections(before, plain);
        var afterSections = Sections(after, plain);
        var sections = beforeSections.Keys.Union(afterSections.Keys)
            .Select(name => (Name: name, Rows: Rows(beforeSections.GetValueOrDefault(name) ?? [],
                afterSections.GetValueOrDefault(name) ?? [], plain, name)))
            .ToList();
        var anchors = Anchors(sections.SelectMany(section => section.Rows).ToList(), changes);
        foreach (var (name, rows) in sections)
        {
            var changed = rows.Any(row => row.Kind != ' ');
            html.Append($"<details class=\"section\"{(changed ? " open" : "")}><summary>{Encode(name)}");
            html.Append(changed ? "" : " <span class=\"same\">unchanged</span>").Append("</summary><table>");
            html.Append("<thead><tr><th>Before</th><th>After</th></tr></thead>");
            WriteRows(html, rows, anchors);
            html.Append("</table></details>");
        }

        return html.Append(Tail).ToString();
    }

    private static string Summary(SnapshotDiff diff, bool plain) =>
        diff.IsEmpty ? "No changes."
        : plain ? $"{diff.Changes.Count} change(s)"
        : $"{diff.Changes.Count} change(s), by section: {string.Join(", ", diff.CountsBySection())}";

    // One entry per change, those that can affect accessibility first and labeled, each tagged so the checkboxes above
    // can hide kinds or show only accessibility changes.
    private static void WriteChangeList(StringBuilder html, IReadOnlyList<SnapshotChange> changes)
    {
        if (changes.Count == 0) return;

        html.Append("<div class=\"filters\">");
        foreach (var kind in changes.Select(change => change.Kind).Distinct().Order())
        {
            var count = changes.Count(change => change.Kind == kind);
            html.Append($"<label><input type=\"checkbox\" checked data-kind=\"{kind}\"> {kind} ({count})</label>");
        }

        var flagged = changes.Count(change => change.AffectsAccessibility);
        if (flagged > 0)
        {
            html.Append($"<label><input type=\"checkbox\" data-accessibility> Only accessibility ({flagged})</label>");
        }

        html.Append("</div><ul class=\"changes\">");
        foreach (var change in changes)
        {
            var page = change.Page is int number ? $" <a href=\"#p{number}\">p{number}</a>" : "";
            var flag = change.AffectsAccessibility ? " data-accessibility" : "";
            var label = change.AffectsAccessibility ? " <span class=\"accessibility\">accessibility</span>" : "";
            html.Append($"<li data-kind=\"{change.Kind}\"{flag}><b>{change.Kind}</b> {Encode(change.Path)}{page}{label}");
            html.Append($"<pre>{Encode(change.Detail)}</pre></li>");
        }

        html.Append("</ul>");
    }

    // Each page a change mentions gets one id, which its links point to: on the page's first changed row, or on its first
    // row when this line-by-line view shows no change there (the change list can call a swap a move of the other part).
    private static Dictionary<Row, int> Anchors(List<Row> rows, IReadOnlyList<SnapshotChange> changes)
    {
        var pages = changes.Select(change => change.Page).OfType<int>().ToHashSet();
        var anchors = new Dictionary<Row, int>(ReferenceEqualityComparer.Instance);
        foreach (var row in rows.Where(row => row.Kind != ' ').Concat(rows))
        {
            if (PageOf(row.After ?? row.Before ?? "") is int page && pages.Remove(page)) anchors[row] = page;
        }

        return anchors;
    }

    private static void WritePageIndex(StringBuilder html, IReadOnlyList<SnapshotChange> changes)
    {
        var pages = changes.Select(change => change.Page).OfType<int>().Distinct().Order().ToList();
        if (pages.Count == 0) return;
        html.Append("<nav class=\"pages\">Pages: ");
        html.Append(string.Join(" ", pages.Select(page => $"<a href=\"#p{page}\">{page}</a>")));
        html.Append("</nav>");
    }

    // Rows within Context of a change stay visible; each long run of other rows folds into one expandable block.
    private static void WriteRows(StringBuilder html, List<Row> rows, Dictionary<Row, int> anchors)
    {
        var shown = new bool[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Kind == ' ') continue;
            for (var j = Math.Max(0, i - Context); j <= Math.Min(rows.Count - 1, i + Context); j++) shown[j] = true;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var end = i;
            while (!shown[i] && end + 1 < rows.Count && !shown[end + 1]) end++;
            if (shown[i] || end - i + 1 < MinFoldedLines)
            {
                for (var j = i; j <= end; j++) WriteRow(html, rows[j], anchors);
                i = end;
                continue;
            }

            html.Append("<tbody class=\"fold\"><tr><td colspan=\"2\"><details>");
            html.Append($"<summary>{end - i + 1} unchanged line(s)</summary><table>");
            for (var j = i; j <= end; j++) WriteRow(html, rows[j], anchors);
            html.Append("</table></details></td></tr></tbody>");
            i = end;
        }
    }

    private static void WriteRow(StringBuilder html, Row row, Dictionary<Row, int> anchors)
    {
        var id = anchors.TryGetValue(row, out var page) ? $" id=\"p{page}\"" : "";
        var (left, right) = row.Kind == '~'
            ? Highlight(row.Before!, row.After!)
            : (Encode(row.Before ?? ""), Encode(row.After ?? ""));
        var leftClass = row.Kind is '-' or '~' ? " class=\"removed\"" : "";
        var rightClass = row.Kind is '+' or '~' ? " class=\"added\"" : "";
        html.Append($"<tr{id}><td{leftClass}>{left}</td><td{rightClass}>{right}</td></tr>");
    }

    // Lines align by the longest common run of lines. Between those, a removed and an added line share a row when the
    // change list would call them one edited line.
    private static List<Row> Rows(List<string> before, List<string> after, bool plain, string sectionName)
    {
        var rows = new List<Row>();
        var removed = new List<string>();
        var added = new List<string>();
        foreach (var step in LineDiff.Compare(before, after).Append(new LineChange(' ', "")))
        {
            if (step.Kind == '-') removed.Add(step.Line);
            else if (step.Kind == '+') added.Add(step.Line);
            else
            {
                if (plain) PairInOrder(rows, removed, added);
                else PairGap(rows, removed, added, sectionName);

                removed.Clear();
                added.Clear();
                rows.Add(new Row(' ', step.Line, step.Line));
            }
        }

        rows.RemoveAt(rows.Count - 1);
        return rows;
    }

    private static void PairGap(List<Row> rows, List<string> removed, List<string> added, string sectionName)
    {
        var addedLines = added.ConvertAll(line => SnapshotLine.Parse(line, 0));
        var next = 0;
        foreach (var old in removed)
        {
            var oldLine = SnapshotLine.Parse(old, 0);
            var match = TreeMatcher.MatchOf(oldLine, addedLines, next, sectionName);
            if (match < 0)
            {
                rows.Add(new Row('-', old, null));
                continue;
            }

            rows.AddRange(added.Skip(next).Take(match - next).Select(line => new Row('+', null, line)));
            rows.Add(new Row('~', old, added[match]));
            next = match + 1;
        }

        rows.AddRange(added.Skip(next).Select(line => new Row('+', null, line)));
    }

    // Plain text has no keys to match lines by, so they pair up in order, as in the change list.
    private static void PairInOrder(List<Row> rows, List<string> removed, List<string> added)
    {
        var paired = Math.Min(removed.Count, added.Count);
        for (var i = 0; i < paired; i++) rows.Add(new Row('~', removed[i], added[i]));
        rows.AddRange(removed.Skip(paired).Select(line => new Row('-', line, null)));
        rows.AddRange(added.Skip(paired).Select(line => new Row('+', null, line)));
    }

    // Marks the words that differ between two lines.
    private static (string Left, string Right) Highlight(string before, string after)
    {
        var left = new StringBuilder();
        var right = new StringBuilder();
        foreach (var step in LineDiff.Compare(before.Split(' '), after.Split(' ')))
        {
            var word = Encode(step.Line) + " ";
            if (step.Kind != '+') left.Append(step.Kind == '-' ? $"<mark>{word}</mark>" : word);
            if (step.Kind != '-') right.Append(step.Kind == '+' ? $"<mark>{word}</mark>" : word);
        }

        return (left.ToString().TrimEnd(), right.ToString().TrimEnd());
    }

    // Each section's lines in order, nesting kept as indentation. Plain text is one section.
    private static Dictionary<string, List<string>> Sections(string snapshot, bool plain) => plain
        ? new() { ["text"] = [.. snapshot.Split('\n')] }
        : SnapshotTree.Parse(snapshot).ToDictionary(section => section.Key, section => Flatten(section.Value).ToList());

    private static IEnumerable<string> Flatten(IEnumerable<SnapshotLine> lines) =>
        lines.SelectMany(line => Flatten(line.Children).Prepend(line.Text));

    private static int? PageOf(string line) => SnapshotLine.Parse(line, 0).Page;

    private static string Encode(string text) => WebUtility.HtmlEncode(text);

    private sealed record Row(char Kind, string? Before, string? After);

    // The colors and page styles this report shares with the command line's summary page.
    internal const string BaseStyle = """
        :root { --bg: #ffffff; --text: #1f2328; --muted: #656d76; --line: #d0d7de; --link: #0969da; }
        @media (prefers-color-scheme: dark) { :root { --bg: #0d1117; --text: #e6edf3; --muted: #8d96a0; --line: #30363d;
          --link: #4493f8; } }
        body { margin: 0 auto; max-width: 1400px; padding: 16px; background: var(--bg); color: var(--text);
          font: 14px/1.4 system-ui, sans-serif; }
        h1 { font-size: 20px; }
        a { color: var(--link); }
        """;

    private const string Head = $$"""
        <!doctype html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Snapshot differences</title>
        <style>
        {{BaseStyle}}
        :root { --removed: #ffebe9; --added: #e6ffec; --mark-removed: #ff818266; --mark-added: #4ac26b66; }
        @media (prefers-color-scheme: dark) { :root { --removed: #3c1618; --added: #12261e; --mark-removed: #f8514966;
          --mark-added: #2ea04366; } }
        .summary, .same { color: var(--muted); }
        .filters label { margin-right: 12px; }
        .accessibility { background: #fde68a; color: #1f2328; border-radius: 3px; padding: 0 4px; font-size: 12px; }
        .changes { padding-left: 20px; }
        .changes pre { margin: 2px 0 8px; white-space: pre-wrap; }
        .pages { margin: 12px 0; }
        .pages a { margin-right: 6px; }
        .section { border: 1px solid var(--line); border-radius: 6px; margin: 12px 0; }
        .section > summary { padding: 8px; cursor: pointer; font-weight: 600; }
        table { width: 100%; border-collapse: collapse; table-layout: fixed; }
        td { width: 50%; padding: 1px 8px; vertical-align: top; font: 12px/1.5 ui-monospace, monospace;
          white-space: pre-wrap; word-break: break-word; border-top: 1px solid var(--line); }
        th { text-align: left; padding: 4px 8px; color: var(--muted); font-weight: 600; }
        td.removed { background: var(--removed); }
        td.added { background: var(--added); }
        td.removed mark { background: var(--mark-removed); color: inherit; }
        td.added mark { background: var(--mark-added); color: inherit; }
        .fold summary { color: var(--muted); cursor: pointer; font: 12px system-ui, sans-serif; }
        .fold td { padding: 0; }
        .fold table td { padding: 1px 8px; }
        </style></head><body>
        """;

    private const string Tail = """
        <script>
        // A change shows when its kind is ticked and, with "Only accessibility" ticked, it can affect accessibility.
        function filter() {
          const kinds = [...document.querySelectorAll('.filters input[data-kind]')].filter(box => box.checked)
            .map(box => box.dataset.kind);
          const onlyAccessibility = document.querySelector('.filters input[data-accessibility]')?.checked;
          document.querySelectorAll('.changes li').forEach(item => item.hidden = !kinds.includes(item.dataset.kind)
            || onlyAccessibility && !item.hasAttribute('data-accessibility'));
        }
        document.querySelectorAll('.filters input').forEach(box => box.addEventListener('change', filter));
        // A page link can point into a folded block or a closed section, so open everything around the target.
        function reveal() {
          const target = location.hash && document.getElementById(location.hash.slice(1));
          for (let node = target; node; node = node.parentElement) if (node.tagName === 'DETAILS') node.open = true;
          if (target) target.scrollIntoView();
        }
        window.addEventListener('hashchange', reveal);
        reveal();
        </script></body></html>
        """;
}
