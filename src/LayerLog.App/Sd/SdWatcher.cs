using System.IO;
using System.Windows.Threading;
using LayerLog.Core.Import;

namespace LayerLog.App.Sd;

public sealed class SdWatcher : IDisposable
{
    readonly Dispatcher dispatcher;
    readonly Dictionary<string, (string Serial, FileSystemWatcher Watcher)> known = [];
    readonly DispatcherTimer rescan;
    readonly Dictionary<string, DispatcherTimer> pending = [];

    /// <summary>Root and serial when a card appears; root and null when it goes away.</summary>
    public event Action<string, string?>? CardChanged;
    public event Action<string>? CardContentChanged;

    public SdWatcher(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        rescan = new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Background, (_, _) => { rescan!.Stop(); Rescan(); }, dispatcher);
        rescan.Stop();
    }

    public void RescanSoon()
    {
        rescan.Stop();
        rescan.Start();
    }

    public void Rescan()
    {
        var present = DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Removable && d.IsReady)
            .Select(d => (Root: d.Name, Serial: VolumeInfo.ReadSerial(d.Name)))
            .Where(d => d.Serial is not null)
            .ToDictionary(d => d.Root, d => d.Serial!);

        foreach (var (root, entry) in known.Where(k => !present.TryGetValue(k.Key, out var s) || s != k.Value.Serial).ToList())
        {
            entry.Watcher.Dispose();
            known.Remove(root);
            CardChanged?.Invoke(root, null);
        }
        foreach (var (root, serial) in present.Where(p => !known.ContainsKey(p.Key)))
        {
            known[root] = (serial, Watch(root));
            CardChanged?.Invoke(root, serial);
        }
    }

    FileSystemWatcher Watch(string root)
    {
        var watcher = new FileSystemWatcher(root, "*.gcode") { IncludeSubdirectories = true, EnableRaisingEvents = true };
        FileSystemEventHandler changed = (_, _) => dispatcher.BeginInvoke(() => Debounce(root));
        watcher.Created += changed;
        watcher.Changed += changed;
        watcher.Deleted += changed;
        watcher.Error += (_, _) => dispatcher.BeginInvoke(RescanSoon);
        return watcher;
    }

    // Slicers write gcode in bursts: wait for two quiet seconds before reading it.
    void Debounce(string root)
    {
        if (!pending.TryGetValue(root, out var timer))
        {
            timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (s, _) =>
            {
                ((DispatcherTimer)s!).Stop();
                CardContentChanged?.Invoke(root);
            }, dispatcher);
            pending[root] = timer;
        }
        timer.Stop();
        timer.Start();
    }

    public void Dispose()
    {
        rescan.Stop();
        foreach (var timer in pending.Values) timer.Stop();
        foreach (var entry in known.Values) entry.Watcher.Dispose();
    }
}
