using System.Text.RegularExpressions;
using LayerLog.Core.Gcode;

namespace LayerLog.Core.Profiles;

public sealed record ConfigChange(string Key, string Base, string Value);

public static partial class ConfigDiff
{
    public static IReadOnlyList<ConfigChange> AgainstBase(GcodeInfo gcode, BaseProfile profile) =>
        Compare(profile.Values, gcode.Config);

    public static IReadOnlyList<ConfigChange> Compare(IReadOnlyDictionary<string, string> baseValues, IReadOnlyDictionary<string, string> used) =>
        Changes(baseValues, used, IdentityKey());

    public static bool IsIgnoredBetween(string key) => IgnoredBetween().IsMatch(key);

    static List<ConfigChange> Changes(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b, Regex skip) =>
        a.Where(p => !skip.IsMatch(p.Key) && b.ContainsKey(p.Key) && First(p.Value) != First(b[p.Key]))
         .Select(p => new ConfigChange(p.Key, First(p.Value), First(b[p.Key])))
         .ToList();

    // Multi-extruder settings repeat their value: "200,200".
    public static string First(string value) => value.Split(',')[0].Trim().Trim('"');

    [GeneratedRegex(@"^(inherits|name$|from$|type$|version$|setting_id$|instantiation$|compatible_|print_settings_id$|filament_settings_id$|default_filament_colour$)|gcode")]
    private static partial Regex IdentityKey();

    [GeneratedRegex(@"_id$|^inherits|filename|time|gcode|uuid|version|^name$|colour|compatible_", RegexOptions.IgnoreCase)]
    private static partial Regex IgnoredBetween();
}
