using LayerLog.Core.Gcode;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public class ConfigDiffTests
{
    static GcodeInfo Gcode(string name)
    {
        using var stream = File.OpenRead(Path.Combine("Fixtures", name));
        return GcodeParser.Parse(stream);
    }

    static readonly Dictionary<string, string> Empty = [];

    static Dictionary<string, string> D(string key, string value) => new() { [key] = value };

    [Fact]
    public void Diff_uses_first_value_of_lists()
    {
        var c = ConfigDiff.Compare(D("nozzle_temperature", "200"), D("nozzle_temperature", "200,200"));
        Assert.Empty(c);
    }

    [Fact]
    public void Diff_skips_identity_keys()
    {
        var a = new Dictionary<string, string>
        {
            ["name"] = "a", ["inherits"] = "a", ["print_settings_id"] = "a", ["compatible_printers"] = "a", ["default_filament_colour"] = "a",
        };
        var b = a.ToDictionary(p => p.Key, _ => "b");
        Assert.Empty(ConfigDiff.Compare(a, b));
    }

    [Fact]
    public void Diff_ignores_keys_missing_on_either_side()
    {
        Assert.Empty(ConfigDiff.Compare(D("only_a", "1"), Empty));
        Assert.Empty(ConfigDiff.Compare(Empty, D("only_b", "1")));
    }

    [Fact]
    public void Totoro_matches_real_changes()
    {
        var g = Gcode("custom-profile.gcode");
        var b = ProfileLibraryTests.Library().Resolve(ProfileKind.Process, g.ProcessBase, g.Version)!;
        var c = ConfigDiff.AgainstBase(g, b);
        Assert.Contains(c, x => x is { Key: "layer_height", Base: "0.12", Value: "0.08" });
        Assert.Contains(c, x => x is { Key: "wall_loops", Base: "2", Value: "4" });
        Assert.DoesNotContain(c, x => x.Key == "sparse_infill_density");
    }

    [Fact]
    public void Between_ignores_identity_and_time_keys()
    {
        var a = new Dictionary<string, string>
        {
            ["printer_settings_id"] = "a", ["inherits_group"] = "a", ["filename_format"] = "a", ["machine_start_gcode"] = "a", ["creality_uuid"] = "a", ["version"] = "a", ["print_time"] = "a",
        };
        var b = a.ToDictionary(p => p.Key, _ => "b");
        Assert.Empty(ConfigDiff.Between(a, b));
    }

    [Fact]
    public void Between_reports_real_differences()
    {
        var c = ConfigDiff.Between(Gcode("custom-profile.gcode").Config, Gcode("system-profile.gcode").Config);
        Assert.Contains(c, x => x is { Key: "layer_height", Base: "0.08", Value: "0.16" });
    }
}
