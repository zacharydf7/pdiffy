namespace PDiffy.Tests.Unit;

public sealed class ProjectFoldersTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pdiffy-tests", Guid.NewGuid().ToString("N"));

    public ProjectFoldersTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "repo", "tests", "Trails", "Maps"));
        Directory.CreateDirectory(Path.Combine(_root, "repo", "Docs"));
    }

    [Theory]
    [InlineData("/_/tests/Trails/Maps", "tests/Trails", "tests/Trails/Maps")]
    [InlineData("/_/tests/Trails", "tests/Trails", "tests/Trails")]
    [InlineData("/_/Docs", "", "Docs")]
    public void Find_ShouldFindTheFolderUnderItsProject_WhenTheBuildMappedItsPath(string mapped, string project, string expected)
    {
        var found = ProjectFolders.Find(mapped, [Repo(project)]);

        Assert.Equal(Repo(expected), found);
    }

    [Fact]
    public void Find_ShouldGiveNothing_WhenNoProjectHoldsTheFolder()
    {
        Assert.Null(ProjectFolders.Find("/_/tests/Lakes/Maps", [Repo("tests/Trails")]));
    }

    [Fact]
    public void ProjectFolder_ShouldBeTheTestProjectsFolder_WhenTheBuildFileRan()
    {
        var snapshots = Snapshot.ProjectFolder("Snapshots");

        Assert.Equal("Snapshots", Path.GetFileName(snapshots));
        Assert.True(File.Exists(Path.Combine(Snapshot.ProjectFolder(), "PDiffy.Tests.Unit.csproj")));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private string Repo(string path) => Path.Combine([_root, "repo", .. path.Split('/', StringSplitOptions.RemoveEmptyEntries)]);
}
