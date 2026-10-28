using System.Text;

namespace PDiffy;

internal sealed partial class SnapshotWriter
{
    // One line of settings, then a line per viewer preference, layer and embedded file.
    private void WriteSettings(DocumentSettings settings)
    {
        var line = new StringBuilder();
        Key(line, Field.PdfVersion, "version", settings.PdfVersion ?? Missing);
        Key(line, Field.PdfVersion, "catalog-version", settings.CatalogVersion);
        Key(line, "page-mode", settings.PageMode);
        Key(line, "page-layout", settings.PageLayout);
        QuotedKey(line, Field.OpenAction, "open-action", settings.OpenAction);
        line.Append($" javascript={YesNo(settings.HasJavaScript)} additional-actions={YesNo(settings.HasAdditionalActions)}");
        line.Append($" encrypted={YesNo(settings.IsEncrypted)}");
        if (settings.Permissions is { } permissions) Key(line, Field.Permissions, "permissions", FlagNames(permissions) ?? "none");
        WriteKeys(line);

        foreach (var preference in settings.ViewerPreferences) Line(1, $"viewer {preference.Name}={preference.Value}");
        foreach (var layer in settings.Layers) Line(1, $"layer {Quote(layer.Name)} {(layer.VisibleByDefault ? "visible" : "hidden")}");
        foreach (var file in settings.EmbeddedFiles) Line(1, FormatEmbeddedFile(file));
    }

    private string FormatEmbeddedFile(EmbeddedFile file)
    {
        var line = new StringBuilder($"embedded-file {Quote(file.Name)}");
        Key(line, "file", QuoteIfPresent(file.FileName));
        Key(line, "description", QuoteIfPresent(file.Description));
        Key(line, "relationship", file.Relationship);
        Key(line, Field.EmbeddedFileContents, "size", file.Size?.ToString());
        Key(line, Field.EmbeddedFileContents, "hash", file.Hash);
        Key(line, Field.EmbeddedFileContents, "unreadable", file.Unreadable ? "yes" : null);
        return line.ToString();
    }
}
