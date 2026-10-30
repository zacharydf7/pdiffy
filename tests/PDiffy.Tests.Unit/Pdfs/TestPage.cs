using System.Text;

namespace PDiffy.Tests.Unit.Pdfs;

/// <summary>A page of a <see cref="TestPdf"/>. Each text call draws one line, lower down the page than the last.</summary>
internal sealed class TestPage(double width, double height, int rotate, string? tabs)
{
    private readonly StringBuilder _content = new();
    private readonly List<string> _imagePixels = [];
    private int _referenceForms;
    private readonly List<string> _annotations = [];
    private readonly List<string> _layers = [];
    private string _pageEntries = "";
    private int _nextLineY = 720;

    public bool HasDamagedFont { get; private set; }

    /// <summary>Raw entries added to the page dictionary, such as "/CropBox [0 0 600 780]".</summary>
    public TestPage PageEntries(string entries)
    {
        _pageEntries += " " + entries;
        return this;
    }

    public string Content => _content.ToString();

    /// <summary>Raw entries of the untagged annotations added with <see cref="Annotation"/>.</summary>
    public IReadOnlyList<string> Annotations => _annotations;

    /// <summary>An annotation no structure element points to, from raw entries such as "/Subtype /Text /Rect [0 0 10 10]".</summary>
    public TestPage Annotation(string entries)
    {
        _annotations.Add(entries);
        return this;
    }

    /// <summary>Text inside marked content with an MCID, which a structure element can point to.</summary>
    public TestPage Tagged(int mcid, string text, string tag = "P") => Raw($"/{tag} << /MCID {mcid} >> BDC {TextLine(text)} EMC");

    public TestPage Artifact(string text) => Raw($"/Artifact BMC {TextLine(text)} EMC");

    /// <summary>An artifact that says what kind it is, such as "/Type /Pagination /Subtype /Footer".</summary>
    public TestPage Artifact(string text, string properties) => Raw($"/Artifact << {properties} >> BDC {TextLine(text)} EMC");

    public TestPage Untagged(string text) => Raw(TextLine(text));

    /// <summary>
    /// A one-pixel gray image at 72,<paramref name="y"/>. <paramref name="gray"/> is the pixel's value as two hex digits.
    /// </summary>
    public TestPage TaggedImage(int mcid, string gray = "80", int y = 600) =>
        Raw($"/Figure << /MCID {mcid} >> BDC {DrawImage(gray, y)} EMC");

    public TestPage UntaggedImage(string gray = "80") => Raw(DrawImage(gray));

    /// <summary>A reference XObject in the page's resources: a form that draws a page of another file (/Ref).</summary>
    public TestPage ReferenceForm()
    {
        _referenceForms++;
        return this;
    }

    public TestPage ArtifactImage(string gray = "80") => Raw($"/Artifact BMC {DrawImage(gray)} EMC");

    /// <summary>A filled rectangle with no marked content around it.</summary>
    public TestPage UntaggedPath() => Raw("72 500 100 20 re f");

    public TestPage ArtifactPath() => Raw("/Artifact BMC 72 480 100 1 re f EMC");

    /// <summary>A clipping rectangle, which draws nothing.</summary>
    public TestPage ClippingPath() => Raw("q 72 460 100 20 re W n Q");

    /// <summary>A rectangle that is filled and then also used as the clip, so it is drawn.</summary>
    public TestPage FilledClippingPath() => Raw("q 72 440 100 20 re W f Q");

    /// <summary>What <paramref name="draw"/> adds, inside optional content (/OC) for a layer of that name.</summary>
    public TestPage InLayer(string layer, Action<TestPage> draw)
    {
        _layers.Add(layer);
        Raw($"/OC /L{_layers.Count} BDC");
        draw(this);
        return Raw("EMC");
    }

    /// <summary>Raw content-stream operators, for marked content the helpers don't cover.</summary>
    public TestPage Raw(string operators)
    {
        _content.Append(operators).Append('\n');
        return this;
    }

    /// <summary>Adds a Type0 font PdfPig can't parse (a ToUnicode CMap cut off mid-range and a bogus font program), so loading the page throws.</summary>
    public TestPage WithDamagedFont()
    {
        HasDamagedFont = true;
        return this;
    }

    public string TextLine(string text)
    {
        var line = $"BT /F1 12 Tf 72 {_nextLineY} Td ({text}) Tj ET";
        _nextLineY -= 20;
        return line;
    }

    /// <param name="font">The F1 font object, or null when the page inherits its resources from the Pages node.</param>
    internal string ToDictionary(int parent, int? font, int content, IReadOnlyList<int> annotations, TestPdf pdf)
    {
        var resources = "";
        if (font is not null)
        {
            var fonts = $"/F1 {font} 0 R";
            if (HasDamagedFont)
            {
                fonts += $" /F2 {AddDamagedFont(pdf)} 0 R";
            }

            var referenceForm = pdf.StreamObject(
                "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Ref << /F (map.pdf) /Page 0 >>", "");
            var images = string.Concat(_imagePixels.Select((gray, i) => $" /Im{i + 1} {AddImage(pdf, gray)} 0 R"))
                + string.Concat(Enumerable.Range(1, _referenceForms)
                    .Select(i => $" /Ref{i} {pdf.Add(referenceForm)} 0 R"));
            var xObjects = images.Length == 0 ? "" : $" /XObject <<{images} >>";
            var groups = string.Concat(_layers.Select((name, i) =>
                $" /L{i + 1} {pdf.Add($"<< /Type /OCG /Name ({name}) >>")} 0 R"));
            var properties = groups.Length == 0 ? "" : $" /Properties <<{groups} >>";
            resources = $" /Resources << /Font << {fonts} >>{xObjects}{properties} >>";
        }

        var tabsEntry = tabs is null ? "" : $" /Tabs /{tabs}";
        var annotsEntry = annotations.Count == 0 ? "" : $" /Annots [{string.Join(" ", annotations.Select(a => $"{a} 0 R"))}]";
        return $"<< /Type /Page /Parent {parent} 0 R /MediaBox [0 0 {width} {height}] /Rotate {rotate}{resources} "
            + $"/Contents {content} 0 R{tabsEntry}{annotsEntry}{_pageEntries} >>";
    }

    private string DrawImage(string gray, int y = 600)
    {
        _imagePixels.Add(gray);
        return $"q 10 0 0 10 72 {y} cm /Im{_imagePixels.Count} Do Q";
    }

    // Uncompressed, the pixel is written as hex text; compressed, as one Flate-compressed byte. Both decode to the same pixel.
    private static int AddImage(TestPdf pdf, string gray)
    {
        const string Image = "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8";
        return pdf.Add(pdf.CompressStreams ? pdf.StreamObject(Image, ((char)Convert.ToByte(gray, 16)).ToString())
            : $"<< {Image} /Filter /ASCIIHexDecode /Length 3 >>\nstream\n{gray}>\nendstream");
    }

    private static int AddDamagedFont(TestPdf pdf)
    {
        var toUnicode = pdf.Add(TestPdf.Stream(
            "1 begincodespacerange <0000> <FFFF> endcodespacerange 2 beginbfrange <0000> <00ff> [<0041> <0042>"));
        var fontFile = pdf.Add(TestPdf.Stream("not a real font program"));
        var descriptor = pdf.Add("<< /Type /FontDescriptor /FontName /Damaged /Flags 32 /FontBBox [0 0 1000 1000] "
            + $"/ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 {fontFile} 0 R >>");
        var descendant = pdf.Add("<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Damaged "
            + $"/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {descriptor} 0 R >>");
        return pdf.Add($"<< /Type /Font /Subtype /Type0 /BaseFont /Damaged /Encoding /Identity-H "
            + $"/DescendantFonts [{descendant} 0 R] /ToUnicode {toUnicode} 0 R >>");
    }
}
