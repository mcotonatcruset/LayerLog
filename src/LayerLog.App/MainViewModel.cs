using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.App.Collection;
using LayerLog.App.History;
using LayerLog.App.Viewer;
using LayerLog.Core.Data;
using LayerLog.Core.Import;
using LayerLog.Core.Profiles;
using LayerLog.Core.Settings;

namespace LayerLog.App;

public enum Screen { Collection, Figure }

public sealed partial class MainViewModel : ObservableObject
{
    readonly HistoryRepository repository;
    readonly GcodeImporter importer;
    readonly DispatcherTimer toastTimer = new() { Interval = TimeSpan.FromSeconds(8) };
    readonly Dictionary<string, string> cards = [];

    public CollectionViewModel Collection { get; }
    public ViewerViewModel Viewer { get; }
    public HistoryViewModel History { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowCollection), nameof(ShowFigure))] private Screen screen;
    // ponytail: the history still sits on top of the two screens; it goes in Task 8
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowCollection), nameof(ShowFigure))] private bool showHistory;
    [ObservableProperty] private string cardStatus = "Cap SD connectada";
    [ObservableProperty] private string? toast;
    [ObservableProperty] private bool toastHasAction;

    public bool ShowCollection => !ShowHistory && Screen == Screen.Collection;
    public bool ShowFigure => !ShowHistory && Screen == Screen.Figure;

    public MainViewModel()
    {
        Directory.CreateDirectory(AppPaths.DataFolder);
        repository = new HistoryRepository(AppPaths.Database);
        importer = new GcodeImporter(repository, new ProfileLibrary(AppPaths.CrealityProfiles));
        Collection = new CollectionViewModel(repository, AppSettings.Load(AppPaths.Settings), ShowToast, OpenFigure);
        Viewer = new ViewerViewModel(repository, Collection, OpenEntry);
        Collection.Refreshed += Viewer.OnCollectionRefreshed;
        History = new HistoryViewModel(repository, new PhotoStore(AppPaths.Photos), OpenFigure, () => Collection.ModelFolder);
        toastTimer.Tick += (_, _) => HideToast();
    }

    // Prints and links are edited in the history, so the figures may be stale on the way back.
    async partial void OnShowHistoryChanged(bool value)
    {
        if (!value) await Collection.UpdateFiguresAsync();
    }

    public async Task StartAsync()
    {
        // Picks the screen before the scan, so a remembered figure never flashes the collection.
        if (Collection.Start(out var scan) is { } remembered) ShowModel(remembered);
        await scan;
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
        await Collection.LinkModelsAsync();
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
        ShowHistory = true;
    }

    [RelayCommand]
    void Back()
    {
        ShowHistory = false;
        Screen = Screen.Collection;
    }

    [RelayCommand]
    void ShowFigureNow()
    {
        ShowHistory = false;
        Screen = Screen.Figure;
    }

    // ponytail: Esc does nothing in the history, whose note boxes use it; that view goes in Task 8
    [RelayCommand]
    void Escape()
    {
        if (ShowFigure) Back();
        else if (ShowCollection) Collection.Query = "";
    }

    void OpenEntry(long gcodeId)
    {
        History.Select(gcodeId);
        ShowHistory = true;
    }

    public void OpenFigure(string relativePath)
    {
        if (Collection.Has(relativePath)) ShowModel(relativePath);
        else ShowToast("Aquest model ja no és a la carpeta.");
    }

    void ShowModel(string relativePath)
    {
        ShowFigureNow();
        Viewer.Open(relativePath);
    }
}
