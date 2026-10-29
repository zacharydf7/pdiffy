using System.Text;
using PDiffy.Tests.Unit.Docx;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Benchmarks;

/// <summary>
/// What a benchmark PDF's pages hold: paragraphs under headings, a table with a row for every two lines, or images with
/// alt text nested a few sections deep.
/// </summary>
public enum Shape { Text, Table, Figures }

/// <summary>The documents the benchmarks read, built in code like the tests' documents.</summary>
internal static class Documents
{
    private const int ParagraphsPerPage = 30;

    /// <summary>
    /// A tagged PDF of <paramref name="pages"/> pages. A text document's words are built from <paramref name="seed"/>,
    /// so two with different seeds share no words but numbers.
    /// </summary>
    public static byte[] Pdf(int pages, Shape shape, string seed = "Paragraph")
    {
        var pdf = new TestPdf { Title = "Benchmark" };
        var elements = new List<TestElement>();
        for (var page = 1; page <= pages; page++)
        {
            var testPage = pdf.AddPage();
            elements.Add(shape switch
            {
                Shape.Text => Text(testPage, page, seed),
                Shape.Table => Table(testPage, page),
                Shape.Figures => Figures(testPage, page),
                _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Not a Shape value."),
            });
        }

        pdf.Structure([.. elements]);
        return pdf.Build();
    }

    /// <summary>
    /// A Word document of <paramref name="paragraphs"/> styled paragraphs and a table with a row for every ten.
    /// </summary>
    public static byte[] Docx(int paragraphs)
    {
        var body = new StringBuilder();
        for (var i = 0; i < paragraphs; i++)
        {
            body.Append($"<w:p><w:pPr><w:pStyle w:val=\"{(i % 10 == 0 ? "Heading1" : "Normal")}\"/></w:pPr>")
                .Append($"<w:r><w:rPr><w:b/></w:rPr><w:t>Paragraph {i}</w:t></w:r>")
                .Append("<w:r><w:t xml:space=\"preserve\"> of the body.</w:t></w:r></w:p>");
        }

        body.Append("<w:tbl>");
        for (var row = 0; row < paragraphs / 10; row++)
        {
            body.Append($"<w:tr><w:tc><w:p><w:r><w:t>Row {row}</w:t></w:r></w:p></w:tc>")
                .Append($"<w:tc><w:p><w:r><w:t>Value {row * 3}</w:t></w:r></w:p></w:tc></w:tr>");
        }

        body.Append("</w:tbl>");
        var styles = "<w:style w:type=\"paragraph\" w:styleId=\"Normal\"><w:rPr><w:sz w:val=\"22\"/></w:rPr></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:basedOn w:val=\"Normal\"/>"
            + "<w:rPr><w:b/></w:rPr></w:style>";
        return new TestDocx { Body = body.ToString(), Styles = styles }.Build().ToArray();
    }

    private static TestElement Text(TestPage page, int number, string seed)
    {
        var kids = new List<object>();
        for (var i = 0; i < ParagraphsPerPage; i++)
        {
            page.Tagged(i, i == 0 ? $"{seed} {seed}{number}" : $"{seed} {i} {seed}{number}", i == 0 ? "H1" : "P");
            kids.Add(new TestElement(i == 0 ? "H1" : "P", i) { Page = number });
        }

        return new TestElement("Sect", [.. kids]) { Page = number };
    }

    private static TestElement Table(TestPage page, int number)
    {
        var rows = new List<object>();
        for (var i = 0; i < ParagraphsPerPage; i += 2)
        {
            page.Tagged(i, $"Row {i}").Tagged(i + 1, $"Value {i * number}");
            var cells = new[] { new TestElement("TD", i) { Page = number }, new TestElement("TD", i + 1) { Page = number } };
            rows.Add(new TestElement("TR", cells) { Page = number });
        }

        return new TestElement("Table", [.. rows]) { Page = number };
    }

    private static TestElement Figures(TestPage page, int number)
    {
        var figures = new List<object>();
        for (var i = 0; i < 10; i++)
        {
            page.TaggedImage(i, gray: (0x10 + i * 20).ToString("X2"), y: 700 - i * 60);
            figures.Add(new TestElement("Figure", i) { Page = number, Entries = $"/Alt (Figure {i})" });
        }

        var inner = new TestElement("Div", [.. figures]) { Page = number };
        return new TestElement("Sect", new TestElement("Part", inner) { Page = number }) { Page = number };
    }
}
