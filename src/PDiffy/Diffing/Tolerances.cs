using System.Globalization;

namespace PDiffy.Diffing;

/// <summary>
/// How far numbers may differ before a line counts as changed, as a PDF snapshot's "# tolerances:" line gives them, such
/// as "positions 1pt". Only the fields in <see cref="Numeric"/> take one.
/// </summary>
internal sealed class Tolerances(IReadOnlyDictionary<Field, double> points)
{
    public const string HeaderPrefix = "# tolerances: ";

    public static readonly Field[] Numeric = [Field.Positions, Field.FontSizes, Field.LineWidths];

    // Subtracting decimals, as in 12.3 - 12, leaves a tiny error, which mustn't push a difference equal to the
    // tolerance past it.
    private const double Slack = 1e-9;

    public static string Header(IEnumerable<KeyValuePair<Field, double>> points) => HeaderPrefix
        + string.Join(", ", points.Select(pair => $"{SnapshotWriter.Name(pair.Key)} {Number(pair.Value)}pt"));

    /// <summary>
    /// The tolerances a snapshot's header gives, or none; an entry that doesn't read as one tolerates nothing.
    /// </summary>
    public static Tolerances Read(string snapshot)
    {
        var header = snapshot.Split('\n').TakeWhile(line => line.StartsWith('#'))
            .FirstOrDefault(line => line.StartsWith(HeaderPrefix, StringComparison.Ordinal));
        var points = new Dictionary<Field, double>();
        foreach (var entry in (header?[HeaderPrefix.Length..] ?? "").Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            var (name, amount) = (entry.Split(' ')[0], entry.Split(' ').ElementAtOrDefault(1) ?? "");
            var field = Numeric.Where(field => SnapshotWriter.Name(field) == name).Cast<Field?>().FirstOrDefault();
            if (field is not null && amount.EndsWith("pt", StringComparison.Ordinal)
                && double.TryParse(amount[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                points[field.Value] = value;
            }
        }

        return new Tolerances(points);
    }

    /// <summary>
    /// Whether every number in a key's value, such as both in at=72,700, moved no more than its tolerance.
    /// </summary>
    public bool Allow(SnapshotLine line, string key, string from, string to)
    {
        if (FieldOf(line, key) is not { } field || !points.TryGetValue(field, out var tolerance)) return false;
        var (before, after) = (Numbers(from), Numbers(to));
        return before is not null && after is not null && before.Length == after.Length
            && before.Zip(after).All(pair => Math.Abs(pair.First - pair.Second) <= tolerance + Slack);
    }

    // A text run's size is its font size; every other size, and every at=, is a position. An embedded file's size, on a
    // line with no page, is a byte count.
    private static Field? FieldOf(SnapshotLine line, string key) => line.Page is null ? null : key switch
    {
        "at" => Field.Positions,
        "size" => line.Words.ElementAtOrDefault(1) == "text" ? Field.FontSizes : Field.Positions,
        "width" => Field.LineWidths,
        _ => null,
    };

    // A value is one number, a pair such as 72,700, or a size such as 120x40.
    private static double[]? Numbers(string value)
    {
        var parts = value.Split(',', 'x');
        var numbers = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])) return null;
        }

        return numbers;
    }

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
