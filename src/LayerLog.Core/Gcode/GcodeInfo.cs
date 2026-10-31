namespace LayerLog.Core.Gcode;

public sealed record GcodeInfo(
    string Version,
    DateTime GeneratedAt,
    string? EstimatedTime,
    double? FilamentGrams,
    IReadOnlyList<string> Objects,
    string ProcessProfile,
    string FilamentProfile,
    string ProcessBase,
    string FilamentBase,
    IReadOnlyDictionary<string, string> Config,
    byte[]? Thumbnail);

public sealed class GcodeFormatException(string message) : Exception(message);
