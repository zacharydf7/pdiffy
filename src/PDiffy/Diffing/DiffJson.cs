using System.Text;
using System.Text.Json;

namespace PDiffy.Diffing;

/// <summary>Writes a diff's changes as versioned JSON, for scripts and tools outside .NET.</summary>
internal static class DiffJson
{
    // Goes up only when a field is renamed, removed or changes meaning; new fields can be added without it.
    private const int Version = 1;

    public static string Write(SnapshotDiff diff)
    {
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, Snapshot.WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteStartArray("changes");
            foreach (var change in diff.Changes)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", change.Kind.ToString());
                writer.WriteString("section", diff.SectionOf(change));
                writer.WriteString("path", change.Path);
                if (change.Page is int page) writer.WriteNumber("page", page);
                else writer.WriteNull("page");
                writer.WriteString("detail", change.Detail);
                writer.WriteBoolean("affectsAccessibility", change.AffectsAccessibility);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(output.ToArray()) + "\n";
    }
}
