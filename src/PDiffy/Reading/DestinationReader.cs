using UglyToad.PdfPig.Outline.Destinations;
using UglyToad.PdfPig.Tokens;

namespace PDiffy.Reading;

/// <summary>Reads where links and actions go, looking named destinations up in the catalog.</summary>
internal sealed class DestinationReader(PdfObjects objects, PageTree pages, DictionaryToken catalog)
{
    // How many of a view's numbers are positions; XYZ's third is its zoom.
    private static readonly Dictionary<string, int> PositionCounts = new()
    {
        ["XYZ"] = 2, ["Fit"] = 0, ["FitB"] = 0, ["FitH"] = 1, ["FitBH"] = 1, ["FitV"] = 1, ["FitBV"] = 1, ["FitR"] = 4,
    };

    private Dictionary<string, IToken>? _named;

    /// <summary>
    /// A destination: [page view positions...], or the name of one. In another file (<paramref name="remote"/>) the page
    /// is a number counted from 0, and a name can't be looked up here.
    /// </summary>
    public Destination? Read(IToken? destination, bool remote = false)
    {
        var resolved = objects.Resolve(destination);
        var name = resolved is NameToken token ? token.Data : PdfObjects.TextOf(resolved);
        if (name is null) return Explicit(resolved, remote);
        if (remote || !Named.TryGetValue(name, out var target)) return new Destination { Name = name };
        return Explicit(objects.Resolve(target), remote: false) ?? new Destination { Name = name };
    }

    /// <summary>A bookmark's destination, which PdfPig has already looked up.</summary>
    public static Destination FromBookmark(ExplicitDestination destination)
    {
        var (view, positions) = destination.Type switch
        {
            ExplicitDestinationType.XyzCoordinates => ("XYZ", [destination.Coordinates.Left, destination.Coordinates.Top]),
            ExplicitDestinationType.FitHorizontally => ("FitH", [destination.Coordinates.Top]),
            ExplicitDestinationType.FitVertically => ("FitV", [destination.Coordinates.Left]),
            ExplicitDestinationType.FitRectangle => ("FitR", [destination.Coordinates.Left, destination.Coordinates.Bottom,
                destination.Coordinates.Right, destination.Coordinates.Top]),
            ExplicitDestinationType.FitBoundingBox => ("FitB", []),
            ExplicitDestinationType.FitBoundingBoxHorizontally => ("FitBH", [destination.Coordinates.Top]),
            ExplicitDestinationType.FitBoundingBoxVertically => ("FitBV", [destination.Coordinates.Left]),
            _ => ("Fit", Array.Empty<double?>()),
        };

        return new Destination
        {
            Page = destination.PageNumber,
            View = view,
            Positions = positions.Select(position => position is { } value ? PdfObjects.Round(value) : (int?)null).ToList(),
        };
    }

    // An explicit destination is an array, or a dictionary whose /D is one (as a named destination can be).
    private Destination? Explicit(IToken? destination, bool remote)
    {
        if (destination is DictionaryToken dictionary) destination = objects.Get(dictionary, "D");
        if (destination is not ArrayToken array || array.Data.Count == 0) return null;

        var first = array.Data[0];
        var page = objects.Resolve(first) is NumericToken index ? index.Int + 1 : remote ? null : pages.NumberOf(first);
        var view = array.Data.Count > 1 ? (objects.Resolve(array.Data[1]) as NameToken)?.Data : null;
        var count = view is not null && PositionCounts.TryGetValue(view, out var known) ? known : array.Data.Count - 2;
        var positions = array.Data.Skip(2).Take(count)
            .Select(item => objects.Resolve(item) is NumericToken number ? PdfObjects.Round(number.Double) : (int?)null);
        var zoom = view == "XYZ" && array.Data.Count > 4 && objects.Resolve(array.Data[4]) is NumericToken { Double: not 0 } scale
            ? scale.Double
            : (double?)null;
        return new Destination { Page = page, View = view, Positions = positions.ToList(), Zoom = zoom };
    }

    // Named destinations live in the catalog's /Dests dictionary (PDF 1.1) or its /Names /Dests name tree.
    private Dictionary<string, IToken> Named => _named ??= ReadNamed();

    private Dictionary<string, IToken> ReadNamed()
    {
        var named = new Dictionary<string, IToken>(StringComparer.Ordinal);
        foreach (var (name, target) in objects.Dictionary(catalog, "Dests")?.Data ?? new Dictionary<string, IToken>())
        {
            named[name] = target;
        }

        var tree = objects.Dictionary(catalog, "Names")?.Data.GetValueOrDefault("Dests");
        foreach (var (key, target) in objects.TreeEntries(tree, "Names"))
        {
            if (PdfObjects.TextOf(objects.Resolve(key)) is { } name) named.TryAdd(name, target);
        }

        return named;
    }
}
