using System.Reflection;

namespace PDiffy;

/// <summary>What <see cref="StructureType"/> lists, for checking a type a test asks for.</summary>
internal static class StructureTypes
{
    // The type of an element that has none (no /S).
    public const string None = "(none)";

    // PDF 2.0 adds standard types that are never role-mapped; StructureType lists only PDF 1.7's for now (#115).
    private static readonly string[] Pdf2Types =
        ["DocumentFragment", "Aside", "Title", "FENote", "Sub", "Em", "Strong", "Artifact"];

    private static readonly string[] Pdf17Types = typeof(StructureType)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!).ToArray();

    private static readonly string[] Standard = [.. Pdf17Types, .. Pdf2Types];

    // PDF/UA-1 builds on PDF 1.7, so only its types are standard there.
    public static bool IsStandardInPdf17(string type) => Pdf17Types.Contains(type);

    // A type and the types a role map maps it to in turn, up to the first standard type, as PDF 1.7 does, a type with no
    // mapping, or up to and including the first repeat. A standard type isn't mapped.
    public static List<string> RoleMapChain(string type, IReadOnlyDictionary<string, string> map)
    {
        var chain = new List<string> { type };
        var seen = new HashSet<string> { type };
        var current = type;
        while (!IsStandardInPdf17(current) && map.TryGetValue(current, out var next))
        {
            chain.Add(next);
            if (!seen.Add(next)) break;

            current = next;
        }

        return chain;
    }

    public static bool IsKnown(string type, IReadOnlyList<PdfRoleMapping> roleMap) =>
        Standard.Contains(type) || roleMap.Any(mapping => mapping.Custom == type || mapping.Standard == type);

    // A type that differs only in case, such as "figure", is the likely meant one.
    public static string Unknown(string type, IReadOnlyList<PdfRoleMapping> roleMap, IEnumerable<string> used)
    {
        var meant = Standard.Concat(roleMap.Select(mapping => mapping.Custom)).Concat(used)
            .FirstOrDefault(known => string.Equals(known, type, StringComparison.OrdinalIgnoreCase));
        var hint = meant is null ? " See StructureType for the standard types." : $" Did you mean '{meant}'?";
        return $"'{type}' isn't a standard structure type, in the role map, or used in the document.{hint}";
    }

    // H1 to H6; a plain H has no level.
    public static int? HeadingLevel(string type) =>
        type is ['H', >= '1' and <= '6'] ? type[1] - '0' : null;
}
