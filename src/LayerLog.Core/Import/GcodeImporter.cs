using System.Security.Cryptography;
using LayerLog.Core.Data;
using LayerLog.Core.Gcode;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Import;

public sealed record ImportResult(int Added, int Known, int Skipped);

public sealed class GcodeImporter(HistoryRepository repository, ProfileLibrary profiles)
{
    public Task<ImportResult> ImportAsync(string root, string volumeSerial, CancellationToken ct) =>
        Task.Run(() => Import(root, volumeSerial, ct), ct);

    ImportResult Import(string root, string volumeSerial, CancellationToken ct)
    {
        int added = 0, known = 0, skipped = 0;
        var locations = new Dictionary<long, string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.gcode", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var hash = Convert.ToHexString(SHA256.HashData(stream));
                var id = repository.FindByHash(hash);
                if (id is null)
                {
                    stream.Position = 0;
                    try
                    {
                        var info = GcodeParser.Parse(stream);
                        id = repository.Add(hash, Path.GetFileName(file), info, Compare(info));
                        added++;
                    }
                    catch (GcodeFormatException)
                    {
                        skipped++;
                        continue;
                    }
                }
                else known++;
                locations[id.Value] = Path.GetRelativePath(root, file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The card went away or a file is busy: keep what we knew and retry on the next pass.
            return new ImportResult(added, known, skipped);
        }

        repository.SetLocations(volumeSerial, locations);
        return new ImportResult(added, known, skipped);
    }

    BaseComparison Compare(GcodeInfo info)
    {
        var process = profiles.Resolve(ProfileKind.Process, info.ProcessBase, info.Version);
        var filament = profiles.Resolve(ProfileKind.Filament, info.FilamentBase, info.Version);
        return new BaseComparison(
            process is null ? [] : ConfigDiff.AgainstBase(info, process),
            filament is null ? [] : ConfigDiff.AgainstBase(info, filament),
            process?.SourceVersion ?? filament?.SourceVersion,
            process?.IsApproximate == true || filament?.IsApproximate == true,
            process is null || filament is null);
    }
}
