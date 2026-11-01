using LayerLog.Core.Data;
using LayerLog.Core.Models;
using Microsoft.Data.Sqlite;

namespace LayerLog.Core.Tests;

public sealed class FigureMetaTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("layerlog").FullName;
    readonly string root;
    readonly HistoryRepository repo;

    public FigureMetaTests()
    {
        root = Directory.CreateDirectory(Path.Combine(dir, "models")).FullName;
        repo = new HistoryRepository(Path.Combine(dir, "t.db"));
    }

    public void Dispose() => Directory.Delete(dir, true);

    string Put(string relative, string content)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return relative;
    }

    void SetAlias(string relative, string? alias) =>
        repo.SetAlias(relative, ModelLibrary.HashFile(Path.Combine(root, relative)), new FileInfo(Path.Combine(root, relative)).Length, alias);

    [Fact]
    public void Alias_is_saved_and_blank_clears_it()
    {
        repo.SetAlias("a.stl", "h", 1, "  Totoro ");
        Assert.Equal("Totoro", repo.FigureMetas()["a.stl"].Alias);
        repo.SetAlias("a.stl", "h", 1, "   ");
        Assert.Null(repo.FigureMetas()["a.stl"].Alias);
    }

    [Fact]
    public void Alias_and_part_flag_are_independent()
    {
        repo.SetAlias("a.stl", "h", 1, "Totoro");
        repo.SetIsPart("a.stl", "h", 1, true);
        var meta = repo.FigureMetas()["a.stl"];
        Assert.Equal("Totoro", meta.Alias);
        Assert.True(meta.IsPart);

        repo.SetIsPart("b.stl", "h2", 2, true);
        repo.SetAlias("b.stl", "h2", 2, "Peu");
        Assert.True(repo.FigureMetas()["b.stl"].IsPart);
        Assert.Equal("Peu", repo.FigureMetas()["b.stl"].Alias);
    }

    [Fact]
    public void Moved_model_keeps_its_alias()
    {
        var a = Put("a.stl", "same content");
        SetAlias(a, "Totoro");
        File.Delete(Path.Combine(root, a));
        var moved = Put(Path.Combine("b", "a.stl"), "same content");
        repo.RelinkMovedModels(root, ModelLibrary.Scan(root));
        var metas = repo.FigureMetas();
        Assert.Single(metas);
        Assert.Equal("Totoro", metas[moved].Alias);
    }

    [Fact]
    public void Edited_model_at_same_path_keeps_its_alias()
    {
        var a = Put("a.stl", "first");
        SetAlias(a, "Totoro");
        Put("a.stl", "second, longer");
        Put("copy.stl", "first"); // looks like the old content moved: must not steal the alias from a model that is still there
        repo.RelinkMovedModels(root, ModelLibrary.Scan(root));
        var metas = repo.FigureMetas();
        Assert.Equal("Totoro", metas[a].Alias);
        Assert.DoesNotContain("copy.stl", metas.Keys);
    }

    [Fact]
    public void Moved_model_keeps_its_part_flag()
    {
        var a = Put("a.stl", "same content");
        repo.SetIsPart(a, ModelLibrary.HashFile(Path.Combine(root, a)), new FileInfo(Path.Combine(root, a)).Length, true);
        File.Delete(Path.Combine(root, a));
        var moved = Put(Path.Combine("b", "a.stl"), "same content");
        repo.RelinkMovedModels(root, ModelLibrary.Scan(root));
        Assert.True(repo.FigureMetas()[moved].IsPart);
    }

    [Fact]
    public void Stale_link_plus_fresh_alias_then_move()
    {
        var a = Put("a.stl", "v1 content");
        var id = repo.Add("g", "a.gcode", HistoryRepositoryTests.Info("a.stl"), HistoryRepositoryTests.Comparison);
        repo.LinkObject(id, "a.stl", a, ModelLibrary.HashFile(Path.Combine(root, a)), false, new FileInfo(Path.Combine(root, a)).Length);
        Put("a.stl", "v2 content, edited");
        SetAlias(a, "Nom");
        File.Delete(Path.Combine(root, a));
        var moved = Put(Path.Combine("sub", "a.stl"), "v2 content, edited");
        repo.RelinkMovedModels(root, ModelLibrary.Scan(root));
        Assert.Equal("Nom", repo.FigureMetas()[moved].Alias);
    }

    [Fact]
    public void Case_only_rename_follows()
    {
        var a = Put("totoro.stl", "x");
        var id = repo.Add("g", "a.gcode", HistoryRepositoryTests.Info("totoro.stl"), HistoryRepositoryTests.Comparison);
        repo.LinkObject(id, "totoro.stl", a, ModelLibrary.HashFile(Path.Combine(root, a)), false, 1);
        SetAlias(a, "Nom");
        File.Move(Path.Combine(root, a), Path.Combine(root, "Totoro.stl"));
        var scan = ModelLibrary.Scan(root);
        repo.RelinkMovedModels(root, scan);
        Assert.Equal("Nom", repo.FigureMetas()["Totoro.stl"].Alias);
        Assert.Empty(repo.EntriesWithoutFigure(scan.Select(m => m.RelativePath).ToHashSet()));
    }

    [Fact]
    public void Gcode_with_no_object_links_is_without_figure()
    {
        repo.Add("g", "a.gcode", HistoryRepositoryTests.Info("x.stl") with { Objects = [] }, HistoryRepositoryTests.Comparison);
        Assert.Single(repo.EntriesWithoutFigure(new HashSet<string>()));
    }

    [Fact]
    public void Existing_version_2_database_migrates_without_losing_prints()
    {
        var db = Path.Combine(dir, "v2.db");
        var old = new HistoryRepository(db);
        var id = old.Add("h", "a.gcode", HistoryRepositoryTests.Info("x"), HistoryRepositoryTests.Comparison);
        old.AddPrint(id, new DateOnly(2026, 1, 1), 4, "ok", null);
        using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "DROP TABLE figure_meta; PRAGMA user_version = 2;";
            cmd.ExecuteNonQuery();
        }

        var reopened = new HistoryRepository(db);
        Assert.Single(reopened.Prints(id));
        Assert.Empty(reopened.FigureMetas());
    }
}
