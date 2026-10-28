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
        Key(line, Field.LinkTargets, "dest", annotation.HasDestination ? "yes" : null);
        QuotedKey(line, Field.AnnotationContents, "contents", annotation.Contents);
        Key(line, "tu", QuoteIfPresent(annotation.AccessibleName));
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
