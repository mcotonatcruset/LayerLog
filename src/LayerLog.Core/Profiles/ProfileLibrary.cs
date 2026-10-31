using System.Text.Json;

namespace LayerLog.Core.Profiles;

public enum ProfileKind { Process, Filament }

public sealed record BaseProfile(IReadOnlyDictionary<string, string> Values, string SourceVersion, bool IsApproximate);

public sealed class ProfileLibrary(string creatorRoot)
{
    public BaseProfile? Resolve(ProfileKind kind, string name, string gcodeVersion)
    {
        var wanted = VersionKey(gcodeVersion);
        var versions = Versions().OrderBy(v => Math.Abs(VersionKey(v) - wanted)).ThenByDescending(VersionKey);

        foreach (var version in versions)
        {
            var folder = Path.Combine(creatorRoot, version, "system", "Creality", kind == ProfileKind.Process ? "process" : "filament");
            if (!File.Exists(Path.Combine(folder, $"{name}.json"))) continue;
            try
            {
                return new BaseProfile(Chain(folder, name, []), version, VersionKey(version) != wanted);
            }
            catch (Exception e) when (e is JsonException or IOException)
            {
                // Unreadable here: another version may still have it.
            }
        }
        return null;
    }

    IEnumerable<string> Versions() =>
        !Directory.Exists(creatorRoot) ? [] :
        Directory.GetDirectories(creatorRoot).Select(d => Path.GetFileName(d)!)
            .Where(n => n.Split('.') is [var a, var b] && int.TryParse(a, out _) && int.TryParse(b, out _));

    static int VersionKey(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor) ? major * 100 + minor : 0;
    }

    static Dictionary<string, string> Chain(string folder, string name, HashSet<string> seen)
    {
        var file = Path.Combine(folder, $"{name}.json");
        if (!seen.Add(name) || !File.Exists(file)) return [];

        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var own = doc.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => First(p.Value));
        var merged = own.TryGetValue("inherits", out var parent) ? Chain(folder, parent, seen) : [];
        foreach (var (key, value) in own) merged[key] = value;
        return merged;
    }

    static string First(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Array => e.GetArrayLength() > 0 ? First(e[0]) : "",
        JsonValueKind.String => e.GetString()!,
        _ => e.GetRawText(),
    };
}
