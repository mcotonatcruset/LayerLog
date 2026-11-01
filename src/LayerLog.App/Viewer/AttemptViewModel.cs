using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
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
public sealed record CompareRow(string Label, string A, string B);
/// <param name="Group">The figure's attempts or the other gcode: the selector's two headings.</param>
public sealed record CompareChoice(long GcodeId, string FileName, string Detail, string Group)
{
    public string Name => $"{FileName}, {Detail}";
}

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
    List<(ObjectLink Link, bool IsLinked)>? shownLinks;

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
    [ObservableProperty] private bool hasFigure;
    [ObservableProperty] private bool hasPrints;
    [ObservableProperty] private bool showFullConfig;
    [ObservableProperty] private string? fullConfigTitle;
    [ObservableProperty] private string? fullConfig;

    [ObservableProperty] private bool isChoosing;
    [ObservableProperty] private bool isComparing;
    [ObservableProperty] private bool showRest;
    [ObservableProperty] private string? compareTitle;
    [ObservableProperty] private string? compareRestTitle;
    [ObservableProperty] private string? nameA;
    [ObservableProperty] private string? nameB;
    [ObservableProperty] private BitmapImage? thumbA;
    [ObservableProperty] private BitmapImage? thumbB;
    [ObservableProperty] private string? compareNote;
    [ObservableProperty] private string? compareCaution;
    [ObservableProperty] private bool canCompare;
    public string CompareHint => CanCompare ? "Comparar amb un altre gcode" : "No hi ha cap altre gcode";
    partial void OnCanCompareChanged(bool value) => OnPropertyChanged(nameof(CompareHint));
    long comparedId;

    public ObservableCollection<CompareRow> CompareRows { get; } = [];
    public ObservableCollection<CompareRow> CompareRest { get; } = [];
    /// <summary>The figure's other attempts, then every other gcode. Built when the selector opens.</summary>
    public ICollectionView? CompareChoices { get; private set; }

    /// <param name="gcodeId">The attempt to show.</param>
    /// <param name="openFigure">The figure it was opened from, or null when it has none.</param>
    public void Show(long gcodeId, string? openFigure = null)
    {
        this.gcodeId = gcodeId;
        CloseComparison();
        figure = openFigure;
        shownLinks = null;
        var e = repository.Get(gcodeId);
        FileName = e.FileName;
        Thumbnail = Images.From(e.Thumbnail);
        ShowMeta(e);

        var total = e.ProcessChanges.Count + e.FilamentChanges.Count;
        ChangesTitle = $"Què vas canviar ({total})";
        Caution = AttemptText.Caution(e);
        CanCompare = repository.List().Count > 1;

        Changes.Clear();
        AddGroup(e.ProcessProfile, e.ProcessChanges);
        AddGroup(e.FilamentProfile, e.FilamentChanges);

        ShowFullConfig = e.BaseMissing;
        FullConfigTitle = $"Configuració completa ({e.Config.Count} paràmetres)";
        FullConfig = string.Join('\n', e.Config.OrderBy(p => p.Key).Select(p => $"{p.Key} = {p.Value}"));

        LoadFigures();
        LoadPrints();
    }

    /// <summary>An import or a scan may have moved its cards or links. Prints stay: rebuilding them would take the focus from a note being typed.</summary>
    public void Refresh()
    {
        ShowMeta(repository.Get(gcodeId));
        LoadFigures();
    }

    void ShowMeta(GcodeEntry e)
    {
        var version = string.Join('.', e.Version.Split('.').Take(2));
        var grams = e.FilamentGrams is { } g ? $" · {g:0} g" : "";
        var time = e.EstimatedTime is null ? "" : $" · {e.EstimatedTime}";
        NoteMeta = $"{Dates.Short(e.GeneratedAt)} · Creality Print {version}{time}{grams}\n{CardText(e)}";
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
        var links = repository.ObjectLinks(gcodeId).Where(l => l.ModelPath is null || l.ModelPath != figure)
            .Select(l => (Link: l, IsLinked: l.ModelPath is { } path && collection.Has(path))).ToList();
        if (shownLinks is not null && links.SequenceEqual(shownLinks)) return; // unchanged rows keep the keyboard focus
        shownLinks = links;
        Figures.Clear();
        foreach (var (link, isLinked) in links) Figures.Add(new FigureLink(this, link, isLinked));
        FiguresTitle = Figures.Count == 0 ? null : figure is null ? "Figures" : "Altres figures";
        HasFigure = Figures.Any(f => f.IsLinked);
        CanAssign = Figures.Count > 0 && !HasFigure;
    }

    void LoadPrints()
    {
        Prints.Clear();
        foreach (var p in repository.Prints(gcodeId)) Prints.Add(Row(p));
        HasPrints = Prints.Count > 0;
    }

    PrintRow Row(PrintRecord p) => new(p, repository, photos, PrintsChanged, PrintDeleted);

    async void PrintsChanged()
    {
        if (IsComparing) BuildComparison(); // the best rating may have changed
        await collection.UpdateFiguresAsync();
    }

    void PrintDeleted(PrintRow row)
    {
        Prints.Remove(row);
        HasPrints = Prints.Count > 0;
        PrintsChanged();
    }

    [RelayCommand]
    void AddPrint()
    {
        var id = repository.AddPrint(gcodeId, DateOnly.FromDateTime(DateTime.Today), null, "", null);
        Prints.Insert(0, Row(repository.Prints(gcodeId).First(p => p.Id == id))); // in place, so a half-typed date on another row survives
        HasPrints = true;
        PrintsChanged();
    }

    [RelayCommand]
    void Close() => close();

    partial void OnIsChoosingChanged(bool value)
    {
        if (!value) return;
        var mine = figure is null ? [] : repository.ListForModel(figure).Select(e => e.Id).ToHashSet();
        var all = repository.List().Where(e => e.Id != gcodeId).ToList();
        var choices = all.Where(e => mine.Contains(e.Id)).Select(e => Choice(e, "Intents d'aquesta figura"))
            .Concat(all.Where(e => !mine.Contains(e.Id)).Select(e => Choice(e, mine.Count == 0 ? "Tots els gcode" : "Altres gcode"))).ToList();
        CompareChoices = new ListCollectionView(choices) { GroupDescriptions = { new PropertyGroupDescription(nameof(CompareChoice.Group)) } };
        OnPropertyChanged(nameof(CompareChoices));
    }

    static CompareChoice Choice(GcodeEntry e, string group) => new(e.Id, e.FileName, $"{Dates.Short(e.GeneratedAt)} · {Profiles.Short(e.ProcessProfile)}", group);

    [RelayCommand]
    void CompareWith(CompareChoice choice)
    {
        IsChoosing = false;
        comparedId = choice.GcodeId;
        ShowRest = false;
        BuildComparison();
        IsComparing = true;
    }

    void BuildComparison()
    {
        var (a, b) = (repository.Get(gcodeId), repository.Get(comparedId));
        var result = AttemptComparison.Between(a, b);
        var (known, rest) = (result.Known, result.Rest);

        (ThumbA, ThumbB, NameA, NameB) = (Images.From(a.Thumbnail), Images.From(b.Thumbnail), a.FileName, b.FileName);
        CompareNote = result.NoConfig ? "Aquest gcode no té configuració" : null;
        CompareTitle = AttemptText.ComparisonTitle(result);
        CompareCaution = AttemptText.ComparisonCaution(a, b);
        CompareRows.Clear();
        foreach (var s in known) CompareRows.Add(new CompareRow(s.Label, s.A, s.B));
        CompareRows.Add(new CompareRow("Millor valoració", Best(a.Id), Best(b.Id)));
        CompareRest.Clear();
        foreach (var s in rest) CompareRest.Add(new CompareRow(s.Label, s.A, s.B));
        CompareRestTitle = AttemptText.RestTitle(rest.Count, known.Count);
    }

    string Best(long id) => repository.BestPrint([id])?.Rating is { } r ? $"{Stars.Text(r)}  {Stars.Spoken(r)}" : Stars.Text(null);

    [RelayCommand]
    public void CloseComparison()
    {
        IsChoosing = false;
        IsComparing = false;
    }

    public void OpenFigure(string relativePath) => openFigure(relativePath);

    [RelayCommand]
    Task AssignFirst() => AssignFigure(Figures.First(f => !f.IsLinked).Name);

    public async Task AssignFigure(string objectName)
    {
        if (collection.ModelFolder is not { } folder)
        {
            toast("Tria primer una carpeta de models.");
            return;
        }
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
