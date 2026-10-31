using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.Core.Data;
using LayerLog.Core.Models;
using LayerLog.Core.Profiles;
using Microsoft.Win32;

namespace LayerLog.App.History;

public sealed record ChangeRow(string Label, string Base, string Value);
public sealed record ChangeGroup(string Caption, IReadOnlyList<ChangeRow> Rows);
public sealed record CompareRow(string Label, string A, string B);

public sealed partial class FigureLink(HistoryViewModel owner, long gcodeId, ObjectLink link) : ObservableObject
{
    public string Name => link.Name;
    public string? ModelPath => link.ModelPath;
    public bool IsLinked => link.ModelPath is not null;

    [RelayCommand]
    void Open() => owner.OpenFigure(link.ModelPath!);

    [RelayCommand]
    void Assign() => owner.AssignFigure(gcodeId, link.Name);
}

public sealed partial class EntryRow(GcodeEntry entry, string state, Action markedChanged) : ObservableObject
{
    public GcodeEntry Entry { get; } = entry;
    public BitmapImage? Thumbnail { get; } = Images.From(entry.Thumbnail);
    public string Detail { get; } = $"{Dates.Short(entry.GeneratedAt)} · {Profiles.Short(entry.ProcessProfile)}";
    public string State { get; } = state;

    [ObservableProperty] private bool isMarked;

    partial void OnIsMarkedChanged(bool value) => markedChanged();
}

public sealed partial class HistoryViewModel(HistoryRepository repository, PhotoStore photos, Action<string> openModel, Func<string?> modelFolder) : ObservableObject
{
    public ObservableCollection<EntryRow> Entries { get; } = [];
    public ObservableCollection<ChangeGroup> Changes { get; } = [];
    public ObservableCollection<PrintRow> Prints { get; } = [];
    public ObservableCollection<FigureLink> Figures { get; } = [];
    public ObservableCollection<CompareRow> CompareRows { get; } = [];

    [ObservableProperty] private string search = "";
    [ObservableProperty] private EntryRow? selected;
    [ObservableProperty] private bool isEmpty = true;
    [ObservableProperty] private bool isComparing;
    [ObservableProperty] private bool showNote;
    [ObservableProperty] private string? changesTitle;
    [ObservableProperty] private string? noteMeta;
    [ObservableProperty] private string? caution;
    [ObservableProperty] private bool baseMissing;
    [ObservableProperty] private string? fullConfigTitle;
    [ObservableProperty] private string? fullConfig;
    [ObservableProperty] private string compareText = "Marca dues entrades per comparar-les";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(CompareCommand))] private bool canCompare;
    [ObservableProperty] private BitmapImage? thumbA;
    [ObservableProperty] private BitmapImage? thumbB;
    [ObservableProperty] private string? nameA;
    [ObservableProperty] private string? nameB;
    [ObservableProperty] private string? compareTitle;
    [ObservableProperty] private string? compareRest;
    [ObservableProperty] private string? compareRestTitle;

    public void Refresh()
    {
        var keep = Selected?.Entry.Id;
        var marked = Entries.Where(e => e.IsMarked).Select(e => e.Entry.Id).ToHashSet();
        Entries.Clear();
        foreach (var entry in repository.List(Search))
        {
            var count = repository.Prints(entry.Id).Count;
            var state = count == 0 ? "No imprès" : count == 1 ? "1 impressió" : $"{count} impressions";
            Entries.Add(new EntryRow(entry, state, UpdateMarks) { IsMarked = marked.Contains(entry.Id) });
        }
        IsEmpty = Entries.Count == 0 && string.IsNullOrEmpty(Search);
        Selected = Entries.FirstOrDefault(e => e.Entry.Id == keep) ?? (IsComparing ? null : Entries.FirstOrDefault());
        UpdateMarks();
    }

    public void Select(long gcodeId)
    {
        Search = "";
        IsComparing = false;
        Refresh();
        Selected = Entries.FirstOrDefault(e => e.Entry.Id == gcodeId);
    }

    partial void OnSearchChanged(string value) => Refresh();

    partial void OnSelectedChanged(EntryRow? value)
    {
        if (value is null)
        {
            ShowNote = false;
            return;
        }
        IsComparing = false;
        ShowEntry(value.Entry);
    }

    void ShowEntry(GcodeEntry e)
    {
        ShowNote = true;
        var version = string.Join('.', e.Version.Split('.').Take(2));
        var grams = e.FilamentGrams is { } g ? $" · {g:0} g" : "";
        var time = e.EstimatedTime is null ? "" : $" · {e.EstimatedTime}";
        NoteMeta = $"{Dates.Short(e.GeneratedAt)} · Creality Print {version}{time}{grams}\n{CardText(e)}";

        var total = e.ProcessChanges.Count + e.FilamentChanges.Count;
        ChangesTitle = $"Què vas canviar ({total})";
        BaseMissing = e.BaseMissing;
        Caution = e.BaseApproximate && e.BaseSourceVersion is not null
            ? $"Comparació aproximada: no hi ha perfils de la versió {version}, s'ha fet servir la {e.BaseSourceVersion}."
            : null;

        Changes.Clear();
        AddGroup(e.ProcessProfile, e.ProcessChanges);
        AddGroup(e.FilamentProfile, e.FilamentChanges);

        FullConfigTitle = $"Configuració completa ({e.Config.Count} paràmetres)";
        FullConfig = string.Join('\n', e.Config.OrderBy(p => p.Key).Select(p => $"{p.Key} = {p.Value}"));

        Figures.Clear();
        foreach (var link in repository.ObjectLinks(e.Id)) Figures.Add(new FigureLink(this, e.Id, link));

        LoadPrints(e.Id);
    }

    static string CardText(GcodeEntry e) =>
        e.VolumeSerials.Count == 0 ? "Ja no és a cap SD" : string.Join(", ", e.VolumeSerials.Select(s => $"SD {s}"));

    void AddGroup(string profile, IReadOnlyList<ConfigChange> changes)
    {
        if (changes.Count == 0) return;
        Changes.Add(new ChangeGroup(Profiles.Short(profile),
            changes.Select(c => new ChangeRow(ConfigLabels.Label(c.Key), ConfigLabels.Format(c.Key, c.Base), ConfigLabels.Format(c.Key, c.Value))).ToList()));
    }

    void LoadPrints(long gcodeId)
    {
        Prints.Clear();
        foreach (var p in repository.Prints(gcodeId)) Prints.Add(new PrintRow(p, repository, photos, () => PrintsChanged(gcodeId)));
    }

    void PrintsChanged(long gcodeId)
    {
        LoadPrints(gcodeId);
        Refresh();
    }

    [RelayCommand]
    void AddPrint()
    {
        if (Selected is not { } row) return;
        repository.AddPrint(row.Entry.Id, DateOnly.FromDateTime(DateTime.Today), null, "", null);
        PrintsChanged(row.Entry.Id);
    }

    public void OpenFigure(string relativePath) => openModel(relativePath);

    public void AssignFigure(long gcodeId, string objectName)
    {
        if (modelFolder() is not { } folder) return;
        var dialog = new OpenFileDialog { Title = $"Quin model és {objectName}?", InitialDirectory = folder, Filter = "Models|*.stl;*.3mf" };
        if (dialog.ShowDialog() != true) return;

        var relative = Path.GetRelativePath(folder, dialog.FileName);
        var size = new FileInfo(dialog.FileName).Length;
        repository.LinkObject(gcodeId, objectName, relative, ModelLibrary.HashFile(dialog.FileName), manual: true, size);
        Figures.Clear();
        foreach (var link in repository.ObjectLinks(gcodeId)) Figures.Add(new FigureLink(this, gcodeId, link));
    }

    void UpdateMarks()
    {
        var marked = Entries.Where(e => e.IsMarked).ToList();
        if (marked.Count > 2)
        {
            marked[0].IsMarked = false; // re-enters with two left
            return;
        }
        CanCompare = marked.Count == 2;
        CompareText = CanCompare ? "2 entrades marcades" : "Marca dues entrades per comparar-les";
    }

    [RelayCommand(CanExecute = nameof(CanCompare))]
    void Compare()
    {
        var marked = Entries.Where(e => e.IsMarked).ToList();
        var (a, b) = (marked[0], marked[1]);
        var differences = ConfigDiff.Between(a.Entry.Config, b.Entry.Config);
        var known = differences.Where(d => ConfigLabels.IsKnown(d.Key)).ToList();
        var rest = differences.Where(d => !ConfigLabels.IsKnown(d.Key)).ToList();

        (ThumbA, ThumbB, NameA, NameB) = (a.Thumbnail, b.Thumbnail, a.Entry.FileName, b.Entry.FileName);
        CompareTitle = differences.Count == 1 ? "1 diferència" : $"{differences.Count} diferències";
        CompareRows.Clear();
        foreach (var d in known)
            CompareRows.Add(new CompareRow(ConfigLabels.Label(d.Key), ConfigLabels.Format(d.Key, d.Base), ConfigLabels.Format(d.Key, d.Value)));
        CompareRows.Add(new CompareRow("Millor valoració", Best(a), Best(b)));
        CompareRestTitle = rest.Count == 0 ? null : $"{rest.Count} diferències més";
        CompareRest = string.Join('\n', rest.Select(d => $"{d.Key}: {d.Base} → {d.Value}"));

        Selected = null;
        IsComparing = true;
    }

    string Best(EntryRow row) => Stars.Text(repository.BestPrint([row.Entry.Id])?.Rating);
}
