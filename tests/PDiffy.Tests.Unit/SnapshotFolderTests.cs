namespace PDiffy.Tests.Unit;

// For tests that change the environment, Snapshot.Directory and the default options. Classes deriving from it share
// one collection, so they never run at once; each test gets its own folder and puts everything back afterwards.
public abstract class SnapshotFolderTests : IDisposable
{
    private static readonly string[] Variables = [Snapshot.AcceptVariable, "CI", "TF_BUILD"];

    private readonly Dictionary<string, string?> _savedVariables =
        Variables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
    private readonly string? _savedDirectory = Snapshot.Directory;
    private readonly Action<SnapshotOptions>? _savedPdfDefaults = PdfSnapshot.DefaultOptions;
    private readonly Action<DocxSnapshotOptions>? _savedDocxDefaults = DocxSnapshot.DefaultOptions;
    private readonly Action<TextSnapshotOptions>? _savedTextDefaults = Snapshot.DefaultOptions;

    protected SnapshotFolderTests()
    {
        Snapshot.Directory = Folder;
        foreach (var name in Variables) Environment.SetEnvironmentVariable(name, null);
    }

    protected string Folder { get; } = Path.Combine(Path.GetTempPath(), "pdiffy-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Snapshot.Directory = _savedDirectory;
        PdfSnapshot.DefaultOptions = _savedPdfDefaults;
        DocxSnapshot.DefaultOptions = _savedDocxDefaults;
        Snapshot.DefaultOptions = _savedTextDefaults;
        foreach (var (name, value) in _savedVariables) Environment.SetEnvironmentVariable(name, value);
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
        GC.SuppressFinalize(this);
    }
}
