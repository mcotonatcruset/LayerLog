namespace LayerLog.Core.Settings;

public sealed record StartupState(string? ModelFolder, string? ModelToOpen, bool NeedsFolder)
{
    public static StartupState From(AppSettings settings, string? defaultFolder)
    {
        var folder = settings.ModelFolder ?? defaultFolder;
        if (folder is null || !Directory.Exists(folder)) return new StartupState(null, null, true);

        var last = settings.LastModel;
        return new StartupState(folder, last is not null && File.Exists(Path.Combine(folder, last)) ? last : null, false);
    }
}
