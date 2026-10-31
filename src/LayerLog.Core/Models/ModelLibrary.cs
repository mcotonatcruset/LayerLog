using System.Security.Cryptography;

namespace LayerLog.Core.Models;

public sealed record ModelFile(string RelativePath, long Size, DateTime Modified);

public static class ModelLibrary
{
    static readonly string[] Extensions = [".stl", ".3mf"];

    public static IReadOnlyList<ModelFile> Scan(string root)
    {
        if (!Directory.Exists(root)) return [];
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        return new DirectoryInfo(root).EnumerateFiles("*", options)
            .Where(f => Extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            .Select(f => new ModelFile(Path.GetRelativePath(root, f.FullName), f.Length, f.LastWriteTimeUtc))
            .OrderBy(m => m.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string? MatchObject(string objectName, IEnumerable<ModelFile> models)
    {
        var matches = models.Where(m => Path.GetFileName(m.RelativePath).Equals(objectName, StringComparison.OrdinalIgnoreCase)).Take(2).ToList();
        return matches.Count == 1 ? matches[0].RelativePath : null;
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
