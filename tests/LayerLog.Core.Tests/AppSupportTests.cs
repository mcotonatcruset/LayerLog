using LayerLog.Core.Data;
using LayerLog.Core.Import;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public sealed class AppSupportTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("layerlog").FullName;

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void Formats_like_vol_command() =>
        Assert.Equal("DE2F-F964", VolumeInfo.FormatSerial(0xDE2FF964));

    [Fact]
    public void Copies_photo_and_keeps_original()
    {
        var source = Path.Combine(dir, "foto.jpg");
        File.WriteAllText(source, "imatge");
        var saved = new PhotoStore(Path.Combine(dir, "photos")).Save(source, 7);
        Assert.Equal("imatge", File.ReadAllText(saved));
        Assert.Equal(".jpg", Path.GetExtension(saved));
        Assert.True(File.Exists(source));
        Assert.NotEqual(source, saved);
    }

    [Theory]
    [InlineData("layer_height", "0.08", "0,08 mm")]
    [InlineData("wall_loops", "4", "4")]
    [InlineData("enable_support", "1", "Sí")]
    [InlineData("support_interface_speed", "100%", "100%")]
    [InlineData("unknown_key", "1.5", "1,5")]
    public void Formats_values_with_units(string key, string value, string expected) =>
        Assert.Equal(expected, ConfigLabels.Format(key, value));

    [Fact]
    public void Unknown_labels_stay_readable()
    {
        Assert.Equal("Altura de capa", ConfigLabels.Label("layer_height"));
        Assert.Equal("seam gap", ConfigLabels.Label("seam_gap"));
        Assert.True(ConfigLabels.IsKnown("layer_height"));
        Assert.False(ConfigLabels.IsKnown("seam_gap"));
    }

    [Fact]
    public void Links_and_counts_reflect_the_models()
    {
        var repo = new HistoryRepository(Path.Combine(dir, "t.db"));
        var a = repo.Add("a", "a.gcode", HistoryRepositoryTests.Info("m.stl"), HistoryRepositoryTests.Comparison);
        var b = repo.Add("b", "b.gcode", HistoryRepositoryTests.Info("m.stl"), HistoryRepositoryTests.Comparison);
        repo.LinkObject(a, "m.stl", "x\\m.stl", null);
        repo.LinkObject(b, "m.stl", "x\\m.stl", null);
        Assert.Equal(2, repo.ModelGcodeCounts()["x\\m.stl"]);
        Assert.Equal([new ObjectLink("m.stl", "x\\m.stl")], repo.ObjectLinks(a));
    }
}
