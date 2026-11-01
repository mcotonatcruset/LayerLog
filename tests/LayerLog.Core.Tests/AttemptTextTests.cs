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

    [Theory]
    [InlineData(0, 0, false, "Cap diferència entre aquests dos gcode")]
    [InlineData(1, 0, false, "1 diferència")]
    [InlineData(2, 1, false, "2 diferències i 1 paràmetre només en un gcode")]
    [InlineData(0, 3, false, "3 paràmetres només en un gcode")]
    [InlineData(0, 1, false, "1 paràmetre només en un gcode")]
    [InlineData(0, 0, true, "No es poden comparar")]
    public void Comparison_title_counts_in_the_singular_and_plural(int differing, int oneSided, bool noConfig, string text) =>
        Assert.Equal(text, AttemptText.ComparisonTitle(new([], [], differing, oneSided, noConfig)));

    [Theory]
    [InlineData(0, 2, null)]
    [InlineData(1, 0, "Mostrar 1 paràmetre")]
    [InlineData(4, 0, "Mostrar 4 paràmetres")]
    [InlineData(1, 2, "1 paràmetre més")]
    [InlineData(3, 2, "3 paràmetres més")]
    public void Rest_title_counts_in_the_singular_and_plural(int rest, int known, string? text) =>
        Assert.Equal(text, AttemptText.RestTitle(rest, known));

    [Fact]
    public void Comparison_caution_shows_when_either_base_is_approximate()
    {
        GcodeEntry E(bool approximate) => new(1, "a.gcode", default, "7.0.1.2", null, null, "", "", new Dictionary<string, string>(), [], [], "6.3", approximate, false, null, [], []);
        Assert.Null(AttemptText.ComparisonCaution(E(false), E(false)));
        Assert.NotNull(AttemptText.ComparisonCaution(E(false), E(true)));
        Assert.NotNull(AttemptText.ComparisonCaution(E(true), E(false)));
        Assert.DoesNotContain("\n",AttemptText.ComparisonCaution(E(true), E(true)));
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

    [Theory]
    [InlineData(null, "", null, "Vols esborrar aquesta impressió? No es pot desfer.")]
    [InlineData(4, "  ", null, "Vols esborrar aquesta impressió? Es perdrà la valoració. No es pot desfer.")]
    [InlineData(null, "nota", null, "Vols esborrar aquesta impressió? Es perdrà la nota. No es pot desfer.")]
    [InlineData(4, "nota", "f.png", "Vols esborrar aquesta impressió? Es perdran la valoració i la nota. El fitxer de la foto es queda al disc. No es pot desfer.")]
    public void Delete_warning_names_only_what_there_is(int? rating, string note, string? photo, string expected) =>
        Assert.Equal(expected, AttemptText.DeleteWarning(new PrintRecord(1, 1, new DateOnly(2026, 1, 1), rating, note, photo)));
}
