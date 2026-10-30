using System.Globalization;
using System.Text;
using System.Xml.Linq;
using static PDiffy.Docx.Reading.DocxXml;

namespace PDiffy.Docx.Reading;

/// <summary>
/// Turns a paragraph's list and level into the label Word shows, such as "A." or "1.2", by counting list items in
/// reading order. Lists that share a definition share their counters, as in Word, except a list that restarts its
/// numbering (a startOverride), which counts on its own.
/// </summary>
internal sealed class NumberingResolver
{
    private const int Levels = 9;

    private readonly Dictionary<string, XElement> _definitions = [];
    private readonly Dictionary<string, XElement> _lists = [];
    private readonly Dictionary<string, int?[]> _counters = [];
    private readonly SortedDictionary<int, SortedSet<int>> _used = [];

    public NumberingResolver(XDocument? numbering)
    {
        foreach (var definition in numbering?.Root?.Elements(W + "abstractNum") ?? [])
        {
            if (Attr(definition, "abstractNumId") is { } id) _definitions.TryAdd(id, definition);
        }

        foreach (var list in numbering?.Root?.Elements(W + "num") ?? [])
        {
            if (Attr(list, "numId") is { } id) _lists.TryAdd(id, list);
        }
    }

    /// <summary>The next label for an item of a list at a level, or null when the list or level isn't defined.</summary>
    public string? Next(int listId, int level)
    {
        var numId = listId.ToString(CultureInfo.InvariantCulture);
        if (!_lists.TryGetValue(numId, out var list) || DefinitionId(list) is not { } definitionId || level is < 0 or >= Levels)
        {
            return null;
        }

        if (!_used.TryGetValue(listId, out var levels)) _used[listId] = levels = [];
        levels.Add(level);
        if (Level(list, definitionId, level) is null) return null;

        var counterKey = list.Descendants(W + "startOverride").Any() ? $"list {numId}" : $"definition {definitionId}";
        if (!_counters.TryGetValue(counterKey, out var counters)) _counters[counterKey] = counters = new int?[Levels];
        counters[level] = (counters[level] ?? Start(list, definitionId, level) - 1) + 1;
        for (var deeper = level + 1; deeper < Levels; deeper++) counters[deeper] = null;
        return Label(list, definitionId, level, counters);
    }

    /// <summary>The lists the document used, each with the levels it used.</summary>
    public List<DocxList> UsedLists() => _used.Select(used =>
    {
        var list = _lists[used.Key.ToString(CultureInfo.InvariantCulture)];
        var definitionId = DefinitionId(list)!;
        return new DocxList
        {
            Id = used.Key, Definition = definitionId,
            Levels = used.Value.Select(level => LevelOf(list, definitionId, level)).ToList(),
        };
    }).ToList();

    // A level the definition doesn't have is reported as missing rather than given made-up settings.
    private DocxListLevel LevelOf(XElement list, string definitionId, int level)
    {
        if (Level(list, definitionId, level) is not { } lvl) return new DocxListLevel
        {
            Level = level, Format = null, Text = null, Start = null, Indent = null, Hanging = null, Font = null,
        };
        var ind = lvl.Element(W + "pPr")?.Element(W + "ind");
        return new DocxListLevel
        {
            Level = level, Format = Attr(lvl.Element(W + "numFmt"), "val") ?? "decimal",
            Text = Attr(lvl.Element(W + "lvlText"), "val") ?? "", Start = Start(list, definitionId, level),
            Indent = Twips(ind, "left") ?? Twips(ind, "start"), Hanging = Twips(ind, "hanging"),
            Font = Attr(lvl.Element(W + "rPr")?.Element(W + "rFonts"), "ascii"),
        };
    }

    // A bullet's label is its text; a number's text has a placeholder (%1 to %9) for each level's counter.
    private string Label(XElement list, string definitionId, int level, int?[] counters)
    {
        var lvl = Level(list, definitionId, level);
        var text = Attr(lvl?.Element(W + "lvlText"), "val") ?? "";
        if (Attr(lvl?.Element(W + "numFmt"), "val") == "bullet") return text;

        var label = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '%' && i + 1 < text.Length && text[i + 1] is >= '1' and <= '9')
            {
                var referenced = text[++i] - '1';
                var format = Attr(Level(list, definitionId, referenced)?.Element(W + "numFmt"), "val") ?? "decimal";
                label.Append(Format(counters[referenced] ?? Start(list, definitionId, referenced), format));
            }
            else label.Append(text[i]);
        }

        return label.ToString();
    }

    private static string Format(int number, string format) => format switch
    {
        "none" => "",
        "decimalZero" => number.ToString("00", CultureInfo.InvariantCulture),
        "upperLetter" => Letters(number).ToUpperInvariant(),
        "lowerLetter" => Letters(number),
        "upperRoman" => Roman(number).ToUpperInvariant(),
        "lowerRoman" => Roman(number),
        _ => number.ToString(CultureInfo.InvariantCulture),
    };

    // Word repeats the letter past z: a to z, then aa to zz, then aaa.
    private static string Letters(int number) =>
        number < 1 ? "" : new string((char)('a' + (number - 1) % 26), (number - 1) / 26 + 1);

    private static string Roman(int number)
    {
        if (number is < 1 or > 3999) return number.ToString(CultureInfo.InvariantCulture);
        (int Value, string Digits)[] numerals =
        [
            (1000, "m"), (900, "cm"), (500, "d"), (400, "cd"), (100, "c"), (90, "xc"), (50, "l"), (40, "xl"), (10, "x"), (9, "ix"),
            (5, "v"), (4, "iv"), (1, "i"),
        ];
        var roman = new StringBuilder();
        foreach (var (value, digits) in numerals)
        {
            for (; number >= value; number -= value) roman.Append(digits);
        }

        return roman.ToString();
    }

    // A list's startOverride replaces where the level starts, the first time and whenever it restarts.
    private int Start(XElement list, string definitionId, int level) =>
        Integer(Override(list, level)?.Element(W + "startOverride"), "val")
        ?? Integer(Level(list, definitionId, level)?.Element(W + "start"), "val") ?? 1;

    // A list can replace a level of its definition with its own.
    private XElement? Level(XElement list, string definitionId, int level) =>
        Override(list, level)?.Element(W + "lvl")
        ?? _definitions.GetValueOrDefault(definitionId)?.Elements(W + "lvl").FirstOrDefault(lvl => Integer(lvl, "ilvl") == level);

    private static XElement? Override(XElement list, int level) =>
        list.Elements(W + "lvlOverride").FirstOrDefault(o => Integer(o, "ilvl") == level);

    // A definition made by a list style only links to it (w:numStyleLink); the levels are in the definition that the
    // style itself defines (w:styleLink).
    private string? DefinitionId(XElement list)
    {
        var id = Attr(list.Element(W + "abstractNumId"), "val");
        if (id is null || Attr(_definitions.GetValueOrDefault(id)?.Element(W + "numStyleLink"), "val") is not { } style) return id;
        return _definitions.FirstOrDefault(definition => Attr(definition.Value.Element(W + "styleLink"), "val") == style).Key ?? id;
    }
}
