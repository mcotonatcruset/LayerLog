using LayerLog.Core.Data;
using LayerLog.Core.Gcode;
using LayerLog.Core.Models;

namespace LayerLog.Core.Tests;

public sealed class FigureCatalogTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("layerlog").FullName;
    readonly HistoryRepository repo;

    public FigureCatalogTests() => repo = new HistoryRepository(Path.Combine(dir, "t.db"));

    public void Dispose() => Directory.Delete(dir, true);

    // Adds a gcode whose objects are linked to the given model paths (object name = model path).
    long Gcode(string hash, string profile, params string[] models)
    {
        var info = HistoryRepositoryTests.Info() with { Objects = models, ProcessProfile = profile };
        var id = repo.Add(hash, hash + ".gcode", info, HistoryRepositoryTests.Comparison);
        foreach (var m in models) repo.LinkObject(id, m, m, "h", size: 1);
        return id;
    }

    static ModelFile Model(string path) => new(path, 1, DateTime.Now);

    IReadOnlyList<Figure> Build(params string[] models) =>
        FigureCatalog.Build(models.Select(Model), repo.FigureMetas(), repo.FigureStats());

    [Fact]
    public void Name_uses_alias_then_file_name()
    {
        repo.SetAlias("3.stl", "h", 1, "Totoro");
        var figures = Build("3.stl", "5.stl");
        Assert.Equal("Totoro", figures.Single(f => f.RelativePath == "3.stl").Name);
        Assert.Equal("5", figures.Single(f => f.RelativePath == "5.stl").Name);
    }

    [Fact]
    public void Folder_is_the_relative_directory_or_empty_at_the_root()
    {
        var figures = Build("3.stl", Path.Combine("Ghibli", "5.stl"));
        Assert.Equal("", figures.Single(f => f.RelativePath == "3.stl").Folder);
        Assert.Equal("Ghibli", figures.Single(f => f.Folder != "").Folder);
    }

    [Fact]
    public void Gcode_with_two_objects_counts_for_both_figures()
    {
        var id = Gcode("a", "Fine", "3.stl", "5.stl");
        repo.AddPrint(id, new DateOnly(2026, 1, 1), 4, "", null);
        var stats = repo.FigureStats();
        Assert.Equal(1, stats["3.stl"].Attempts);
        Assert.Equal(1, stats["5.stl"].Attempts);
        Assert.Equal(4, stats["3.stl"].BestRating);
        Assert.Equal(4, stats["5.stl"].BestRating);
    }

    [Fact]
    public void Prints_are_counted_once_each_across_the_attempts_of_a_figure()
    {
        var first = Gcode("a", "Fine", "3.stl", "5.stl");
        var second = Gcode("b", "Fine", "3.stl");
        Gcode("c", "Fine", "7.stl");
        repo.AddPrint(first, new DateOnly(2026, 1, 1), 4, "", null);
        repo.AddPrint(first, new DateOnly(2026, 1, 2), null, "", null);
        repo.AddPrint(second, new DateOnly(2026, 1, 3), null, "", null);

        var stats = repo.FigureStats();
        Assert.Equal(3, stats["3.stl"].Prints);
        Assert.Equal(2, stats["5.stl"].Prints);
        Assert.Equal(0, stats["7.stl"].Prints);
        Assert.Equal(1, stats["7.stl"].Attempts);
    }

    [Fact]
    public void Best_rating_breaks_ties_with_the_most_recent_print()
    {
        var old = Gcode("a", "Old", "3.stl");
        var recent = Gcode("b", "Recent", "3.stl");
        repo.AddPrint(old, new DateOnly(2026, 1, 1), 5, "", null);
        repo.AddPrint(recent, new DateOnly(2026, 2, 1), 5, "", null);
        repo.AddPrint(old, new DateOnly(2026, 3, 1), 3, "", null);
        var stats = repo.FigureStats()["3.stl"];
        Assert.Equal(2, stats.Attempts);
        Assert.Equal(5, stats.BestRating);
        Assert.Equal("Recent", stats.BestProfile);
    }

    [Fact]
    public void Unrated_figure_has_no_best_rating()
    {
        var id = Gcode("a", "Fine", "3.stl");
        repo.AddPrint(id, new DateOnly(2026, 1, 1), null, "", null);
        var stats = repo.FigureStats()["3.stl"];
        Assert.Equal(1, stats.Attempts);
        Assert.Null(stats.BestRating);
        Assert.Null(stats.BestProfile);
    }

    [Fact]
    public void Gcode_of_deleted_model_is_without_figure()
    {
        Gcode("a", "Fine", "x.stl");
        Assert.Single(repo.EntriesWithoutFigure(new HashSet<string> { "y.stl" }));
    }

    [Fact]
    public void Gcode_linked_to_one_existing_figure_is_not_without_figure()
    {
        Gcode("a", "Fine", "x.stl", "y.stl");
        Assert.Empty(repo.EntriesWithoutFigure(new HashSet<string> { "y.stl" }));
    }

    [Fact]
    public void Recent_ignores_figures_without_attempts_and_parts()
    {
        Gcode("a", "Fine", "1.stl");
        Gcode("b", "Fine", "2.stl");
        repo.SetIsPart("2.stl", "h", 1, true);
        var recent = FigureCatalog.Recent(Build("1.stl", "2.stl", "3.stl"), 10);
        Assert.Equal(["1.stl"], recent.Select(f => f.RelativePath));
    }

    [Fact]
    public void Search_finds_the_alias_and_folder_ignoring_accents()
    {
        repo.SetAlias(Path.Combine("Ghibli", "3.stl"), "h", 1, "Tòtoro");
        var figure = Build(Path.Combine("Ghibli", "3.stl")).Single();
        Assert.True(ModelSearch.Matches(figure, "totoro", FigureFilter.All));
        Assert.True(ModelSearch.Matches(figure, "GHIBLI toto", FigureFilter.All));
        Assert.False(ModelSearch.Matches(figure, "totoro", FigureFilter.Printed));
        Assert.False(ModelSearch.Matches(figure, "mei", FigureFilter.All));
    }
}
