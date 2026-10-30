using System.Text;
using PDiffy.Diffing;

namespace PDiffy.Cli;

/// <summary>
/// How many verified snapshots compare each section, and how many leave out each field, read from their headers alone,
/// so a suite that has drifted into ignoring something everywhere shows it.
/// </summary>
internal sealed class SnapshotCoverage
{
    private const string IgnoredSections = "# ignored sections: ";
    private const string IgnoredFields = "# ignored fields: ";

    private readonly List<Format> _formats;
    private readonly int _others;

    private SnapshotCoverage(List<Format> formats, int others) => (_formats, _others) = (formats, others);

    public static SnapshotCoverage Of(IEnumerable<string> verifiedFiles)
    {
        var headers = verifiedFiles.Select(file => File.ReadLines(file).TakeWhile(line => line.StartsWith('#')).ToList())
            .ToList();
        var formats = new List<Format>
        {
            Format.Read("PDF", SnapshotWriter.FormatPrefix, Names<PdfSection>(), headers),
            Format.Read("Word", DocxSnapshotWriter.FormatPrefix, Names<DocxSection>(), headers),
        };
        return new SnapshotCoverage(formats, headers.Count - formats.Sum(format => format.Count));
    }

    public string ToText()
    {
        var text = new StringBuilder();
        foreach (var format in _formats.Where(format => format.Count > 0))
        {
            text.Append($"{format.Count} {format.Name} snapshot(s)\n");
            var width = format.Sections.Max(section => section.Length);
            foreach (var section in format.Sections)
            {
                var ignored = format.IgnoredIn(section);
                text.Append($"  {section.PadRight(width)}  {format.ComparedIn(section)} of {format.Count}")
                    .Append(ignored > 0 ? $", ignored in {ignored}\n" : "\n");
            }

            text.Append($"  fields ignored: {format.FieldsIgnored()}\n");
            if (format.LimitedToPages > 0) text.Append($"  limited to some pages: {format.LimitedToPages}\n");
            if (format.WithTolerances > 0) text.Append($"  with tolerances: {format.WithTolerances}\n");
        }

        if (_others > 0) text.Append($"{_others} other snapshot(s), text or JSON, which have no sections\n");
        return text.Length == 0 ? "No verified snapshots.\n" : text.ToString();
    }

    public string ToMarkdown()
    {
        var text = new StringBuilder("### PDiffy: snapshot coverage\n");
        foreach (var format in _formats.Where(format => format.Count > 0))
        {
            text.Append($"\n**{format.Count} {format.Name} snapshot(s)**\n\n")
                .Append("| Section | Compared in | Ignored in |\n|---|---|---|\n");
            foreach (var section in format.Sections)
            {
                var ignored = format.IgnoredIn(section);
                text.Append($"| `{section}` | {format.ComparedIn(section)} | {ignored} |\n");
            }

            text.Append($"\nFields ignored: {format.FieldsIgnored(code: true)}\n");
            if (format.LimitedToPages > 0) text.Append($"\nLimited to some pages: {format.LimitedToPages}\n");
            if (format.WithTolerances > 0) text.Append($"\nWith tolerances: {format.WithTolerances}\n");
        }

        if (_others > 0) text.Append($"\n{_others} other snapshot(s), text or JSON, which have no sections\n");
        if (!_formats.Any(format => format.Count > 0) && _others == 0) text.Append("No verified snapshots.\n");
        return text.ToString();
    }

    private static List<string> Names<T>() where T : struct, Enum => [.. Enum.GetValues<T>().Select(SnapshotWriter.Name)];

    // The snapshots of one format: each one's ignored sections and fields, and whether it keeps only some pages or
    // tolerates numbers.
    private sealed record Format(string Name, List<string> Sections, List<List<string>> Headers)
    {
        public int Count => Headers.Count;

        public int LimitedToPages => Headers.Count(header => header.Any(line => line.StartsWith("# only pages: ")));

        public int WithTolerances => Headers.Count(header => header.Any(line => line.StartsWith(Tolerances.HeaderPrefix)));

        public static Format Read(string name, string prefix, List<string> sections, List<List<string>> headers) =>
            new(name, sections, headers.Where(header => header.FirstOrDefault()?.StartsWith(prefix) == true).ToList());

        public int IgnoredIn(string section) => Headers.Count(header => Listed(header, IgnoredSections).Contains(section));

        // The checks section prints only in a snapshot that opts in, which its header says.
        public int ComparedIn(string section) => Headers.Count(header =>
            !Listed(header, IgnoredSections).Contains(section)
            && (section != SnapshotWriter.Name(PdfSection.Checks)
                || header.Any(line => line.StartsWith(SnapshotWriter.ChecksHeader))));

        // Most used first, such as "positions in 30, colors in 12".
        public string FieldsIgnored(bool code = false)
        {
            var counts = Headers.SelectMany(header => Listed(header, IgnoredFields)).CountBy(field => field)
                .OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{(code ? $"`{pair.Key}`" : pair.Key)} in {pair.Value}").ToList();
            return counts.Count == 0 ? "none" : string.Join(", ", counts);
        }

        private static IEnumerable<string> Listed(List<string> header, string prefix) =>
            header.Where(line => line.StartsWith(prefix)).SelectMany(line => line[prefix.Length..].Split(", "));
    }
}
