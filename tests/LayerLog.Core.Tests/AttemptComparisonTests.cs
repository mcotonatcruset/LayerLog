using LayerLog.Core.Data;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public class AttemptComparisonTests
{
    static GcodeEntry Entry(params (string Key, string Value)[] config) =>
        new(1, "a.gcode", default, "7.0.1.2", null, null, "", "", config.ToDictionary(c => c.Key, c => c.Value), [], [], "6.3", false, false, null, [], []);

    [Fact]
    public void Known_settings_come_first_and_the_rest_apart()
    {
        var a = Entry(("layer_height", "0.08"), ("zzz_unknown_setting", "1"));
        var b = Entry(("layer_height", "0.16"), ("zzz_unknown_setting", "2"));

        var (known, rest) = AttemptComparison.Between(a, b);

        var height = Assert.Single(known);
        Assert.Equal(("layer_height", "0,08 mm", "0,16 mm"), (height.Key, height.A, height.B));
        Assert.Equal("zzz_unknown_setting", Assert.Single(rest).Key);
    }

    [Fact]
    public void Same_gcode_has_no_differences()
    {
        var a = Entry(("layer_height", "0.08"), ("zzz_unknown_setting", "1"));
        var (known, rest) = AttemptComparison.Between(a, a);
        Assert.Empty(known);
        Assert.Empty(rest);
    }

    [Fact]
    public void Setting_missing_on_one_side_is_shown_as_missing()
    {
        var (_, rest) = AttemptComparison.Between(Entry(("zzz_unknown_setting", "1"), ("layer_height", "0.1")), Entry(("layer_height", "0.1")));
        var only = Assert.Single(rest);
        Assert.Equal(("1", "—"), (only.A, only.B));

        var (_, back) = AttemptComparison.Between(Entry(("layer_height", "0.1")), Entry(("zzz_unknown_setting", "1"), ("layer_height", "0.1")));
        Assert.Equal(("—", "1"), (back.Single().A, back.Single().B));
    }

    [Fact]
    public void Counts_separate_changed_values_from_settings_on_one_side()
    {
        var a = Entry(("layer_height", "0.08"), ("zzz_a", "1"), ("zzz_same", "x"));
        var b = Entry(("layer_height", "0.16"), ("zzz_b", "1"), ("zzz_same", "x"));
        var result = AttemptComparison.Between(a, b);
        Assert.Equal((1, 2), (result.Differing, result.OneSided));
    }

    [Fact]
    public void Gcode_without_config_is_flagged_not_listed()
    {
        var result = AttemptComparison.Between(Entry(), Entry(("layer_height", "0.16")));
        Assert.True(result.NoConfig);
        Assert.Empty(result.Known);
        Assert.Empty(result.Rest);
    }
}
