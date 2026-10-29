using BenchmarkDotNet.Attributes;

namespace PDiffy.Benchmarks;

/// <summary>Reading a Word document and writing its snapshot, for long bodies and big tables.</summary>
[MemoryDiagnoser]
public class DocxBenchmarks
{
    private byte[] _docx = [];
    private DocxDocument _document = null!;

    [Params(100, 1000)]
    public int Paragraphs { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _docx = Documents.Docx(Paragraphs);
        _document = DocxDocument.Load(new MemoryStream(_docx));
    }

    [Benchmark]
    public DocxDocument Load() => DocxDocument.Load(new MemoryStream(_docx));

    [Benchmark]
    public string ToSnapshot() => _document.ToSnapshot();
}
