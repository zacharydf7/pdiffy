using System.Globalization;
using UglyToad.PdfPig.Graphics.Colors;

namespace PDiffy.Reading;

/// <summary>Writes a color so a change of color space shows up: gray:0.5, #ff0000, cmyk:0,0,0,1 or pattern:shading.</summary>
internal static class ColorText
{
    // Patterns (gradients and tiled fills) have no single color; PdfPig throws if asked for one.
    public static string? Format(IColor? color) => color switch
    {
        null => null,
        GrayColor gray => $"gray:{Number(gray.Gray)}",
        CMYKColor cmyk => $"cmyk:{Number(cmyk.C)},{Number(cmyk.M)},{Number(cmyk.Y)},{Number(cmyk.K)}",
        TilingPatternColor => "pattern:tiling",
        ShadingPatternColor => "pattern:shading",
        PatternColor => "pattern",
        _ => Hex(color.ToRGBValues()),
    };

    private static string Hex((double R, double G, double B) rgb) =>
        $"#{Byte(rgb.R):x2}{Byte(rgb.G):x2}{Byte(rgb.B):x2}";

    private static int Byte(double value) => (int)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);

    private static string Number(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
}
