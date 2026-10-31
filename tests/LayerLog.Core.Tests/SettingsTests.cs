using LayerLog.Core.Settings;

namespace LayerLog.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("layerlog").FullName;
    string SettingsFile => Path.Combine(dir, "settings.json");

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void Missing_settings_file_gives_defaults() =>
        Assert.Equal(new AppSettings(null, null, null), AppSettings.Load(SettingsFile));

    [Fact]
    public void Corrupt_settings_file_gives_defaults()
    {
        File.WriteAllText(SettingsFile, "{ no és json");
        Assert.Equal(new AppSettings(null, null, null), AppSettings.Load(SettingsFile));
    }

    [Fact]
    public void Changed_folder_is_saved()
    {
        new AppSettings(dir, "a.stl", null).Save(SettingsFile);
        Assert.Equal(dir, AppSettings.Load(SettingsFile).ModelFolder);
    }

    [Fact]
    public void Save_creates_the_data_folder()
    {
        var nested = Path.Combine(dir, "x", "settings.json");
        new AppSettings(dir, null, null).Save(nested);
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public void Default_folder_is_used_on_first_run()
    {
        var s = StartupState.From(new AppSettings(null, null, null), dir);
        Assert.Equal(dir, s.ModelFolder);
        Assert.False(s.NeedsFolder);
    }

    [Fact]
    public void Missing_folder_asks_for_one()
    {
        var gone = Path.Combine(dir, "gone");
        var s = StartupState.From(new AppSettings(gone, "a.stl", null), gone);
        Assert.True(s.NeedsFolder);
        Assert.Null(s.ModelToOpen);
    }

    [Fact]
    public void Missing_last_model_opens_nothing()
    {
        var s = StartupState.From(new AppSettings(dir, "gone.stl", null), null);
        Assert.Equal(dir, s.ModelFolder);
        Assert.Null(s.ModelToOpen);
    }

    [Fact]
    public void Existing_last_model_is_reopened()
    {
        File.WriteAllText(Path.Combine(dir, "a.stl"), "x");
        Assert.Equal("a.stl", StartupState.From(new AppSettings(dir, "a.stl", null), null).ModelToOpen);
    }
}
