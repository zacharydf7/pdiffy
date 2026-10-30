namespace PDiffy;

/// <summary>Tells a PDF or a Word document apart from a snapshot file, as the command line does.</summary>
internal static class DocumentFiles
{
    public static bool IsDocx(string path) => Path.GetExtension(path).Equals(".docx",
        StringComparison.OrdinalIgnoreCase);

    // PDF readers accept the %PDF- header anywhere in the first 1024 bytes, after a byte-order mark or other junk.
    public static bool IsPdf(string path)
    {
        using var file = File.OpenRead(path);
        var start = new byte[1024];
        var length = file.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
        return start.AsSpan(0, length).IndexOf("%PDF-"u8) >= 0;
    }
}
