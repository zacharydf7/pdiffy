namespace PDiffy.Reading;

/// <summary>Limits that keep a malformed or hostile file from exhausting memory or the stack.</summary>
internal static class ReadLimits
{
    /// <summary>
    /// How deep PDiffy follows nested content: structure elements, bookmarks, and XML elements in a DOCX part or XMP.
    /// Readers recurse into each level, and real documents stay far below this.
    /// </summary>
    public const int MaxDepth = 256;
}
