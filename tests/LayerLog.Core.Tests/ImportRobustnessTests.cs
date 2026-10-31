using System.Text;
using LayerLog.Core.Data;
using LayerLog.Core.Gcode;
using LayerLog.Core.Import;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public sealed class ImportRobustnessTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("layerlog").FullName;
    readonly string card;
    readonly string profiles;
    readonly HistoryRepository repo;
    readonly GcodeImporter importer;

    public ImportRobustnessTests()
    {
        card = Directory.CreateDirectory(Path.Combine(dir, "card")).FullName;
        profiles = Path.Combine(dir, "creator");
        repo = new HistoryRepository(Path.Combine(dir, "t.db"));
        importer = new GcodeImporter(repo, new ProfileLibrary(profiles));
    }

    public void Dispose() => Directory.Delete(dir, true);

    static string Fixture(string name = "custom-profile.gcode") => File.ReadAllText(Path.Combine("Fixtures", name), Encoding.Latin1);

    void Put(string name, string content) => File.WriteAllText(Path.Combine(card, name), content, Encoding.Latin1);

    Task<ImportResult> Import() => importer.ImportAsync(card, "AAAA", CancellationToken.None);

    [Fact]
    public async Task Bad_thumbnail_keeps_the_entry_without_image()
    {
        var lines = Fixture().Split('\n');
        var i = Array.FindIndex(lines, l => l.StartsWith("; png begin"));
        lines[i + 1] = "; %%% not base64 %%%";
        Put("a.gcode", string.Join('\n', lines));
        Assert.Equal(1, (await Import()).Added);
        Assert.Null(repo.List()[0].Thumbnail);
    }

    [Fact]
    public async Task Unexpected_date_is_skipped()
    {
        Put("a.gcode", Fixture().Replace("on 2026-05-21 at", "on 21/05/2026 at"));
        Assert.Equal(1, (await Import()).Skipped);
    }

    [Fact]
    public async Task Short_version_does_not_break_the_import()
    {
        Put("a.gcode", Fixture().Replace("V7.1.1.4472", "V7"));
        Assert.Equal(1, (await Import()).Added);
        Assert.True(repo.List()[0].BaseMissing);
    }

    [Fact]
    public async Task Corrupt_profile_counts_as_missing()
    {
        var folder = Directory.CreateDirectory(Path.Combine(profiles, "7.1", "system", "Creality", "process")).FullName;
        File.WriteAllText(Path.Combine(folder, "0.12mm Fine @Creality Ender-3 V3 SE 0.4 nozzle.json"), "{ corrupte");
        Put("a.gcode", Fixture());
        Assert.Equal(1, (await Import()).Added);
        Assert.True(repo.List()[0].BaseMissing);
    }

    [Fact]
    public async Task Overlapping_passes_add_one_entry()
    {
        Put("a.gcode", Fixture());
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Import()));
        Assert.Single(repo.List());
    }

    [Fact]
    public void Adding_the_same_hash_twice_returns_the_same_entry()
    {
        var a = repo.Add("h", "a.gcode", HistoryRepositoryTests.Info(), HistoryRepositoryTests.Comparison);
        var b = repo.Add("h", "a.gcode", HistoryRepositoryTests.Info(), HistoryRepositoryTests.Comparison);
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Unreadable_file_does_not_stop_the_rest()
    {
        Put("a.gcode", Fixture("system-profile.gcode"));
        Put("z.gcode", Fixture());
        using var locked = new FileStream(Path.Combine(card, "a.gcode"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var r = await Import();
        Assert.Equal(1, r.Added);
        Assert.Equal(1, r.Skipped);
        Assert.True(r.Interrupted);
    }

    [Fact]
    public async Task Missing_card_is_reported_as_interrupted()
    {
        Directory.Delete(card, true);
        Assert.True((await Import()).Interrupted);
    }

    [Fact]
    public void Card_signature_sees_new_and_renamed_gcode_only()
    {
        var first = CardSignature.Of(card);
        Put("x.gcode.tmp", "a");
        Assert.Equal(first, CardSignature.Of(card));
        File.Move(Path.Combine(card, "x.gcode.tmp"), Path.Combine(card, "x.gcode"));
        Assert.NotEqual(first, CardSignature.Of(card));
    }

    [Fact]
    public void Card_signature_is_null_when_the_card_is_gone() =>
        Assert.Null(CardSignature.Of(Path.Combine(dir, "gone")));
}
