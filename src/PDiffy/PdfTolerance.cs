namespace PDiffy;

/// <summary>
/// The numbers in a PDF snapshot that <see cref="PdfSnapshotOptions.Tolerate"/> can let differ a little.
/// </summary>
public enum PdfTolerance
{
    /// <summary>Where things are, and the size of images, paths and annotations, in points.</summary>
    Positions,

    /// <summary>The size text is drawn at, in points.</summary>
    FontSizes,

    /// <summary>The width of stroked paths, in points.</summary>
    LineWidths,
}
