using BenchmarkDotNet.Attributes;

namespace PDiffy.Benchmarks;

/// <summary>
/// Comparing two snapshots: the usual case of one paragraph edited in a long document, and the worst case, a document
/// whose text shares no words with the other's.
/// </summary>
[MemoryDiagnoser]
public class DiffBenchmarks
{
    private string _before = "";
    private string _edited = "";
    private string _unrelated = "";

    [Params(10, 100)]
    public int Pages { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _before = PdfUaDocument.Load(new MemoryStream(Documents.Pdf(Pages, Shape.Text))).ToSnapshot();
        _edited = _before.Replace("\"Paragraph 3 Paragraph2\"", "\"Paragraph three Paragraph2\"");
        if (_edited == _before) throw new InvalidOperationException("The edit didn't match the snapshot's text.");
        _unrelated = PdfUaDocument.Load(new MemoryStream(Documents.Pdf(Pages, Shape.Text, seed: "Other"))).ToSnapshot();
    }

    [Benchmark]
    public SnapshotDiff OneEdit() => SnapshotDiff.Compare(_before, _edited);

    [Benchmark]
    public SnapshotDiff Unrelated() => SnapshotDiff.Compare(_before, _unrelated);
}
