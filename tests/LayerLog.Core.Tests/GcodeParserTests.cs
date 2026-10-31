using LayerLog.Core.Gcode;

namespace LayerLog.Core.Tests;

public class GcodeParserTests
{
    static GcodeInfo Parse(string name)
    {
        using var stream = File.OpenRead(Path.Combine("Fixtures", name));
        return GcodeParser.Parse(stream);
    }

    [Fact]
    public void Reads_custom_profile()
    {
        var g = Parse("custom-profile.gcode");
        Assert.Equal("7.1.1.4472", g.Version);
        Assert.Equal(new DateTime(2026, 5, 21, 19, 22, 13), g.GeneratedAt);
        Assert.Equal("0.08", g.Config["layer_height"]);
        Assert.Equal(["Totoro2.stl"], g.Objects);
        Assert.Equal("0.12mm Fine @Creality Ender-3 V3 SE 0.4 nozzle - Copiar(1)", g.ProcessProfile);
        Assert.Equal("Hyper PLA @Creality Ender-3 V3 SE 0.4 nozzle - Copiar(1)", g.FilamentProfile);
        Assert.Equal("0.12mm Fine @Creality Ender-3 V3 SE 0.4 nozzle", g.ProcessBase);
        Assert.Equal("Hyper PLA @Creality Ender-3 V3 SE 0.4 nozzle", g.FilamentBase);
        Assert.Equal("3h 44m 50s", g.EstimatedTime);
        Assert.Equal(13.24, g.FilamentGrams);
        Assert.NotNull(g.Thumbnail);
        Assert.Equal(0x89, g.Thumbnail![0]);
    }

    [Fact]
    public void System_profile_falls_back_to_print_settings_id()
    {
        var g = Parse("system-profile.gcode");
        Assert.Equal("0.16mm Optimal @Creality Ender-3 V3 SE 0.4 nozzle", g.ProcessBase);
        Assert.Equal("Hyper PLA @Creality Ender-3 V3 SE 0.4 nozzle", g.FilamentBase);
    }

    [Fact]
    public void Layer_height_comes_from_config_block_not_header()
    {
        var g = Parse("system-profile.gcode");
        Assert.Equal("0.16", g.Config["layer_height"]);
    }

    [Theory]
    [InlineData("not-creality.gcode")]
    [InlineData("truncated.gcode")]
    public void Rejects_invalid_gcode(string file) =>
        Assert.Throws<GcodeFormatException>(() => Parse(file));
}
