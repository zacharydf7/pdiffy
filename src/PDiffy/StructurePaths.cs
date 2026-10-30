namespace PDiffy;

/// <summary>
/// Paths to structure elements as a snapshot diff and a check finding show them: <c>structure > Document > P[2]</c>.
/// </summary>
internal static class StructurePaths
{
    public static readonly string Root = SnapshotWriter.Name(Section.Structure);

    // A sibling that shares its key with others gets its place among them: P[2].
    public static IEnumerable<(StructureElement Element, string Path)> WithPaths(
        IReadOnlyList<StructureElement> siblings, string parentPath)
    {
        var keys = siblings.Select(KeyOf).ToList();
        var counts = keys.CountBy(key => key).ToDictionary();
        var seen = new Dictionary<string, int>();
        for (var i = 0; i < siblings.Count; i++)
        {
            var place = seen[keys[i]] = seen.GetValueOrDefault(keys[i]) + 1;
            var label = counts[keys[i]] > 1 ? $"{keys[i]}[{place}]" : keys[i];
            yield return (siblings[i], $"{parentPath} > {label}");
        }
    }

    /// <summary>The elements again, each with its <see cref="StructureElement.Path"/> and its children's.</summary>
    public static IReadOnlyList<StructureElement> Assign(IReadOnlyList<StructureElement> siblings, string parentPath) =>
        WithPaths(siblings, parentPath)
            .Select(pair => pair.Element with { Path = pair.Path, Children = Assign(pair.Element.Children, pair.Path) })
            .ToList();

    // As the snapshot prints an element: its name or type, then the type it's mapped to.
    private static string KeyOf(StructureElement element) =>
        (element.Name ?? element.Type) + (element.MappedType is null ? "" : "->" + element.MappedType);
}
