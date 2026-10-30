using System.Globalization;
using System.Text;

namespace PDiffy;

internal sealed partial class SnapshotWriter
{
    private void WriteAnnotations(IReadOnlyList<Annotation> annotations)
    {
        if (annotations.Count == 0) Line(1, None);
        foreach (var annotation in annotations)
        {
            var line = new StringBuilder($"p{annotation.Page} {annotation.Subtype} {RoleName(annotation.Role)}");
            Key(line, Field.Positions, "at", $"{annotation.X},{annotation.Y}");
            Key(line, Field.Positions, "size", $"{annotation.Width}x{annotation.Height}");
            Key(line, "flags", FlagNames(annotation.Flags));
            AppendAnnotationDetails(line, annotation);
            Line(1, line.ToString());
        }
    }

    // Shared by this section and the structure section, which lists the annotations an element points to.
    private void AppendAnnotationDetails(StringBuilder line, Annotation annotation)
    {
        QuotedKey(line, Field.LinkTargets, "uri", annotation.Uri);
        Key(line, Field.LinkTargets, "action", annotation.Action);
        QuotedKey(line, Field.LinkTargets, "file", annotation.File);
        if (annotation.Destination is { } destination)
        {
            // A name the PDF doesn't define is printed instead of a page.
            Key(line, Field.LinkTargets, "dest", destination.Name is null ? PageText(destination) : null);
            QuotedKey(line, Field.LinkTargets, "dest-name", destination.Name);
            AppendView(line, destination);
        }

        QuotedKey(line, Field.AnnotationContents, "contents", annotation.Contents);
        Key(line, "tu", QuoteIfPresent(annotation.AccessibleName));
    }

    // p? is a page reference that isn't a page.
    private static string PageText(Destination destination) => $"p{destination.Page?.ToString() ?? "?"}";

    // The view a link or bookmark opens its page at, the view's positions ("-" for one the viewer keeps) and its zoom.
    private void AppendView(StringBuilder line, Destination destination)
    {
        if (options.Ignores(Field.LinkTargets)) return;
        Key(line, "view", destination.View);
        if (destination.Positions.Count > 0)
        {
            Key(line, Field.Positions, "view-at", string.Join(",", destination.Positions.Select(p => p?.ToString() ?? "-")));
        }

        Key(line, "zoom", destination.Zoom?.ToString(CultureInfo.InvariantCulture));
    }

    private void WriteFormFields(IReadOnlyList<FormField> fields)
    {
        if (fields.Count == 0) Line(1, None);
        foreach (var field in fields)
        {
            var line = new StringBuilder($"{KebabCase(field.Type.ToString())} {Quote(field.Name)}");
            if (field.Page is int page) line.Append($" p{page}");
            Key(line, "tu", QuoteIfPresent(field.AccessibleName));
            QuotedKey(line, Field.FormValues, "value", field.Value);
            if (field.Options.Count > 0) line.Append($" options=[{string.Join(",", field.Options.Select(Quote))}]");
            Key(line, "maxlen", field.MaxLength?.ToString());
            Key(line, "flags", FlagNames(field.Flags));
            Line(1, line.ToString());
        }
    }

    // Set flags as kebab-case names, e.g. "print,no-zoom"; null when none are set.
    private static string? FlagNames<T>(T flags) where T : struct, Enum
    {
        var set = Enum.GetValues<T>().Where(flag => Convert.ToInt32(flag) != 0 && flags.HasFlag(flag));
        var joined = string.Join(",", set.Select(flag => KebabCase(flag.ToString())));
        return joined.Length == 0 ? null : joined;
    }

    private static string KebabCase(string pascalCase)
    {
        var sb = new StringBuilder();
        foreach (var c in pascalCase)
        {
            if (char.IsUpper(c) && sb.Length > 0) sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }
}
