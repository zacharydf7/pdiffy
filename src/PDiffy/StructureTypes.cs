using System.Reflection;

namespace PDiffy;

/// <summary>What <see cref="StructureType"/> lists, for checking a type a test asks for.</summary>
internal static class StructureTypes
{
    // PDF 2.0 adds standard types that are never role-mapped; StructureType lists only PDF 1.7's for now (#115).
    private static readonly string[] Pdf2Types =
        ["DocumentFragment", "Aside", "Title", "FENote", "Sub", "Em", "Strong", "Artifact"];

    private static readonly string[] Standard = typeof(StructureType)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!)
        .Concat(Pdf2Types).ToArray();

    public static bool IsKnown(string type, IReadOnlyList<RoleMapping> roleMap) =>
        Standard.Contains(type) || roleMap.Any(mapping => mapping.Custom == type || mapping.Standard == type);

    // A type that differs only in case, such as "figure", is the likely meant one.
    public static string Unknown(string type, IReadOnlyList<RoleMapping> roleMap, IEnumerable<string> used)
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
