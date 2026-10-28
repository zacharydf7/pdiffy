using System.Text;

namespace PDiffy.Tests.Unit.Pdfs;

/// <summary>Writes a PDF from hand-written object bodies, for files <see cref="TestPdf"/> can't produce.</summary>
internal static class RawPdf
{
    /// <summary>Object 1 must be the catalog. Objects are numbered from 1 in the order given.</summary>
    public static PdfUaDocument Load(params string[] objects) => LoadWithTrailer("", objects);

    /// <summary>Like <see cref="Load"/>, with extra trailer entries such as "/Info 5 0 R" or "/Encrypt 4 0 R".</summary>
    public static PdfUaDocument LoadWithTrailer(string trailerEntries, params string[] objects) =>
        PdfUaDocument.Load(new MemoryStream(Build(objects, catalog: 1, trailerEntries)));

    public static byte[] Build(IReadOnlyList<string> objects, int catalog, string trailerEntries)
    {
        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root {catalog} 0 R {trailerEntries} >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }
}
