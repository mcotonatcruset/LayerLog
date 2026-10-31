using LayerLog.Core.Profiles;

namespace LayerLog.Core.Data;

public sealed record BaseComparison(
    IReadOnlyList<ConfigChange> Process,
    IReadOnlyList<ConfigChange> Filament,
    string? SourceVersion,
    bool Approximate,
    bool Missing);

public sealed record GcodeEntry(
    long Id,
    string FileName,
    DateTime GeneratedAt,
    string Version,
    string? EstimatedTime,
    double? FilamentGrams,
    string ProcessProfile,
    string FilamentProfile,
    IReadOnlyDictionary<string, string> Config,
    IReadOnlyList<ConfigChange> ProcessChanges,
    IReadOnlyList<ConfigChange> FilamentChanges,
    string? BaseSourceVersion,
    bool BaseApproximate,
    bool BaseMissing,
    byte[]? Thumbnail,
    IReadOnlyList<string> Objects,
    IReadOnlyList<string> VolumeSerials);

public sealed record ObjectLink(string Name, string? ModelPath);

public sealed record PrintRecord(long Id, long GcodeId, DateOnly Date, int? Rating, string Note, string? PhotoPath);
