using System.Text;

namespace PDiffy.Tests.Unit.Pdfs;

/// <summary>
/// How <see cref="RawPdf.Build"/> lays the file out, for tests that change only the encoding: objects written last to
/// first, numbered from <see cref="FirstNumber"/>, indexed by a cross-reference stream instead of a table, or followed by
/// an update that rewrites the catalog unchanged.
/// </summary>
internal sealed record PdfLayout(bool ReverseOrder = false, int FirstNumber = 1, bool CrossReferenceStream = false,
    bool EmptyUpdate = false);

/// <summary>Writes a PDF from hand-written object bodies, for files <see cref="TestPdf"/> can't produce.</summary>
internal static class RawPdf
{
    /// <summary>Object 1 must be the catalog. Objects are numbered from 1 in the order given.</summary>
    public static PdfUaDocument Load(params string[] objects) => LoadWithTrailer("", objects);

    /// <summary>Like <see cref="Load"/>, with extra trailer entries such as "/Info 5 0 R" or "/Encrypt 4 0 R".</summary>
    public static PdfUaDocument LoadWithTrailer(string trailerEntries, params string[] objects) =>
        PdfUaDocument.Load(new MemoryStream(Build(objects, catalog: 1, trailerEntries)));

    /// <summary>The objects are numbered from the layout's first number, and refer to each other by those numbers.</summary>
    public static byte[] Build(IReadOnlyList<string> objects, int catalog, string trailerEntries, PdfLayout? layout = null)
    {
        layout ??= new PdfLayout();
        var first = layout.FirstNumber;
        var trailer = $"/Root {catalog} 0 R {trailerEntries}";

        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new int[objects.Count];
        var order = Enumerable.Range(0, objects.Count);
        foreach (var i in layout.ReverseOrder ? order.Reverse() : order)
        {
            offsets[i] = pdf.Length;
            pdf.Append($"{first + i} 0 obj\n{objects[i]}\nendobj\n");
        }

        // A cross-reference stream is an object too, numbered after the others.
        var size = first + objects.Count + (layout.CrossReferenceStream ? 1 : 0);
        var xref = pdf.Length;
        if (layout.CrossReferenceStream) AppendCrossReferenceStream(pdf, offsets, first, trailer);
        else AppendCrossReferenceTable(pdf, offsets, first, trailer, size);
        pdf.Append($"startxref\n{xref}\n%%EOF\n");

        if (layout.EmptyUpdate)
        {
            var update = pdf.Length;
            pdf.Append($"{catalog} 0 obj\n{objects[catalog - first]}\nendobj\n");
            var updateXref = pdf.Length;
            pdf.Append($"xref\n{catalog} 1\n{update:D10} 00000 n \n");
            pdf.Append($"trailer\n<< /Size {size} {trailer} /Prev {xref} >>\nstartxref\n{updateXref}\n%%EOF\n");
        }

        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    private static void AppendCrossReferenceTable(StringBuilder pdf, int[] offsets, int first, string trailer, int size)
    {
        pdf.Append($"xref\n0 1\n0000000000 65535 f \n{first} {offsets.Length}\n");
        foreach (var offset in offsets) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {size} {trailer} >>\n");
    }

    // Each entry is a type byte, a 4-byte offset and a 2-byte generation; the stream lists itself last.
    private static void AppendCrossReferenceStream(StringBuilder pdf, int[] offsets, int first, string trailer)
    {
        var number = first + offsets.Length;
        var entries = new StringBuilder(Entry(0, 0, 0xFFFF));
        foreach (var offset in offsets) entries.Append(Entry(1, offset, 0));
        entries.Append(Entry(1, pdf.Length, 0));
        pdf.Append($"{number} 0 obj\n<< /Type /XRef /Size {number + 1} /W [1 4 2] /Index [0 1 {first} {offsets.Length + 1}] ");
        pdf.Append($"{trailer} /Length {entries.Length} >>\nstream\n{entries}\nendstream\nendobj\n");
    }

    private static string Entry(int type, int value, int generation) => string.Concat((char)type,
        (char)(value >> 24 & 0xFF), (char)(value >> 16 & 0xFF), (char)(value >> 8 & 0xFF), (char)(value & 0xFF),
        (char)(generation >> 8 & 0xFF), (char)(generation & 0xFF));
}
