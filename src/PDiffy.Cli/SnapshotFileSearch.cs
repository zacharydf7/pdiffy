namespace PDiffy.Cli;

/// <summary>Finds snapshot files of one kind, such as received or verified ones, in the files and folders given.</summary>
internal static class SnapshotFileSearch
{
    private static readonly EnumerationOptions AnyCase = new()
    {
        MatchCasing = MatchCasing.CaseInsensitive,
        AttributesToSkip = 0,
    };

    /// <summary>
    /// The files whose names contain <paramref name="part"/>, such as ".received.", each once and in order. Several
    /// targets are allowed because shells expand a pattern like *.received.* into many; no target means the current
    /// folder.
    /// </summary>
    public static List<string> Find(IEnumerable<string> targets, bool includeSubfolders, string part)
    {
        var pattern = $"*{part}*";
        var search = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = targets.DefaultIfEmpty(".").Select(SpelledOnDisk)
            .SelectMany(target => File.Exists(target) ? [target] : Directory.GetFiles(target, pattern, search))
            .Distinct().Order(StringComparer.Ordinal).ToList();
        if (files.FirstOrDefault(file => !Path.GetFileName(file).Contains(part)) is { } wrong)
        {
            throw new ArgumentException($"{wrong} isn't a {part.Trim('.')} snapshot ({pattern}).");
        }

        return files;
    }

    // A target spelled as the file system has it, so tests/Snaps and tests/snaps reach the same files where names
    // ignore case (Windows, and macOS by default). A name is respelled only when the volume finds it as typed, so where
    // names respect case a wrong-case target still isn't found. Files found in a folder are already spelled this way.
    internal static string SpelledOnDisk(string target)
    {
        var path = Path.GetFullPath(target);
        var root = Path.GetPathRoot(path)!;
        var spelled = OperatingSystem.IsWindows() ? root.ToUpperInvariant() : root;
        var parts = path[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var typed = Path.Combine(spelled, part);
            var names = Path.Exists(typed)
                ? Directory.EnumerateFileSystemEntries(spelled, part, AnyCase).Select(Path.GetFileName).ToList()
                : [];
            spelled = names.Count == 1 ? Path.Combine(spelled, names[0]!) : typed;
        }

        return spelled;
    }
}
