using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PDiffy;

/// <summary>
/// Snapshot tests for text and JSON, with the same files and workflow as <see cref="PdfSnapshot"/>: the approved text
/// lives in <c>{name}.verified.{extension}</c>, and a mismatch writes <c>{name}.received.{extension}</c> and an HTML
/// report, then throws <see cref="SnapshotException"/>.
/// </summary>
public static class Snapshot
{
    /// <summary>Set PDIFFY_ACCEPT=1 to accept every received snapshot as the new verified one. Ignored on CI.</summary>
    public const string AcceptVariable = "PDIFFY_ACCEPT";

    /// <summary>
    /// The folder that holds snapshot files, for every kind of snapshot. When null, each snapshot sits next to the test
    /// file that made it. A call's <c>directory</c> argument overrides it.
    /// </summary>
    public static string? Directory { get; set; }

    /// <summary>
    /// Compares text with the verified file exactly: whitespace counts, and only CRLF line endings are read as LF.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <param name="name">The snapshot's file name without extension. Defaults to the test file and method, such as
    /// "TrailMapTests.TrailMap_ShouldMatch".</param>
    /// <param name="configure">What to scrub, such as dates or GUIDs.</param>
    /// <param name="extension">The snapshot files' extension, without the dot.</param>
    /// <param name="directory">The folder for this snapshot, instead of <see cref="Directory"/>.</param>
    /// <param name="testFile">Filled in by the compiler with the calling test's file; leave it out.</param>
    /// <param name="testName">Filled in by the compiler with the calling test's method; leave it out.</param>
    public static Task VerifyAsync(string text, string? name = null, Action<TextSnapshotOptions>? configure = null,
        string extension = "txt", string? directory = null, [CallerFilePath] string testFile = "",
        [CallerMemberName] string testName = "")
    {
        var options = new TextSnapshotOptions();
        configure?.Invoke(options);
        var files = SnapshotWorkflow.Files(name, extension, directory, testFile, testName);
        return SnapshotWorkflow.VerifyAsync(Scrub(text, options), files, "text");
    }

    /// <summary>
    /// Compares JSON with the verified file after writing it the same way every time: two-space indentation, keys in the
    /// order written. Strings (values or keys) that are a GUID or an ISO 8601 timestamp, such as an id or a creation time,
    /// are scrubbed unless <see cref="TextSnapshotOptions.KeepGuids"/> or <see cref="TextSnapshotOptions.KeepDates"/> says
    /// otherwise; dates written for people, and anything inside longer text, stay unless
    /// <see cref="TextSnapshotOptions.ScrubGuids"/> or <see cref="TextSnapshotOptions.ScrubDates"/> asks for them too.
    /// Invalid JSON throws <see cref="SnapshotException"/>.
    /// </summary>
    /// <param name="json">The JSON to check.</param>
    /// <param name="name">The snapshot's file name without extension. Defaults to the test file and method.</param>
    /// <param name="configure">What else to scrub, or what to keep.</param>
    /// <param name="directory">The folder for this snapshot, instead of <see cref="Directory"/>.</param>
    /// <param name="testFile">Filled in by the compiler with the calling test's file; leave it out.</param>
    /// <param name="testName">Filled in by the compiler with the calling test's method; leave it out.</param>
    public static Task VerifyJsonAsync(string json, string? name = null, Action<TextSnapshotOptions>? configure = null,
        string? directory = null, [CallerFilePath] string testFile = "", [CallerMemberName] string testName = "")
    {
        var options = new TextSnapshotOptions().ScrubJsonValues();
        configure?.Invoke(options);
        var files = SnapshotWorkflow.Files(name, "json", directory, testFile, testName);
        return SnapshotWorkflow.VerifyAsync(Scrub(Indent(json, files.Name), options), files, "JSON");
    }

    private static string Scrub(string text, TextSnapshotOptions options) =>
        new Scrubbing(options.Scrubbers).Scrub(text.Replace("\r\n", "\n"));

    // JsonDocument keeps keys in order, duplicates included, and numbers exactly as written.
    private static string Indent(string json, string name)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            using var output = new MemoryStream();
            using (var writer = new Utf8JsonWriter(output, WriterOptions)) document.WriteTo(writer);
            return System.Text.Encoding.UTF8.GetString(output.ToArray()) + "\n";
        }
        catch (JsonException problem)
        {
            throw new SnapshotException($"{name}: the text isn't valid JSON. {problem.Message}");
        }
    }

    // The relaxed encoder leaves accents, quotes and symbols readable instead of escaping them as \uXXXX.
    internal static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true, IndentSize = 2, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
