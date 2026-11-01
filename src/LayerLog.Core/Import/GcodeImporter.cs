using System.Security.Cryptography;
using LayerLog.Core.Data;
using LayerLog.Core.Gcode;
using LayerLog.Core.Profiles;

namespace LayerLog.Core.Import;

/// <param name="Newest">The most recently sliced of the gcodes added, if any.</param>
public sealed record ImportResult(int Added, int Known, int Skipped, bool Interrupted = false, long? Newest = null);

public sealed class GcodeImporter(HistoryRepository repository, ProfileLibrary profiles)
{
    readonly SemaphoreSlim onePass = new(1, 1);

    public async Task<ImportResult> ImportAsync(string root, string volumeSerial, CancellationToken ct)
    {
        await onePass.WaitAsync(ct);
        try
        {
            return await Task.Run(() => Import(root, volumeSerial, ct), ct);
        }
        finally
        {
            onePass.Release();
        }
    }

    ImportResult Import(string root, string volumeSerial, CancellationToken ct)
    {
        int added = 0, known = 0, skipped = 0;
        var interrupted = false;
        (DateTime At, long Id)? newest = null;
        var locations = new Dictionary<long, string>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.gcode", options))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var hash = Convert.ToHexString(SHA256.HashData(stream));
                    var id = repository.FindByHash(hash);
                    if (id is null)
                    {
                        stream.Position = 0;
                        var info = GcodeParser.Parse(stream);
                        id = repository.Add(hash, Path.GetFileName(file), info, Compare(info));
                        added++;
                        if (newest is null || info.GeneratedAt >= newest.Value.At) newest = (info.GeneratedAt, id.Value);
                    }
                    else known++;
                    locations[id.Value] = Path.GetRelativePath(root, file);
                }
                catch (GcodeFormatException)
                {
                    skipped++;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // Busy or unreadable: the others still import, and this one retries on the next pass.
                    skipped++;
                    interrupted = true;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            interrupted = true; // the card went away
        }

        // A partial pass cannot tell "deleted" from "not reached", so it keeps what we knew.
        if (!interrupted) repository.SetLocations(volumeSerial, locations);
        return new ImportResult(added, known, skipped, interrupted, newest?.Id);
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
