using System.Text;

namespace PDiffy;

/// <summary>
/// Reads a document the way generators hand it over: from the stream's start wherever it was left, with its position put
/// back afterwards, so it can be read again. A stream that isn't already in memory, such as a file, is copied first, so a
/// failure to read it throws its own exception instead of being taken for a damaged document. Input that plainly isn't
/// the expected format fails with a message that says what it is.
/// </summary>
internal static class InputStream
{
    public static T Read<T>(Stream stream, DocumentFormat format, Func<Stream, T> read)
    {
        // A stream that can't seek is read from where it is, since it can't go back to its start.
        var position = stream.CanSeek ? stream.Position : (long?)null;
        try
        {
            if (position is not null) stream.Position = 0;
            if (stream is MemoryStream memory) return ReadFromStart(memory, format, read);

            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            copy.Position = 0;
            return ReadFromStart(copy, format, read);
        }
        finally
        {
            if (position is long start) stream.Position = start;
        }
    }

    private static T ReadFromStart<T>(Stream stream, DocumentFormat format, Func<Stream, T> read)
    {
        var start = new byte[512];
        var length = stream.ReadAtLeast(start, start.Length, throwOnEndOfStream: false);
        stream.Position = 0;
        if (Problem(start.AsSpan(0, length), format) is { } problem)
        {
            throw new InvalidDataException(problem);
        }

        return read(stream);
    }

    private static string? Problem(ReadOnlySpan<byte> start, DocumentFormat format)
    {
        var expected = format == DocumentFormat.Pdf ? "PDF" : "Word document";
        if (start.IsEmpty) return $"The stream is empty, so there's no {expected} to read. Was it written and flushed?";
        if (format == DocumentFormat.Pdf && start.StartsWith("PK\u0003\u0004"u8))
        {
            return "This is a ZIP file, not a PDF. If it's a Word document, use DocxDocument or "
                + "Snapshot.VerifyDocxAsync.";
        }

        if (format == DocumentFormat.Docx && start.StartsWith("%PDF"u8))
        {
            return "This is a PDF, not a Word document. Use PdfUaDocument or Snapshot.VerifyPdfAsync.";
        }

        var text = Encoding.Latin1.GetString(start.StartsWith("\uFEFF"u8) ? start[3..] : start).TrimStart(' ', '\t', '\r', '\n');
        if (text.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase) || text.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
        {
            return $"This is a web page, not a {expected}. The generator may have returned an error page.";
        }

        return null;
    }
}

internal enum DocumentFormat
{
    Pdf,
    Docx,
}
