namespace PDiffy;

/// <summary>Names a page size when it is within 1 pt of a known one, otherwise W×H in whole points.</summary>
internal static class PageSize
{
    private static readonly (string Name, double Width, double Height)[] Known =
    [
        ("letter", 612, 792),
        ("legal", 612, 1008),
        ("a4", 595.28, 841.89),
    ];

    public static string Describe(double width, double height)
    {
        foreach (var (name, w, h) in Known)
        {
            if (Near(width, w) && Near(height, h)) return name;
            if (Near(width, h) && Near(height, w)) return name + "-landscape";
        }

        return $"{Round(width)}x{Round(height)}";
    }

    /// <summary>The size <see cref="Describe"/> printed, read back: a name, a name with "-landscape", or W×H.</summary>
    public static (double Width, double Height)? Dimensions(string description)
    {
        var landscape = description.EndsWith("-landscape", StringComparison.Ordinal);
        var name = landscape ? description[..^"-landscape".Length] : description;
        foreach (var (known, width, height) in Known)
        {
            if (known == name) return landscape ? (height, width) : (width, height);
        }

        var parts = description.Split('x');
        return parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) ? (w, h) : null;
    }

    private static bool Near(double a, double b) => Math.Abs(a - b) <= 1;

    private static int Round(double d) => (int)Math.Round(d, MidpointRounding.AwayFromZero);
}
