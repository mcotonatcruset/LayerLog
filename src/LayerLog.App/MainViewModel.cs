using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.App.History;
using LayerLog.App.Viewer;
using LayerLog.Core.Data;
using LayerLog.Core.Import;
using LayerLog.Core.Profiles;
using LayerLog.Core.Settings;

namespace LayerLog.App;

public sealed partial class MainViewModel : ObservableObject
{
    readonly HistoryRepository repository;
    readonly GcodeImporter importer;
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    readonly Dictionary<string, string> cards = [];

    public ViewerViewModel Viewer { get; }
    public HistoryViewModel History { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowHistory))] private bool showViewer = true;
    [ObservableProperty] private string cardStatus = "Cap SD connectada";
    [ObservableProperty] private string? toast;
    [ObservableProperty] private bool toastHasAction;

    public bool ShowHistory
    {
        get => !ShowViewer;
        set => ShowViewer = !value;
    }

    public MainViewModel()
    {
        Directory.CreateDirectory(AppPaths.DataFolder);
        repository = new HistoryRepository(AppPaths.Database);
        importer = new GcodeImporter(repository, new ProfileLibrary(AppPaths.CrealityProfiles));
        History = new HistoryViewModel(repository, new PhotoStore(AppPaths.Photos), OpenModel, () => Viewer!.ModelFolder);
        Viewer = new ViewerViewModel(repository, AppSettings.Load(AppPaths.Settings), ShowToast, OpenEntry);
        toastTimer.Tick += (_, _) => HideToast();
    }

    // Prints are edited in the history, so the figure panel may be stale on the way back.
    partial void OnShowViewerChanged(bool value)
    {
        if (value) Viewer.RefreshFigure();
    }

    public async Task StartAsync()
    {
        await Viewer.StartAsync();
        History.Refresh();
    }

    public async void OnCardChanged(string root, string? serial)
    {
        if (serial is null) cards.Remove(root);
        else cards[root] = serial;
        CardStatus = cards.Count == 0 ? "Cap SD connectada" : string.Join(" · ", cards.Select(c => $"SD {c.Value} · {c.Key.TrimEnd('\\')}"));
        if (serial is not null) await ImportAsync(root, serial);
    }

    public async void OnCardContentChanged(string root)
    {
        if (cards.TryGetValue(root, out var serial)) await ImportAsync(root, serial);
    }

    async Task ImportAsync(string root, string serial)
    {
        var result = await importer.ImportAsync(root, serial, CancellationToken.None);
        await Viewer.LinkModelsAsync();
        History.Refresh();
        if (result.Added > 0)
        {
            var noun = result.Added == 1 ? "gcode nou" : "gcode nous";
            ShowToast($"SD {serial}: {result.Added} {noun}", action: true);
        }
        else if (result.Interrupted) ShowToast($"SD {serial}: no s'ha pogut llegir tota la targeta", action: false);
    }

    void ShowToast(string text) => ShowToast(text, action: false);

    void ShowToast(string text, bool action)
    {
        Toast = text;
        ToastHasAction = action;
        toastTimer.Stop();
        toastTimer.Start();
    }

    void HideToast()
    {
        toastTimer.Stop();
        Toast = null;
    }

    [RelayCommand]
    void ShowHistoryNow()
    {
        HideToast();
        ShowViewer = false;
    }

    void OpenEntry(long gcodeId)
    {
        History.Select(gcodeId);
        ShowViewer = false;
    }

    void OpenModel(string relativePath)
    {
        ShowViewer = true;
        Viewer.ShowModel(relativePath);
    }
}
