using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PDiffy.Cli;

/// <summary>
/// Reads snapshot files as they were at an earlier commit, branch or tag, with git. Git lists each target's folder as
/// spelled on disk, relative to it, so a file's path matches the one <see cref="SnapshotFileSearch"/> finds.
/// </summary>
internal static class GitSnapshots
{
    /// <summary>
    /// Each file's full path, as it would be on disk, and its lines; or the problem, such as git missing from the path
    /// or a commit it doesn't know, when they can't be read.
    /// </summary>
    public static (List<(string File, string[] Lines)>? Files, string? Problem) At(string reference,
        IReadOnlyList<string> targets, bool includeSubfolders, string part)
    {
        var files = new List<(string File, string[] Lines)>();
        foreach (var target in targets.DefaultIfEmpty(".").Select(SnapshotFileSearch.SpelledOnDisk))
        {
            // A file target chooses itself; a folder the files in it, and with includeSubfolders those under it.
            var isFile = File.Exists(target);
            var folder = isFile ? Path.GetDirectoryName(target)! : target;
            var (listing, problem) = Git(folder, null, "ls-tree", "-r", "-z", "--name-only", reference);
            if (listing is null) return (null, problem);

            var paths = Encoding.UTF8.GetString(listing).Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(path => isFile ? path == Path.GetFileName(target)
                    : Path.GetFileName(path).Contains(part) && (includeSubfolders || !path.Contains('/')))
                .ToList();
            var requests = string.Concat(paths.Select(path => $"{reference}:./{path}\n"));
            (var contents, problem) = Git(folder, requests, "cat-file", "--batch");
            if (contents is null) return (null, problem);

            files.AddRange(Blobs(contents).Select((text, index) =>
                (Path.GetFullPath(Path.Combine(folder, paths[index])), Lines(text))));
        }

        return (files.DistinctBy(file => file.File).ToList(), null);
    }

    // As File.ReadAllLines reads a file on disk: without a byte order mark, and with LF or CRLF line ends.
    private static string[] Lines(string text) =>
        text.TrimStart('\uFEFF').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();

    // cat-file --batch writes each file as "<id> blob <size>\n", the file's bytes, then "\n".
    private static IEnumerable<string> Blobs(byte[] output)
    {
        var at = 0;
        while (at < output.Length)
        {
            var end = Array.IndexOf(output, (byte)'\n', at);
            var size = int.Parse(Encoding.ASCII.GetString(output, at, end - at).Split(' ')[2]);
            yield return Encoding.UTF8.GetString(output, end + 1, size);
            at = end + 1 + size + 1;
        }
    }

    // The output, or the problem: git's first line of error, or that git isn't on the path.
    private static (byte[]? Output, string? Problem) Git(string folder, string? input, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = folder,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        try
        {
            using var git = Process.Start(start)!;
            var error = git.StandardError.ReadToEndAsync();
            var writing = Task.Run(() =>
            {
                git.StandardInput.Write(input ?? "");
                git.StandardInput.Close();
            });
            using var output = new MemoryStream();
            git.StandardOutput.BaseStream.CopyTo(output);
            writing.Wait();
            git.WaitForExit();
            var message = error.Result.Split('\n')[0].Trim();
            return git.ExitCode == 0 ? (output.ToArray(), null) : (null, message);
        }
        catch (Win32Exception)
        {
            return (null, "git isn't on the path");
        }
    }
}
