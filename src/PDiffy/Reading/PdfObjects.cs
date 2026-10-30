using System.Globalization;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Reads values out of PdfPig's raw dictionaries, following indirect references.</summary>
internal sealed class PdfObjects(PdfDocument document)
{
    // How deep Describe goes into nested arrays, so an array that contains itself can't loop forever.
    internal const int MaxArrayDepth = 4;

    // A reference to an object the file doesn't define is the null object (ISO 32000-1, 7.3.10), and so is one whose
    // place in the file is damaged.
    public IToken? Resolve(IToken? token)
    {
        while (token is IndirectReferenceToken reference)
        {
            try
            {
                token = document.Structure.GetObject(reference.Data).Data;
            }
            catch (Exception problem) when (problem is InvalidOperationException or ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return token;
    }

    public IToken? Get(DictionaryToken owner, string key) => Resolve(owner.Data.GetValueOrDefault(key));

    public DictionaryToken? Dictionary(DictionaryToken owner, string key) => Get(owner, key) as DictionaryToken;

    public string? Name(DictionaryToken owner, string key) => Get(owner, key) is NameToken name ? name.Data : null;

    public string? Text(DictionaryToken owner, string key) => TextOf(Get(owner, key));

    /// <summary>The file name a file specification gives: its /UF (Unicode), or else its /F.</summary>
    public string? FileName(DictionaryToken specification) => Text(specification, "UF") ?? Text(specification, "F");

    public int? Integer(DictionaryToken owner, string key) => Get(owner, key) is NumericToken number ? number.Int : null;

    public bool Boolean(DictionaryToken owner, string key) => Get(owner, key) is BooleanToken boolean && boolean.Data;

    /// <summary>Whether an action runs JavaScript: a JavaScript action, or a Rendition action with a /JS script.</summary>
    public bool RunsScript(DictionaryToken action) => Name(action, "S") switch
    {
        "JavaScript" => true,
        "Rendition" => action.Data.ContainsKey("JS"),
        _ => false,
    };

    /// <summary>The items of an array, or the value itself when it is a single item. Items are not resolved.</summary>
    public IReadOnlyList<IToken> Items(IToken? value) => Resolve(value) switch
    {
        null => [],
        ArrayToken array => array.Data,
        _ => [value!],
    };

    /// <summary>
    /// A rectangle such as /Rect or /CropBox: two opposite corners in either order, turned into the bottom-left corner
    /// plus a size in whole points. Null when the entry is missing or not four numbers.
    /// </summary>
    public PageBox? Box(DictionaryToken owner, string key)
    {
        if (Get(owner, key) is not ArrayToken array || array.Data.Count < 4)
        {
            return null;
        }

        var numbers = array.Data.Take(4).Select(item => Resolve(item) is NumericToken number ? number.Double : 0).ToList();
        var left = Math.Min(numbers[0], numbers[2]);
        var bottom = Math.Min(numbers[1], numbers[3]);
        var width = Math.Abs(numbers[2] - numbers[0]);
        var height = Math.Abs(numbers[3] - numbers[1]);
        return new PageBox(Round(left), Round(bottom), Round(width), Round(height));
    }

    /// <summary>A simple value as text: a name, string, number or true/false, or an array of them in brackets.</summary>
    public string? Describe(IToken? token) => Describe(token, depth: 0);

    /// <summary>A dictionary's entries as sorted settings, leaving out the given keys.</summary>
    public IReadOnlyList<Setting> Settings(DictionaryToken? dictionary, params string[] skip) =>
        (dictionary?.Data ?? new Dictionary<string, IToken>())
            .Where(pair => !skip.Contains(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new Setting(pair.Key, Describe(pair.Value) ?? ""))
            .ToList();

    /// <summary>
    /// The (key, value) pairs of a name tree or number tree. A node holds them in <paramref name="pairsKey"/> (/Names or
    /// /Nums) or holds /Kids nodes that do. A node reached a second time (a malformed loop) is skipped.
    /// </summary>
    public IReadOnlyList<(IToken Key, IToken Value)> TreeEntries(IToken? root, string pairsKey)
    {
        var entries = new List<(IToken Key, IToken Value)>();
        var visited = new HashSet<IndirectReference>();

        // Walked with a stack, so a tree nested thousands deep can't overflow it. A node's kids come before its own pairs,
        // so the node goes back on the stack, already read, under its kids.
        var pending = new Stack<(IToken? Node, DictionaryToken? Read)>([(root, null)]);
        while (pending.TryPop(out var next))
        {
            if (next.Read is { } read)
            {
                var pairs = Items(read.Data.GetValueOrDefault(pairsKey));
                for (var i = 0; i + 1 < pairs.Count; i += 2)
                {
                    entries.Add((pairs[i], pairs[i + 1]));
                }

                continue;
            }

            if (next.Node is IndirectReferenceToken visit && !visited.Add(visit.Data))
            {
                continue;
            }

            if (Resolve(next.Node) is not DictionaryToken dictionary)
            {
                continue;
            }

            pending.Push((null, dictionary));
            foreach (var kid in Items(dictionary.Data.GetValueOrDefault("Kids")).Reverse())
            {
                pending.Push((kid, null));
            }
        }

        return entries;
    }

    /// <summary>
    /// A stream's decoded contents. For filters PdfPig doesn't support (such as JPEG's DCTDecode) the stored bytes are the
    /// contents. Null when the data is damaged and can't be decoded.
    /// </summary>
    public byte[]? Contents(StreamToken stream)
    {
        try
        {
            return stream.Decode(document.Structure.FilterProvider).ToArray();
        }
        catch (NotSupportedException)
        {
            return stream.Data.ToArray();
        }
        catch (Exception)
        {
            // PdfPig throws many exception types on damaged data (KeyNotFoundException for bad LZW, for example).
            // The caller records the contents as unreadable, so the snapshot shows it.
            return null;
        }
    }

    // An action and those it chains through /Next, in the order they run. An action reached a second time (a
    // malformed loop) is skipped.
    public List<DictionaryToken> Chain(IToken? first)
    {
        var chain = new List<DictionaryToken>();
        var pending = new Stack<IToken>();
        var visited = new HashSet<IndirectReference>();
        if (first is not null)
        {
            pending.Push(first);
        }

        while (pending.Count > 0)
        {
            var token = pending.Pop();
            if (token is IndirectReferenceToken reference && !visited.Add(reference.Data))
            {
                continue;
            }

            if (Resolve(token) is not DictionaryToken action)
            {
                continue;
            }

            chain.Add(action);

            // Pushed last to first, so they're taken first to last.
            foreach (var next in Items(action.Data.GetValueOrDefault("Next")).Reverse())
            {
                pending.Push(next);
            }
        }

        return chain;
    }

    public static string? TextOf(IToken? token) => token switch
    {
        StringToken text => text.Data,
        HexToken hex => hex.Data,
        _ => null,
    };

    private string? Describe(IToken? token, int depth) => Resolve(token) switch
    {
        null => null,
        NameToken name => name.Data,
        BooleanToken boolean => boolean.Data ? "true" : "false",
        NumericToken number => number.Data.ToString(CultureInfo.InvariantCulture),
        ArrayToken array when depth < MaxArrayDepth => $"[{string.Join(" ", array.Data.Select(item => Describe(item, depth + 1)))}]",
        ArrayToken => "[...]",
        var other => TextOf(other),
    };

    public static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
