using LayerLog.Core.Data;

namespace LayerLog.Core.Profiles;

public sealed record ComparedSetting(string Key, string Label, string A, string B);

/// <remarks>A record, since a tuple can't have an element named Rest.</remarks>
/// <param name="Differing">Settings both gcode have, with different values.</param>
/// <param name="OneSided">Settings only one gcode has.</param>
/// <param name="NoConfig">A gcode with no configuration at all: comparing it would just list the other one.</param>
public sealed record ComparisonResult(IReadOnlyList<ComparedSetting> Known, IReadOnlyList<ComparedSetting> Rest, int Differing = 0, int OneSided = 0, bool NoConfig = false)
{
    public void Deconstruct(out IReadOnlyList<ComparedSetting> known, out IReadOnlyList<ComparedSetting> rest) => (known, rest) = (Known, Rest);
}

public static class AttemptComparison
{
    public const string Missing = "—";

    /// <summary>What differs between two gcode, known settings apart from the rest. A setting only one side has shows «—» on the other.</summary>
    public static ComparisonResult Between(GcodeEntry a, GcodeEntry b)
    {
        if (a.Config.Count == 0 || b.Config.Count == 0) return new([], [], NoConfig: true);
        var keys = a.Config.Keys.Union(b.Config.Keys).Where(k => !ConfigDiff.IsIgnoredBetween(k));
        var settings = keys
            .Select(k => (Key: k, A: a.Config.GetValueOrDefault(k), B: b.Config.GetValueOrDefault(k)))
            .Where(c => c.A is null || c.B is null || ConfigDiff.First(c.A) != ConfigDiff.First(c.B))
            .ToList();
        var shown = settings.Select(c => new ComparedSetting(c.Key, ConfigLabels.Label(c.Key), Show(c.Key, c.A), Show(c.Key, c.B)))
            .OrderBy(s => s.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        var oneSided = settings.Count(c => c.A is null || c.B is null);
        return new(shown.Where(s => ConfigLabels.IsKnown(s.Key)).ToList(), shown.Where(s => !ConfigLabels.IsKnown(s.Key)).ToList(), settings.Count - oneSided, oneSided);
    }

    static string Show(string key, string? value) => value is null ? Missing : ConfigLabels.Format(key, ConfigDiff.First(value));
}
