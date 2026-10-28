
namespace PDiffy.Tests.Unit.Pdfs;

/// <summary>Builds small tagged PDFs for tests by writing the PDF objects directly.</summary>
internal sealed class TestPdf
{
    private readonly List<TestPage> _pages = [];
    private readonly List<TestElement> _structure = [];
    private readonly List<(string Custom, string Standard)> _roleMap = [];
    private readonly List<(string Title, int Page)> _bookmarks = [];
    private readonly List<(string Name, string Entries, int Page)> _formFields = [];
    private readonly List<string> _objects = [];
    private string _catalogEntries = "";

    public string? Lang { get; init; } = "en-US";
    public string? Title { get; init; }
    public string? PdfUaPart { get; init; }

    /// <summary>Raw XMP properties, such as "&lt;dc:language&gt;...&lt;/dc:language&gt;", using the rdf, pdfuaid, pdfaid and dc prefixes.</summary>
    public string? Xmp { get; init; }

    /// <summary>Raw entries added to the Info dictionary, after /Title.</summary>
    public string InfoEntries { get; init; } = "";

    /// <summary>The BaseFont of the page font F1. The font isn't embedded, so PdfPig only recognizes standard names.</summary>
    public string BaseFont { get; init; } = "Helvetica";

    /// <summary>Puts the font resources on the Pages node, so every page inherits them instead of having its own.</summary>
    public bool ResourcesOnPagesNode { get; init; }

    public TestPage AddPage(double width = 612, double height = 792, int rotate = 0, string? tabs = "S")
    {
        var page = new TestPage(width, height, rotate, tabs);
        _pages.Add(page);
        return page;
    }

    public TestPdf Structure(params TestElement[] elements)
    {
        _structure.AddRange(elements);
        return this;
    }

    public TestPdf RoleMap(string custom, string standard)
    {
        _roleMap.Add((custom, standard));
        return this;
    }

    public TestPdf Bookmark(string title, int page)
    {
        _bookmarks.Add((title, page));
        return this;
    }

    /// <summary>
    /// A form field merged with its widget annotation on a page. <paramref name="entries"/> are raw field entries and must
    /// include the type, e.g. "/FT /Tx /TU (Street) /V (12 Main St)".
    /// </summary>
    public TestPdf FormField(string name, string entries, int page = 1)
    {
        _formFields.Add((name, entries, page));
        return this;
    }

    /// <summary>Raw entries added to the catalog dictionary, e.g. "/PageLabels &lt;&lt; ... &gt;&gt;".</summary>
    public TestPdf CatalogEntries(string entries)
    {
        _catalogEntries += " " + entries;
        return this;
    }

    public PdfUaDocument Load() => PdfUaDocument.Load(new MemoryStream(Build()));

    public byte[] Build()
    {
        _objects.Clear();
        var catalog = Reserve();
        var pagesNode = Reserve();
        var font = Add($"<< /Type /Font /Subtype /Type1 /BaseFont /{BaseFont} /Encoding /WinAnsiEncoding >>");
        var pageObjects = _pages.Select(_ => Reserve()).ToList();
        var annotations = _pages.Select(_ => new List<int>()).ToList();

        var catalogEntries = $"/Type /Catalog /Pages {pagesNode} 0 R /MarkInfo << /Marked true >> "
            + "/ViewerPreferences << /DisplayDocTitle true >>" + _catalogEntries;
        if (Lang is not null) catalogEntries += $" /Lang ({Lang})";

        if (_structure.Count > 0)
        {
            var root = Reserve();
            var kids = _structure.Select(element => $"{AddElement(element, root, pageObjects, annotations)} 0 R");
            var roleMap = string.Concat(_roleMap.Select(r => $" /{r.Custom} /{r.Standard}"));
            Set(root, $"<< /Type /StructTreeRoot /K [{string.Join(" ", kids)}] /RoleMap <<{roleMap} >> >>");
            catalogEntries += $" /StructTreeRoot {root} 0 R";
        }

        for (var i = 0; i < _pages.Count; i++)
        {
            annotations[i].AddRange(_pages[i].Annotations.Select(entries => Add($"<< /Type /Annot {entries} >>")));
        }

        if (_formFields.Count > 0)
        {
            var fields = new List<int>();
            foreach (var (name, entries, page) in _formFields)
            {
                var field = Add($"<< /Type /Annot /Subtype /Widget /T ({name}) /Rect [72 300 272 320] {entries} >>");
                annotations[page - 1].Add(field);
                fields.Add(field);
            }

            catalogEntries += $" /AcroForm << /Fields [{string.Join(" ", fields.Select(f => $"{f} 0 R"))}] >>";
        }

        if (_bookmarks.Count > 0) catalogEntries += $" /Outlines {AddOutlines(pageObjects)} 0 R";
        var xmpProperties = (PdfUaPart is null ? "" : $"<pdfuaid:part>{PdfUaPart}</pdfuaid:part>") + Xmp;
        if (xmpProperties.Length > 0) catalogEntries += $" /Metadata {AddXmp(xmpProperties)} 0 R";

        for (var i = 0; i < _pages.Count; i++)
        {
            var content = Add(Stream(_pages[i].Content));
            Set(pageObjects[i], _pages[i].ToDictionary(pagesNode, ResourcesOnPagesNode ? null : font, content, annotations[i], this));
        }

        var inheritedResources = ResourcesOnPagesNode ? $" /Resources << /Font << /F1 {font} 0 R >> >>" : "";
        var pageRefs = string.Join(" ", pageObjects.Select(p => $"{p} 0 R"));
        Set(pagesNode, $"<< /Type /Pages /Kids [{pageRefs}] /Count {_pages.Count}{inheritedResources} >>");
        Set(catalog, $"<< {catalogEntries} >>");
        var titleEntry = Title is null ? "" : $"/Title ({Title}) ";
        var info = Add($"<< {titleEntry}{InfoEntries} >>");
        return RawPdf.Build(_objects, catalog, $"/Info {info} 0 R");
    }

    internal int Add(string body)
    {
        _objects.Add(body);
        return _objects.Count;
    }

    internal static string Stream(string content) => $"<< /Length {content.Length} >>\nstream\n{content}\nendstream";

    private int Reserve() => Add("");

    private void Set(int number, string body) => _objects[number - 1] = body;

    private int AddElement(TestElement element, int parent, List<int> pageObjects, List<List<int>> annotations)
    {
        var number = Reserve();
        var pageIndex = (element.Page ?? 1) - 1;
        var page = pageObjects[pageIndex];
        var kids = new List<string>();
        foreach (var kid in element.Kids)
        {
            switch (kid)
            {
                case int mcid:
                    kids.Add(mcid.ToString());
                    break;
                case TestElement child:
                    kids.Add($"{AddElement(child, number, pageObjects, annotations)} 0 R");
                    break;
                case TestMcr reference:
                    kids.Add($"<< /Type /MCR /MCID {reference.Mcid} /Pg {pageObjects[reference.Page - 1]} 0 R >>");
                    break;
                case TestLink link:
                    var annotation = Add($"<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /URI /URI ({link.Uri}) >> "
                        + $"/Contents ({link.Contents}) >>");
                    annotations[pageIndex].Add(annotation);
                    kids.Add($"<< /Type /OBJR /Obj {annotation} 0 R /Pg {page} 0 R >>");
                    break;
            }
        }

        var pageEntry = element.Page is null ? "" : $" /Pg {page} 0 R";
        Set(number, $"<< /Type /StructElem /S /{element.Type} /P {parent} 0 R{pageEntry} /K [{string.Join(" ", kids)}] "
            + $"{element.Entries} >>");
        return number;
    }

    private int AddOutlines(List<int> pageObjects)
    {
        var outlines = Reserve();
        var items = _bookmarks.Select(_ => Reserve()).ToList();
        for (var i = 0; i < items.Count; i++)
        {
            var (title, page) = _bookmarks[i];
            var previous = i > 0 ? $" /Prev {items[i - 1]} 0 R" : "";
            var next = i < items.Count - 1 ? $" /Next {items[i + 1]} 0 R" : "";
            Set(items[i], $"<< /Title ({title}) /Parent {outlines} 0 R /Dest [{pageObjects[page - 1]} 0 R /Fit]{previous}{next} >>");
        }

        Set(outlines, $"<< /Type /Outlines /First {items[0]} 0 R /Last {items[^1]} 0 R /Count {items.Count} >>");
        return outlines;
    }

    // The rdf:Description declares the rdf, pdfuaid, pdfaid and dc prefixes, so properties can use them directly.
    private int AddXmp(string properties)
    {
        var xml = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
            + "<rdf:Description xmlns:pdfuaid=\"http://www.aiim.org/pdfua/ns/id/\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\" "
            + $"xmlns:dc=\"http://purl.org/dc/elements/1.1/\">{properties}</rdf:Description></rdf:RDF></x:xmpmeta>";
        return Add($"<< /Type /Metadata /Subtype /XML /Length {xml.Length} >>\nstream\n{xml}\nendstream");
    }

}
