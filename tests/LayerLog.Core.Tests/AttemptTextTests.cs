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
