using LayerLog.Core.Data;
using LayerLog.Core.Models;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public class AttemptTextTests
{
    [Theory]
    [InlineData(0, "Sense imprimir")]
    [InlineData(1, "1 impressió")]
    [InlineData(3, "3 impressions")]
    public void State_uses_the_agreed_words(int prints, string text) => Assert.Equal(text, AttemptText.State(prints));

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(true, false, "Comparació aproximada: no hi ha perfils de la versió 7.0, s'ha fet servir la 6.3.")]
    [InlineData(false, true, "No s'ha trobat algun perfil base: mira la configuració completa.")]
    public void Caution_says_why_the_changes_may_not_be_exact(bool approximate, bool missing, string? text)
    {
        var entry = new GcodeEntry(1, "a.gcode", default, "7.0.1.2", null, null, "", "", new Dictionary<string, string>(), [], [], "6.3", approximate, missing, null, [], []);

        Assert.Equal(text, AttemptText.Caution(entry));
    }

    [Fact]
    public void Pick_orders_by_importance_then_known_labels()
    {
        var changes = new[]
        {
            new ConfigChange("seam_gap", "0.1", "0.2"),
            new ConfigChange("travel_speed", "300", "400"),
            new ConfigChange("nozzle_temperature", "190", "200"),
            new ConfigChange("raft_layers", "0", "2"),
            new ConfigChange("wall_loops", "2", "3"),
            new ConfigChange("layer_height", "0.12", "0.08"),
        };

        var picked = KeyChanges.Pick(changes, 6).Select(c => c.Key).ToList();

        Assert.Equal(
            ["layer_height", "wall_loops", "raft_layers", "nozzle_temperature", "travel_speed", "seam_gap"],
            picked);
    }

    [Fact]
    public void Pick_returns_all_when_there_are_fewer_than_count()
    {
        var changes = new[]
        {
            new ConfigChange("travel_speed", "300", "400"),
            new ConfigChange("layer_height", "0.12", "0.08"),
        };

        var picked = KeyChanges.Pick(changes, 5).Select(c => c.Key).ToList();

        Assert.Equal(["layer_height", "travel_speed"], picked);
    }
}
