using LayerLog.Core.Data;

namespace LayerLog.Core.Profiles;

public sealed record ComparedSetting(string Key, string Label, string A, string B);

/// <remarks>A record, since a tuple can't have an element named Rest.</remarks>
public sealed record ComparisonResult(IReadOnlyList<ComparedSetting> Known, IReadOnlyList<ComparedSetting> Rest);

public static class AttemptComparison
{
    public const string Missing = "—";

    /// <summary>What differs between two gcode, known settings apart from the rest. A setting only one side has shows «—» on the other.</summary>
    public static ComparisonResult Between(GcodeEntry a, GcodeEntry b)
    {
        var keys = a.Config.Keys.Union(b.Config.Keys).Where(k => !ConfigDiff.IsIgnoredBetween(k));
        var settings = keys
            .Select(k => (Key: k, A: a.Config.GetValueOrDefault(k), B: b.Config.GetValueOrDefault(k)))
            .Where(c => c.A is null || c.B is null || ConfigDiff.First(c.A) != ConfigDiff.First(c.B))
            .Select(c => new ComparedSetting(c.Key, ConfigLabels.Label(c.Key), Show(c.Key, c.A), Show(c.Key, c.B)))
            .OrderBy(s => s.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new(settings.Where(s => ConfigLabels.IsKnown(s.Key)).ToList(), settings.Where(s => !ConfigLabels.IsKnown(s.Key)).ToList());
    }

    static string Show(string key, string? value) => value is null ? Missing : ConfigLabels.Format(key, ConfigDiff.First(value));
}
