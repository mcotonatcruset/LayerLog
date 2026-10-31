using System.Text;

namespace LayerLog.Core.Import;

/// <summary>A cheap fingerprint of the gcode on a card, to notice changes by polling without holding it open.</summary>
public static class CardSignature
{
    public static string? Of(string root)
    {
        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            var files = new DirectoryInfo(root).EnumerateFiles("*.gcode", options)
                .Select(f => $"{f.FullName}|{f.Length}|{f.LastWriteTimeUtc.Ticks}")
                .Order(StringComparer.Ordinal);
            return new StringBuilder().AppendJoin('\n', files).ToString();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
