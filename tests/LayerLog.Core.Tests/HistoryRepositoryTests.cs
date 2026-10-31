using LayerLog.Core.Data;
using LayerLog.Core.Gcode;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public sealed class HistoryRepositoryTests : IDisposable
{
    readonly string dbPath = Path.Combine(Path.GetTempPath(), $"layerlog-{Guid.NewGuid():N}.db");
    readonly HistoryRepository repo;

    public HistoryRepositoryTests() => repo = new HistoryRepository(dbPath);

    public void Dispose() => File.Delete(dbPath);

    internal static GcodeInfo Info(string obj = "Totoro2.stl") => new(
        "7.1.1.4472", new DateTime(2026, 5, 21), "3h", 13.2, [obj], "Fine - Copiar(1)", "PLA", "Fine", "PLA",
        new Dictionary<string, string> { ["layer_height"] = "0.08" }, [1, 2, 3]);

    internal static readonly BaseComparison Comparison = new(
        [new ConfigChange("layer_height", "0.12", "0.08")], [new ConfigChange("nozzle_temperature", "190", "200")], "7.3", true, false);

    long Add(string hash = "h1", string file = "Totoro.gcode", string obj = "Totoro2.stl") => repo.Add(hash, file, Info(obj), Comparison);

    [Fact]
    public void Adding_gcode_creates_no_print() => Assert.Empty(repo.Prints(Add()));

    [Fact]
    public void One_gcode_can_have_many_prints()
    {
        var id = Add();
        repo.AddPrint(id, new DateOnly(2026, 5, 22), 5, "bé", null);
        repo.AddPrint(id, new DateOnly(2026, 5, 23), 2, "malament", null);
        Assert.Equal(2, repo.Prints(id).Count);
    }

    [Fact]
    public void Print_can_be_updated_and_deleted()
    {
        var id = Add();
        var p = repo.Prints(repo.AddPrint(id, new DateOnly(2026, 5, 22), 5, "bé", null) > 0 ? id : 0)[0];
        repo.UpdatePrint(p with { Note = "millor", Rating = 4 });
        Assert.Equal("millor", repo.Prints(id)[0].Note);
        repo.DeletePrint(p.Id);
        Assert.Empty(repo.Prints(id));
    }

    [Fact]
    public void Entry_keeps_stored_comparison()
    {
        Add();
        var e = new HistoryRepository(dbPath).List()[0];
        Assert.Equal("0.12", e.ProcessChanges[0].Base);
        Assert.Equal("200", e.FilamentChanges[0].Value);
        Assert.Equal("7.3", e.BaseSourceVersion);
        Assert.True(e.BaseApproximate);
        Assert.Equal("0.08", e.Config["layer_height"]);
        Assert.Equal(new byte[] { 1, 2, 3 }, e.Thumbnail);
    }

    [Fact]
    public void FindByHash_finds_only_known_content()
    {
        var id = Add();
        Assert.Equal(id, repo.FindByHash("h1"));
        Assert.Null(repo.FindByHash("other"));
    }

    [Fact]
    public void Same_gcode_on_two_cards_keeps_both_serials()
    {
        var id = Add();
        repo.SetLocations("AAAA", new Dictionary<long, string> { [id] = "a.gcode" });
        repo.SetLocations("BBBB", new Dictionary<long, string> { [id] = "b.gcode" });
        Assert.Equal(["AAAA", "BBBB"], repo.List()[0].VolumeSerials.Order());
    }

    [Fact]
    public void Entry_survives_when_removed_from_card()
    {
        var id = Add();
        repo.SetLocations("AAAA", new Dictionary<long, string> { [id] = "a.gcode" });
        repo.SetLocations("AAAA", new Dictionary<long, string>());
        Assert.Empty(repo.List()[0].VolumeSerials);
    }

    [Fact]
    public void Best_print_prefers_rating_then_date()
    {
        var id = Add();
        repo.AddPrint(id, new DateOnly(2026, 1, 1), 5, "vell", null);
        repo.AddPrint(id, new DateOnly(2026, 2, 1), 5, "nou", null);
        repo.AddPrint(id, new DateOnly(2026, 3, 1), 3, "pitjor", null);
        Assert.Equal("nou", repo.BestPrint([id])!.Note);
    }

    [Fact]
    public void Unrated_print_is_never_best_over_rated()
    {
        var id = Add();
        repo.AddPrint(id, new DateOnly(2026, 3, 1), null, "sense", null);
        repo.AddPrint(id, new DateOnly(2026, 1, 1), 1, "dolent", null);
        Assert.Equal("dolent", repo.BestPrint([id])!.Note);
    }

    [Fact]
    public void Search_matches_file_and_object_names()
    {
        Add("h1", "Totoro.gcode", "Totoro2.stl");
        Add("h2", "Marine.gcode", "lt-head.stl");
        Assert.Single(repo.List("marine"));
        Assert.Single(repo.List("LT-HEAD"));
    }

    [Fact]
    public void ListForObject_finds_entries_with_that_object()
    {
        Add();
        Assert.Single(repo.ListForObject("Totoro2.stl"));
        Assert.Empty(repo.ListForObject("altre.stl"));
    }

    [Fact]
    public void Newest_gcode_comes_first()
    {
        repo.Add("old", "old.gcode", Info() with { GeneratedAt = new DateTime(2025, 1, 1) }, Comparison);
        repo.Add("new", "new.gcode", Info() with { GeneratedAt = new DateTime(2026, 1, 1) }, Comparison);
        Assert.Equal("new.gcode", repo.List()[0].FileName);
    }
}
