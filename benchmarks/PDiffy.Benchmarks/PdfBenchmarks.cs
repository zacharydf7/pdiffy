using BenchmarkDotNet.Attributes;
using PDiffy.Tests.Unit.Pdfs;

namespace PDiffy.Benchmarks;

/// <summary>Reading a PDF and writing its snapshot, for documents of a few sizes and shapes.</summary>
[MemoryDiagnoser]
public class PdfBenchmarks
{
    private byte[] _pdf = [];
    private PdfUaDocument _document = null!;

    [Params(1, 10, 100)]
    public int Pages { get; set; }

    [ParamsAllValues]
    public Shape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _pdf = Documents.Pdf(Pages, Shape);
        _document = PdfUaDocument.Load(new MemoryStream(_pdf));
    }

    [Benchmark]
    public PdfUaDocument Load() => PdfUaDocument.Load(new MemoryStream(_pdf));

    [Benchmark]
    public string ToSnapshot() => _document.ToSnapshot();
}
