using System.IO;
using System.Windows.Threading;

namespace LayerLog.App.Viewer;

/// <summary>Tells when the model folder changes, once things have been quiet for a moment.</summary>
public sealed class ModelWatcher : IDisposable
{
    readonly FileSystemWatcher? watcher;
    readonly DispatcherTimer quiet;

    public ModelWatcher(string folder, Dispatcher dispatcher, Action changed)
    {
        // Unlike the SD, this folder is on a fixed disk: holding a handle on it stops nothing.
        quiet = new DispatcherTimer(TimeSpan.FromSeconds(1.5), DispatcherPriority.Background, (_, _) =>
        {
            quiet!.Stop();
            changed();
        }, dispatcher);
        quiet.Stop();

        try
        {
            watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
            };
            void Restart() => dispatcher.BeginInvoke(() =>
            {
                quiet.Stop();
                quiet.Start();
            });
            watcher.Created += (_, _) => Restart();
            watcher.Changed += (_, _) => Restart();
            watcher.Deleted += (_, _) => Restart();
            watcher.Renamed += (_, _) => Restart();
            watcher.Error += (_, _) => Restart(); // a burst overflowed the buffer: rescanning covers it
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            watcher?.Dispose();
            watcher = null; // the folder is gone or unreadable: reopening it still works
        }
    }

    public void Dispose()
    {
        quiet.Stop();
        watcher?.Dispose();
    }
}
