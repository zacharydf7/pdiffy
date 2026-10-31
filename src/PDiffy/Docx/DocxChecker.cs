using PDiffy.Diffing;

namespace PDiffy;

/// <summary>Applies <see cref="DocxCheck"/> rules to a document's model, one method per rule.</summary>
internal sealed class DocxChecker(DocxDocument document)
{
    private readonly List<DocxCheckFinding> _findings = [];
    private List<(object Item, string Path)>? _items;

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
            case DocxCheck.TableHeaders: TableHeaders(); break;
            case DocxCheck.DocumentLanguage: DocumentLanguage(); break;
            default: throw new ArgumentOutOfRangeException(nameof(check), check, "Not a DocxCheck value.");
        }
    }

    // A shape with text in it is read as its text, and an ActiveX control by its own label, so neither needs alt text;
    // a picture does, even grouped with a caption.
    private void ImageAltText()
    {
        foreach (var (item, path) in Items)
        {
            if (item is not DocxImage image) continue;

            var readAsText = image.Kind != DocxGraphicKinds.Image
                && image.TextBox.OfType<DocxParagraph>().Any(paragraph => !string.IsNullOrWhiteSpace(paragraph.Text));
            if (image.AltText is not null || image.Decorative || readAsText) continue;
            if (image.Kind == DocxGraphicKinds.Control) continue;

            var kind = $"{char.ToUpperInvariant(image.Kind[0])}{image.Kind[1..]}";
            Add(DocxCheck.ImageAltText, path, $"{kind} has no alt text");
        }
    }

    // A table of one row has nothing to label. One used only for layout needs no header row either, but it can't be
    // told from a data table, so the rule is a warning.
    private void TableHeaders()
    {
        foreach (var (item, path) in Items)
        {
            if (item is not DocxTable table || table.Rows.Count < 2 || table.Rows[0].IsHeader) continue;

            Add(DocxCheck.TableHeaders, path, "Table has no header row");
        }
    }

    // Text gets its language from its run, its styles or the document's defaults, so a document whose defaults set
    // none is found only when some text gets none from anywhere. It's one finding for the whole document, since the
    // fix is usually the defaults.
    private void DocumentLanguage()
    {
        var paragraphCount = document.EveryParagraph().Count(paragraph => paragraph.Spans
            .Any(span => span.Format.Language is null && !string.IsNullOrWhiteSpace(span.Text)));
        if (paragraphCount == 0) return;

        var paragraphText = paragraphCount == 1 ? "1 paragraph has" : $"{paragraphCount} paragraphs have";
        Add(DocxCheck.DocumentLanguage, SnapshotWriter.Name(DocxSection.Document),
            $"Document has no language, and {paragraphText} text without one");
    }

    // Every image and table the snapshot prints, in its order, with the path a diff shows for its line. Written once,
    // since several rules walk them.
    private List<(object Item, string Path)> Items
    {
        get
        {
            if (_items is not null) return _items;

            var (snapshot, items) = DocxSnapshotWriter.WriteWithItems(document, new DocxSnapshotOptions());
            var paths = SnapshotTree.PathsByStart(snapshot);
            return _items = items.ConvertAll(item => (item.Item, paths[item.Start]));
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
        DocxCheck.TableHeaders => new(DocxCheckCategory.Tables, CheckSeverity.Warning, "WCAG 2.2 1.3.1"),
        DocxCheck.DocumentLanguage => new(DocxCheckCategory.Language, CheckSeverity.Error, "WCAG 2.2 3.1.1"),
        _ => throw new ArgumentOutOfRangeException(nameof(check), check, "Every check needs a rule."),
    };
}

/// <summary>A <see cref="DocxCheck"/>'s family, how much its findings matter and the criterion it comes from.</summary>
internal readonly record struct DocxCheckRule(DocxCheckCategory Category, CheckSeverity Severity, string Reference);
