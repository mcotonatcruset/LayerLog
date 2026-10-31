using LayerLog.Core.Data;
using LayerLog.Core.Models;

namespace LayerLog.Core.Tests;

public sealed class ModelLibraryTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("layerlog").FullName;
    readonly string root;
    readonly HistoryRepository repo;

    public ModelLibraryTests()
    {
        root = Directory.CreateDirectory(Path.Combine(dir, "models")).FullName;
        repo = new HistoryRepository(Path.Combine(dir, "t.db"));
    }

    public void Dispose() => Directory.Delete(dir, true);

    string Put(string relative, string content = "model")
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return relative;
    }

    long AddGcode(string obj) => repo.Add(Guid.NewGuid().ToString(), "a.gcode", HistoryRepositoryTests.Info(obj), HistoryRepositoryTests.Comparison);

    [Fact]
    public void Scan_lists_models_in_subfolders()
    {
        Put(Path.Combine("a", "b.stl"));
        Put("c.3MF");
        Put("notes.txt");
        Assert.Equal([Path.Combine("a", "b.stl"), "c.3MF"], ModelLibrary.Scan(root).Select(m => m.RelativePath));
    }

    [Fact]
    public void Scan_of_missing_folder_is_empty() =>
        Assert.Empty(ModelLibrary.Scan(Path.Combine(dir, "nope")));

    [Fact]
    public void Match_is_case_insensitive()
    {
        Put(Path.Combine("x", "Totoro2.STL"));
        Assert.Equal(Path.Combine("x", "Totoro2.STL"), ModelLibrary.MatchObject("totoro2.stl", ModelLibrary.Scan(root)));
    }

    [Fact]
    public void Match_returns_null_when_name_is_ambiguous()
    {
        Put(Path.Combine("a", "1.stl"));
        Put(Path.Combine("b", "1.stl"));
        Assert.Null(ModelLibrary.MatchObject("1.stl", ModelLibrary.Scan(root)));
    }

    [Fact]
    public void Match_returns_null_for_internal_objects()
    {
        Put("Totoro2.stl");
        Assert.Null(ModelLibrary.MatchObject("Cubo", ModelLibrary.Scan(root)));
    }

    [Fact]
    public void Manual_link_wins_over_automatic_match()
    {
        var a = Put(Path.Combine("a", "Totoro2.stl"));
        var b = Put("b.stl");
        var id = AddGcode("Totoro2.stl");
        repo.LinkObject(id, "Totoro2.stl", b, "h", manual: true);
        repo.LinkUnlinked(root, ModelLibrary.Scan(root));
        Assert.Single(repo.ListForModel(b));
        Assert.Empty(repo.ListForModel(a));
    }

    [Fact]
    public void Unlinked_objects_are_linked_by_name()
    {
        var a = Put(Path.Combine("a", "Totoro2.stl"));
        AddGcode("Totoro2.stl");
        repo.LinkUnlinked(root, ModelLibrary.Scan(root));
        Assert.Single(repo.ListForModel(a));
    }

    [Fact]
    public void Moved_model_keeps_its_link()
    {
        var a = Put(Path.Combine("old", "m.stl"), "same content");
        AddGcode("m.stl");
        repo.LinkUnlinked(root, ModelLibrary.Scan(root));
        File.Delete(Path.Combine(root, a));
        var moved = Put(Path.Combine("new", "m.stl"), "same content");
        repo.RelinkMovedModels(root, ModelLibrary.Scan(root));
        Assert.Single(repo.ListForModel(moved));
    }

    [Fact]
    public void Renamed_model_keeps_its_link()
    {
        var a = Put("m.stl", "same content");
        AddGcode("m.stl");
        repo.LinkUnlinked(root, ModelLibrary.Scan(root));
        File.Delete(Path.Combine(root, a));
        var renamed = Put("renamed.stl", "same content");
        repo.RelinkMovedModels(root, ModelLibrary.Scan(root));
        Assert.Single(repo.ListForModel(renamed));
    }

    [Fact]
    public void Deleted_model_leaves_the_link_alone()
    {
        var a = Put("m.stl", "content");
        AddGcode("m.stl");
        repo.LinkUnlinked(root, ModelLibrary.Scan(root));
        File.Delete(Path.Combine(root, a));
        repo.RelinkMovedModels(root, ModelLibrary.Scan(root));
        Assert.Single(repo.ListForModel(a));
    }
}
