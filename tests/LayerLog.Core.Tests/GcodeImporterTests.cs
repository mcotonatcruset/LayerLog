using LayerLog.Core.Data;
using LayerLog.Core.Import;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public sealed class GcodeImporterTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("layerlog").FullName;
    readonly string card;
    readonly HistoryRepository repo;
    readonly GcodeImporter importer;

    public GcodeImporterTests()
    {
        card = Directory.CreateDirectory(Path.Combine(dir, "card")).FullName;
        repo = new HistoryRepository(Path.Combine(dir, "t.db"));
        importer = new GcodeImporter(repo, new ProfileLibrary(Path.Combine("Fixtures", "profiles")));
    }

    public void Dispose() => Directory.Delete(dir, true);

    void Put(string fixture, string relative)
    {
        var path = Path.Combine(card, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(Path.Combine("Fixtures", fixture), path, true);
    }

    Task<ImportResult> Import(string serial = "AAAA") => importer.ImportAsync(card, serial, CancellationToken.None);

    [Fact]
    public async Task Finds_gcode_in_subfolders()
    {
        Put("custom-profile.gcode", "a.gcode");
        Put("system-profile.gcode", Path.Combine("sub", "dir", "b.gcode"));
        Assert.Equal(2, (await Import()).Added);
    }

    [Fact]
    public async Task Newest_is_the_last_sliced_of_those_added()
    {
        Put("system-profile.gcode", "a.gcode");
        Put("custom-profile.gcode", "b.gcode");
        var r = await Import();
        Assert.Equal("a.gcode", repo.Get(r.Newest!.Value).FileName);
        Assert.Null((await Import()).Newest);
    }

    [Fact]
    public async Task Second_import_adds_nothing()
    {
        Put("custom-profile.gcode", "a.gcode");
        Put("system-profile.gcode", "b.gcode");
        await Import();
        var r = await Import();
        Assert.Equal(0, r.Added);
        Assert.Equal(2, r.Known);
    }

    [Fact]
    public async Task Same_content_on_two_cards_is_one_entry()
    {
        Put("custom-profile.gcode", "a.gcode");
        await Import("AAAA");
        await Import("BBBB");
        var e = Assert.Single(repo.List());
        Assert.Equal(["AAAA", "BBBB"], e.VolumeSerials.Order());
    }

    [Fact]
    public async Task Invalid_gcode_is_skipped_and_others_continue()
    {
        Put("not-creality.gcode", "bad.gcode");
        Put("custom-profile.gcode", "good.gcode");
        var r = await Import();
        Assert.Equal(1, r.Added);
        Assert.Equal(1, r.Skipped);
    }

    [Fact]
    public async Task Partial_file_is_retried_after_being_completed()
    {
        Put("truncated.gcode", "a.gcode");
        Assert.Equal(1, (await Import()).Skipped);
        Put("system-profile.gcode", "a.gcode");
        Assert.Equal(1, (await Import()).Added);
    }

    [Fact]
    public async Task Missing_root_keeps_prior_locations()
    {
        Put("custom-profile.gcode", "a.gcode");
        await Import();
        Directory.Delete(card, true);
        var r = await Import();
        Assert.Equal(0, r.Added);
        Assert.Single(repo.List()[0].VolumeSerials);
    }

    [Fact]
    public async Task Locked_file_stops_the_pass_and_keeps_prior_locations()
    {
        Put("custom-profile.gcode", "a.gcode");
        await Import();
        Put("system-profile.gcode", "b.gcode");
        using (new FileStream(Path.Combine(card, "b.gcode"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Import();
        Assert.Single(repo.List()[0].VolumeSerials);
    }

    [Fact]
    public async Task Removed_file_loses_its_location_but_keeps_entry()
    {
        Put("custom-profile.gcode", "a.gcode");
        await Import();
        File.Delete(Path.Combine(card, "a.gcode"));
        await Import();
        Assert.Empty(Assert.Single(repo.List()).VolumeSerials);
    }

    [Fact]
    public async Task Stores_changes_against_base()
    {
        Put("custom-profile.gcode", "a.gcode");
        await Import();
        var e = repo.List()[0];
        Assert.Contains(e.ProcessChanges, c => c is { Key: "layer_height", Base: "0.12", Value: "0.08" });
        Assert.True(e.BaseApproximate);
    }

    [Fact]
    public async Task Unknown_profile_marks_entry_as_missing_base()
    {
        Put("system-profile.gcode", "a.gcode"); // no 0.16mm profile in the fixtures
        await Import();
        Assert.True(repo.List()[0].BaseMissing);
    }

    [Fact]
    public async Task Does_not_modify_source_files()
    {
        Put("custom-profile.gcode", "a.gcode");
        var path = Path.Combine(card, "a.gcode");
        var bytes = File.ReadAllBytes(path);
        var written = File.GetLastWriteTimeUtc(path);
        await Import();
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(written, File.GetLastWriteTimeUtc(path));
    }
}
