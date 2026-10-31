using System.IO;
using System.Windows.Threading;
using LayerLog.Core.Import;

namespace LayerLog.App.Sd;

public sealed class SdWatcher : IDisposable
{
    sealed class Card(string serial)
    {
        public string Serial { get; } = serial;
        public string? Imported { get; set; }
        public string? Pending { get; set; }
    }

    readonly Dictionary<string, Card> known = [];
    readonly DispatcherTimer rescan;
    readonly DispatcherTimer poll;
    bool polling;

    /// <summary>Root and serial when a card appears; root and null when it goes away.</summary>
    public event Action<string, string?>? CardChanged;
    public event Action<string>? CardContentChanged;

    public SdWatcher(Dispatcher dispatcher)
    {
        rescan = new DispatcherTimer(TimeSpan.FromMilliseconds(600), DispatcherPriority.Background, (_, _) => { rescan!.Stop(); Rescan(); }, dispatcher);
        rescan.Stop();
        // ponytail: polling, because a FileSystemWatcher holds a handle that stops Windows ejecting the card.
        poll = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background, async (_, _) => await PollAsync(), dispatcher);
    }

    public void RescanSoon()
    {
        rescan.Stop();
        rescan.Start();
    }

    public void Rescan()
    {
        var present = new Dictionary<string, string>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady))
            if (VolumeInfo.ReadSerial(drive.Name) is { } serial) present[drive.Name] = serial;

        foreach (var (root, _) in known.Where(k => !present.TryGetValue(k.Key, out var s) || s != k.Value.Serial).ToList())
        {
            known.Remove(root);
            CardChanged?.Invoke(root, null);
        }
        foreach (var (root, serial) in present.Where(p => !known.ContainsKey(p.Key)))
        {
            known[root] = new Card(serial) { Imported = CardSignature.Of(root) };
            CardChanged?.Invoke(root, serial);
        }
    }

    async Task PollAsync()
    {
        if (polling) return;
        polling = true;
        try
        {
            foreach (var (root, card) in known.ToList())
            {
                var now = await Task.Run(() => CardSignature.Of(root));
                if (now is null || now == card.Imported)
                {
                    card.Pending = null;
                    continue;
                }
                // Same change on two polls in a row: the slicer has finished writing.
                if (now == card.Pending)
                {
                    card.Imported = now;
                    card.Pending = null;
                    CardContentChanged?.Invoke(root);
                }
                else card.Pending = now;
            }
        }
        finally
        {
            polling = false;
        }
    }

    public void Dispose()
    {
        rescan.Stop();
        poll.Stop();
    }
}
