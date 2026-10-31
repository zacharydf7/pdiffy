using PDiffy.Diffing;

namespace PDiffy;

/// <summary>Applies <see cref="DocxCheck"/> rules to a document's model, one method per rule.</summary>
internal sealed class DocxChecker(DocxDocument document)
{
    private readonly List<DocxCheckFinding> _findings = [];

    public static IReadOnlyList<DocxCheckFinding> Run(DocxDocument document, IEnumerable<DocxCheck> checks)
    {
        var checker = new DocxChecker(document);
        foreach (var check in checks.Distinct().Order()) checker.Apply(check);
        return checker._findings;
    }

    private void Apply(DocxCheck check)
    {
        switch (check)
        {
            case DocxCheck.ImageAltText: ImageAltText(); break;
            default: throw new ArgumentOutOfRangeException(nameof(check), check, "Not a DocxCheck value.");
        }
    }

    // Every image the snapshot prints, in its order, with the path a diff shows for its line. A shape with text in it
    // is read as its text, and an ActiveX control by its own label, so neither needs alt text; a picture does, even
    // grouped with a caption.
    private void ImageAltText()
    {
        var (snapshot, images) = DocxSnapshotWriter.WriteWithImages(document, new DocxSnapshotOptions());
        var paths = SnapshotTree.PathsByStart(snapshot);
        foreach (var (image, start) in images)
        {
            var readAsText = image.Kind != DocxGraphicKinds.Image
                && image.TextBox.OfType<DocxParagraph>().Any(paragraph => !string.IsNullOrWhiteSpace(paragraph.Text));
            if (image.AltText is not null || image.Decorative || readAsText) continue;
            if (image.Kind == DocxGraphicKinds.Control) continue;

            var kind = $"{char.ToUpperInvariant(image.Kind[0])}{image.Kind[1..]}";
            Add(DocxCheck.ImageAltText, paths[start], $"{kind} has no alt text");
        }
    }

    private void Add(DocxCheck check, string path, string message)
    {
        var (category, severity, reference) = RuleOf(check);
        _findings.Add(new DocxCheckFinding
        {
            Check = check, Category = category, Severity = severity, Reference = reference, Path = path,
            Message = message,
        });
    }

    // Each rule's family and the WCAG 2.2 success criterion it comes from, since PDF/UA doesn't cover Word.
    internal static DocxCheckRule RuleOf(DocxCheck check) => check switch
    {
        DocxCheck.ImageAltText => new(DocxCheckCategory.AltText, CheckSeverity.Error, "WCAG 2.2 1.1.1"),
        _ => throw new ArgumentOutOfRangeException(nameof(check), check, "Every check needs a rule."),
    };
}

/// <summary>A <see cref="DocxCheck"/>'s family, how much its findings matter and the criterion it comes from.</summary>
internal readonly record struct DocxCheckRule(DocxCheckCategory Category, CheckSeverity Severity, string Reference);
