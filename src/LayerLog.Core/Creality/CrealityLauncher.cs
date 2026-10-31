using System.Diagnostics;

namespace LayerLog.Core.Creality;

public static class CrealityLauncher
{
    const string ExeName = "CrealityPrint.exe";

    public static IReadOnlyList<string> DefaultRoots =>
        [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Creality")];

    public static string? FindExe(string? saved, IEnumerable<string> roots)
    {
        if (saved is not null && File.Exists(saved)) return saved;
        return roots.Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, ExeName, SearchOption.AllDirectories))
            .FirstOrDefault();
    }

    public static ProcessStartInfo BuildStart(string exe, IReadOnlyList<string> files)
    {
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        foreach (var file in files) start.ArgumentList.Add(file);
        return start;
    }

    public static bool GroupWarningNeeded(int fileCount) => fileCount >= 2;
}
