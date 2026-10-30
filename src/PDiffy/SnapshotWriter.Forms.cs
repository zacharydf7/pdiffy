using System.Globalization;
using System.Text;

namespace PDiffy;

internal sealed partial class SnapshotWriter
{
    private void WriteAnnotations(IReadOnlyList<PdfAnnotation> annotations)
    {
        if (annotations.Count == 0) Line(1, None);
        foreach (var annotation in annotations)
        {
            var line = new StringBuilder($"p{annotation.Page} {annotation.Subtype} {RoleName(annotation.Role)}");
            Key(line, PdfDetail.Positions, "at", $"{annotation.X},{annotation.Y}");
            Key(line, PdfDetail.Positions, "size", $"{annotation.Width}x{annotation.Height}");
            Key(line, "flags", FlagNames(annotation.Flags));
            AppendAnnotationDetails(line, annotation);
            Line(1, line.ToString());
        }
    }

    // Shared by this section and the structure section, which lists the annotations an element points to.
    private void AppendAnnotationDetails(StringBuilder line, PdfAnnotation annotation)
    {
        QuotedKey(line, PdfDetail.LinkTargets, "uri", annotation.Uri);
        Key(line, PdfDetail.LinkTargets, "action", annotation.Action);
        QuotedKey(line, PdfDetail.LinkTargets, "file", annotation.File);
        if (annotation.Destination is { } destination)
        {
            // A name the PDF doesn't define is printed instead of a page.
            Key(line, PdfDetail.LinkTargets, "dest", destination.Name is null ? PageText(destination) : null);
            QuotedKey(line, PdfDetail.LinkTargets, "dest-name", destination.Name);
            AppendView(line, destination);
        }

        QuotedKey(line, PdfDetail.AnnotationContents, "contents", annotation.Contents);
        Key(line, "tu", QuoteIfPresent(annotation.AccessibleName));
        if (annotation.MediaClip is { } clip)
        {
            Key(line, "media-type", clip.ContentType ?? Missing);
            QuotedKey(line, PdfDetail.AltText, "media-alt", clip.AltText, showMissing: true);
        }
    }

    // p? is a page reference that isn't a page.
    private static string PageText(PdfDestination destination) => $"p{destination.Page?.ToString() ?? "?"}";

    // The view a link or bookmark opens its page at, the view's positions ("-" for one the viewer keeps) and its zoom.
    private void AppendView(StringBuilder line, PdfDestination destination)
    {
        if (options.Ignores(PdfDetail.LinkTargets)) return;
        Key(line, "view", destination.View);
        if (destination.Positions.Count > 0)
        {
            Key(line, PdfDetail.Positions, "view-at", string.Join(",", destination.Positions.Select(p => p?.ToString() ?? "-")));
        }

        Key(line, "zoom", destination.Zoom?.ToString(CultureInfo.InvariantCulture));
    }

    private void WriteForms(PdfFormSettings? form, IReadOnlyList<PdfFormField> fields)
    {
        var settings = form is null ? null : FormSettingsLine(form);
        if (settings is not null) Line(1, settings);
        else if (fields.Count == 0) Line(1, None);
        foreach (var field in fields)
        {
            var line = new StringBuilder($"{KebabCase(field.Type.ToString())} {Quote(field.Name)}");
            if (field.Page is int page) line.Append($" p{page}");
            Key(line, "tu", QuoteIfPresent(field.AccessibleName));
            QuotedKey(line, PdfDetail.FormValues, "value", field.Value);
            if (field.Options.Count > 0) line.Append($" options=[{string.Join(",", field.Options.Select(Quote))}]");
            Key(line, "maxlen", field.MaxLength?.ToString());
            Key(line, "flags", FlagNames(field.Flags));
            QuotedKey(line, PdfDetail.FormValues, "default", field.DefaultValue);
            if (field.ExportValues.Count > 0) line.Append($" exports=[{string.Join(",", field.ExportValues.Select(Quote))}]");
            Key(line, "align", field.Alignment == PdfFormFieldAlignment.Left ? null : KebabCase(field.Alignment.ToString()));
            Key(line, PdfDetail.FontNames, "text-font", field.TextFont);
            Key(line, PdfDetail.FontSizes, "text-size", field.TextSize is { } size ? FontSizeText(size) : null);
            Key(line, PdfDetail.Colors, "text-color", field.TextColor);
            if (field.Scripts.Count > 0)
            {
                var scripts = field.Scripts.Select(script => $"{KebabCase(script.Trigger.ToString())}:{script.Hash ?? "unreadable"}");
                line.Append($" scripts={string.Join(",", scripts)}");
            }

            if (field.Signature is { } signature) AppendSignature(line, signature);
            Line(1, line.ToString());
        }
    }

    // Null when the form sets none of them, so most forms print no line for it.
    private string? FormSettingsLine(PdfFormSettings form)
    {
        var line = new StringBuilder("form");
        if (form.CalculationOrder.Count > 0) line.Append($" calculation-order=[{string.Join(",", form.CalculationOrder.Select(Quote))}]");
        Key(line, "need-appearances", form.NeedsAppearances ? "yes" : null);
        Key(line, "xfa", form.Xfa is { } xfa ? KebabCase(xfa.ToString()) : null);
        return line.Length > "form".Length ? line.ToString() : null;
    }

    // A size of 0 fits the text to the field.
    private static string FontSizeText(double size) => size == 0 ? "auto" : size.ToString(CultureInfo.InvariantCulture);

    private static void AppendSignature(StringBuilder line, PdfSignature signature)
    {
        line.Append(" signed=yes");
        Key(line, "subfilter", signature.SubFilter);
        Key(line, "covers-file", signature.CoversWholeFile is bool covers ? YesNo(covers) : null);
        Key(line, "reason", signature.HasReason ? Present(true) : null);
        Key(line, "location", signature.HasLocation ? Present(true) : null);
        Key(line, "signing-time", signature.HasSigningTime ? Present(true) : null);
        Key(line, "certification", signature.Certification is { } level ? KebabCase(level.ToString()) : null);
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
