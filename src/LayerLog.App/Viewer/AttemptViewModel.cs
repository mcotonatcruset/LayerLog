using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.App.Collection;
using LayerLog.Core.Data;
using LayerLog.Core.Models;
using LayerLog.Core.Profiles;
using Microsoft.Win32;

namespace LayerLog.App.Viewer;

public sealed record ChangeRow(string Label, string Base, string Value);
public sealed record ChangeGroup(string Caption, IReadOnlyList<ChangeRow> Rows);

public sealed partial class FigureLink(AttemptViewModel owner, ObjectLink link, bool isLinked) : ObservableObject
{
    public string Name => link.Name;
    public bool IsLinked => isLinked;

    [RelayCommand]
    void Open() => owner.OpenFigure(link.ModelPath!);

    [RelayCommand]
    Task Assign() => owner.AssignFigure(link.Name);
}

public sealed partial class AttemptViewModel(HistoryRepository repository, PhotoStore photos, CollectionViewModel collection,
    Action<string> openFigure, Action<string> toast, Action close) : ObservableObject
{
    long gcodeId;
    string? figure;

    public ObservableCollection<ChangeGroup> Changes { get; } = [];
    public ObservableCollection<PrintRow> Prints { get; } = [];
    public ObservableCollection<FigureLink> Figures { get; } = [];

    [ObservableProperty] private string? fileName;
    [ObservableProperty] private BitmapImage? thumbnail;
    [ObservableProperty] private string? noteMeta;
    [ObservableProperty] private string? changesTitle;
    [ObservableProperty] private string? caution;
    [ObservableProperty] private string? figuresTitle;
    [ObservableProperty] private bool canAssign;
    [ObservableProperty] private bool hasPrints;
    [ObservableProperty] private bool showFullConfig;
    [ObservableProperty] private string? fullConfigTitle;
    [ObservableProperty] private string? fullConfig;

    /// <param name="gcodeId">The attempt to show.</param>
    /// <param name="openFigure">The figure it was opened from, or null when it has none.</param>
    public void Show(long gcodeId, string? openFigure = null)
    {
        this.gcodeId = gcodeId;
        figure = openFigure;
        var e = repository.Get(gcodeId);
        FileName = e.FileName;
        Thumbnail = Images.From(e.Thumbnail);
        var version = string.Join('.', e.Version.Split('.').Take(2));
        var grams = e.FilamentGrams is { } g ? $" · {g:0} g" : "";
        var time = e.EstimatedTime is null ? "" : $" · {e.EstimatedTime}";
        NoteMeta = $"{Dates.Short(e.GeneratedAt)} · Creality Print {version}{time}{grams}\n{CardText(e)}";

        var total = e.ProcessChanges.Count + e.FilamentChanges.Count;
        ChangesTitle = $"Què vas canviar ({total})";
        Caution = AttemptText.Caution(e);

        Changes.Clear();
        AddGroup(e.ProcessProfile, e.ProcessChanges);
        AddGroup(e.FilamentProfile, e.FilamentChanges);

        ShowFullConfig = e.BaseMissing;
        FullConfigTitle = $"Configuració completa ({e.Config.Count} paràmetres)";
        FullConfig = string.Join('\n', e.Config.OrderBy(p => p.Key).Select(p => $"{p.Key} = {p.Value}"));

        LoadFigures();
        LoadPrints();
    }

    static string CardText(GcodeEntry e) =>
        e.VolumeSerials.Count == 0 ? "Ja no és a cap SD" : string.Join(", ", e.VolumeSerials.Select(s => $"SD {s}"));

    void AddGroup(string profile, IReadOnlyList<ConfigChange> changes)
    {
        if (changes.Count == 0) return;
        Changes.Add(new ChangeGroup(Profiles.Short(profile),
            changes.Select(c => new ChangeRow(ConfigLabels.Label(c.Key), ConfigLabels.Format(c.Key, c.Base), ConfigLabels.Format(c.Key, c.Value))).ToList()));
    }

    // A link to a model that's gone counts as unlinked: that's how a gcode lands in «Sense figura», and it needs assigning again.
    void LoadFigures()
    {
        Figures.Clear();
        foreach (var link in repository.ObjectLinks(gcodeId).Where(l => l.ModelPath is null || l.ModelPath != figure))
            Figures.Add(new FigureLink(this, link, link.ModelPath is { } path && collection.Has(path)));
        FiguresTitle = Figures.Count == 0 ? null : figure is null ? "Figures" : "Altres figures";
        CanAssign = Figures.Any(f => !f.IsLinked);
    }

    void LoadPrints()
    {
        Prints.Clear();
        foreach (var p in repository.Prints(gcodeId)) Prints.Add(new PrintRow(p, repository, photos, PrintsChanged));
        HasPrints = Prints.Count > 0;
    }

    async void PrintsChanged()
    {
        LoadPrints();
        await collection.UpdateFiguresAsync();
    }

    [RelayCommand]
    void AddPrint()
    {
        repository.AddPrint(gcodeId, DateOnly.FromDateTime(DateTime.Today), null, "", null);
        PrintsChanged();
    }

    [RelayCommand]
    void Close() => close();

    public void OpenFigure(string relativePath) => openFigure(relativePath);

    [RelayCommand]
    Task AssignFirst() => AssignFigure(Figures.First(f => !f.IsLinked).Name);

    public async Task AssignFigure(string objectName)
    {
        if (collection.ModelFolder is not { } folder) return;
        var dialog = new OpenFileDialog { Title = $"Quin model és {objectName}?", InitialDirectory = folder, Filter = "Models|*.stl;*.3mf" };
        if (dialog.ShowDialog() != true) return;

        var relative = Path.GetRelativePath(folder, dialog.FileName);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            toast("El model ha de ser dins de la carpeta de models.");
            return;
        }
        var size = new FileInfo(dialog.FileName).Length;
        repository.LinkObject(gcodeId, objectName, relative, ModelLibrary.HashFile(dialog.FileName), manual: true, size);
        await collection.UpdateFiguresAsync();
        if (figure is null) openFigure(relative);
        else LoadFigures();
    }
}
