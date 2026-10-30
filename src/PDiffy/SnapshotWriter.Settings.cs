using System.Text;

namespace PDiffy;

internal sealed partial class SnapshotWriter
{
    // One line of settings, then a line per viewer preference, layer and embedded file.
    private void WriteSettings(PdfSettings settings)
    {
        var line = new StringBuilder();
        Key(line, PdfDetail.PdfVersion, "version", settings.PdfVersion ?? Missing);
        Key(line, PdfDetail.PdfVersion, "catalog-version", settings.CatalogVersion);
        Key(line, "page-mode", settings.PageMode);
        Key(line, "page-layout", settings.PageLayout);
        QuotedKey(line, PdfDetail.OpenAction, "open-action", settings.OpenAction);
        line.Append($" javascript={YesNo(settings.HasJavaScript)} additional-actions={YesNo(settings.HasAdditionalActions)}");
        line.Append($" encrypted={YesNo(settings.IsEncrypted)}");
        if (settings.Permissions is { } permissions) Key(line, PdfDetail.Permissions, "permissions", FlagNames(permissions) ?? "none");
        WriteKeys(line);

        foreach (var preference in settings.ViewerPreferences) Line(1, $"viewer {preference.Name}={preference.Value}");
        foreach (var layer in settings.Layers) Line(1, $"layer {Quote(layer.Name)} {(layer.VisibleByDefault ? "visible" : "hidden")}");
        foreach (var file in settings.EmbeddedFiles) Line(1, FormatEmbeddedFile(file));
    }

    private string FormatEmbeddedFile(PdfEmbeddedFile file)
    {
        var line = new StringBuilder($"embedded-file {Quote(file.Name)}");
        Key(line, "file", QuoteIfPresent(file.FileName));
        Key(line, "description", QuoteIfPresent(file.Description));
        Key(line, "relationship", file.Relationship);
        Key(line, PdfDetail.EmbeddedFileContents, "size", file.Size?.ToString());
        Key(line, PdfDetail.EmbeddedFileContents, "hash", file.Hash);
        Key(line, PdfDetail.EmbeddedFileContents, "unreadable", file.Unreadable ? "yes" : null);
        return line.ToString();
    }
}
