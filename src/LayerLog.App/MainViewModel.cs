using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.App.Collection;
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
    Action? toastAction;

    public CollectionViewModel Collection { get; }
    public ViewerViewModel Viewer { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowCollection), nameof(ShowFigure))] private Screen screen;
    [ObservableProperty] private string cardStatus = "Cap SD connectada";
    [ObservableProperty] private string? toast;
    [ObservableProperty] private bool toastHasAction;

    public bool ShowCollection => Screen == Screen.Collection;
    public bool ShowFigure => Screen == Screen.Figure;

    public MainViewModel()
    {
        Directory.CreateDirectory(AppPaths.DataFolder);
        repository = new HistoryRepository(AppPaths.Database);
        importer = new GcodeImporter(repository, new ProfileLibrary(AppPaths.CrealityProfiles));
        Collection = new CollectionViewModel(repository, AppSettings.Load(AppPaths.Settings), ShowToast, OpenFigure, OpenOrphan);
        Viewer = new ViewerViewModel(repository, Collection, new AttemptViewModel(repository, new PhotoStore(AppPaths.Photos), Collection, OpenFigure, ShowToast, CloseAttempt));
        Collection.Refreshed += Viewer.OnCollectionRefreshed;
        toastTimer.Tick += (_, _) => HideToast();
    }

    public async Task StartAsync()
    {
        // Picks the screen before the scan, so a remembered figure never flashes the collection.
        if (Collection.Start(out var scan) is { } remembered) ShowModel(remembered);
        await scan;
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
        if (result.Added > 0)
        {
            var noun = result.Added == 1 ? "gcode nou" : "gcode nous";
            ShowToast($"SD {serial}: {result.Added} {noun}", () => OpenAttempt(result.Newest!.Value));
        }
        else if (result.Interrupted) ShowToast($"SD {serial}: no s'ha pogut llegir tota la targeta");
    }

    void ShowToast(string text) => ShowToast(text, null);

    void ShowToast(string text, Action? action)
    {
        Toast = text;
        toastAction = action;
        ToastHasAction = action is not null;
        toastTimer.Stop();
        toastTimer.Start();
    }

    void HideToast()
    {
        toastTimer.Stop();
        Toast = null;
    }

    [RelayCommand]
    void RunToastAction()
    {
        var action = toastAction;
        HideToast();
        action?.Invoke();
    }

    [RelayCommand]
    void Back()
    {
        Viewer.CloseAttempt();
        Screen = Screen.Collection;
    }

    [RelayCommand]
    void ShowFigureNow()
    {
        Viewer.CloseAttempt();
        Screen = Screen.Figure;
    }

    [RelayCommand]
    void Escape()
    {
        if (ShowCollection) Collection.Query = "";
        else if (Viewer.Attempt.IsComparing) Viewer.Attempt.CloseComparisonCommand.Execute(null);
        else if (Viewer.SelectedAttempt is not null) CloseAttempt();
        else Back();
    }

    void CloseAttempt()
    {
        Viewer.CloseAttempt();
        if (Viewer.FigureName is null) Screen = Screen.Collection; // a gcode opened with no figure behind it
    }

    /// <summary>The newest gcode's figure, or the gcode alone when it has none.</summary>
    void OpenAttempt(long gcodeId)
    {
        if (repository.ObjectLinks(gcodeId).FirstOrDefault(l => l.ModelPath is { } path && Collection.Has(path)) is { } link) OpenFigure(link.ModelPath!);
        else OpenOrphan(gcodeId);
    }

    void OpenOrphan(long gcodeId)
    {
        ShowFigureNow();
        Viewer.OpenOrphan(gcodeId);
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
