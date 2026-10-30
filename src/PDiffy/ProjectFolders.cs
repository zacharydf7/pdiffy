using System.Reflection;

namespace PDiffy;

/// <summary>The test project folders that PDiffy's build file records in each test assembly.</summary>
internal static class ProjectFolders
{
    public const string MetadataKey = "PDiffy.ProjectDirectory";

    // Reading attributes loads their types, and a loaded assembly can carry one whose assembly isn't there.
    public static string? Of(Assembly assembly)
    {
        try
        {
            var entries = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            return entries.FirstOrDefault(entry => entry.Key == MetadataKey)?.Value;
        }
        catch (Exception e) when (e is FileNotFoundException or FileLoadException or TypeLoadException)
        {
            return null;
        }
    }

    public static IReadOnlyList<string> Loaded() =>
        AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic).Select(Of).OfType<string>()
            .Distinct().ToList();

    /// <summary>
    /// Finds a folder whose path the build mapped, such as /_/tests/Trails/Maps, under the project folder it belongs to:
    /// the project folder's last segments (tests/Trails) are matched in the mapped path, and what follows (Maps) is the
    /// folder inside the project. A project at the root of the mapped path is tried last.
    /// </summary>
    public static string? Find(string mappedFolder, IEnumerable<string> projectFolders)
    {
        var mapped = Segments(mappedFolder);
        foreach (var project in projectFolders)
        {
            var segments = Segments(project);
            for (var length = segments.Length; length > 0; length--)
            {
                var start = IndexOf(mapped, segments[^length..]);
                if (start >= 0 && Existing(project, mapped[(start + length)..]) is { } found) return found;
            }

            if (mapped.Length > 0 && Existing(project, mapped[1..]) is { } atRoot) return atRoot;
        }

        return null;
    }

    private static string[] Segments(string path) => path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

    private static int IndexOf(string[] path, string[] part)
    {
        for (var i = 0; i + part.Length <= path.Length; i++)
        {
            if (path.AsSpan(i, part.Length).SequenceEqual(part, StringComparer.OrdinalIgnoreCase)) return i;
        }

        return -1;
    }

    private static string? Existing(string project, string[] inside)
    {
        var folder = Path.Combine([project, .. inside]);
        return Directory.Exists(folder) ? folder : null;
    }
}
