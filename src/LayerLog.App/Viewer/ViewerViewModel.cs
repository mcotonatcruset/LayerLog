using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.App.Collection;
using LayerLog.Core.Data;
using LayerLog.Core.Models;
using LayerLog.Core.Profiles;

namespace LayerLog.App.Viewer;

public sealed record AttemptRow(long GcodeId, string FileName, string Detail, string State, bool IsRated, string Spoken)
{
    public string Name => $"{FileName}, {Spoken}";
}

public sealed partial class ViewerViewModel(HistoryRepository repository, CollectionViewModel collection, AttemptViewModel attempt) : ObservableObject
{
    const int KeyChangeCount = 4;

    CancellationTokenSource? loading;
    string? shown, shownFolder;

    public AttemptViewModel Attempt => attempt;
    public ObservableCollection<ChangeRow> BestChanges { get; } = [];
    public ObservableCollection<AttemptRow> Attempts { get; } = [];

    /// <summary>The loaded mesh, or null when nothing can be shown.</summary>
    public event Action<StlMesh?>? MeshChanged;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SceneMessage))] private string? message;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowLoading))] private bool isLoading;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowOrphan), nameof(SceneMessage), nameof(ShowLoading))] private string? orphanText;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowSummary))] private string? figureName;
    [ObservableProperty] private string? place;
    [ObservableProperty] private string? sizeText;
    [ObservableProperty] private bool hasAttempts;
    [ObservableProperty] private string? attemptsTitle;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ShowSummary))] private AttemptRow? selectedAttempt;
    [ObservableProperty] private AttemptRow? bestAttempt;
    [ObservableProperty] private string? bestStars;
    [ObservableProperty] private string? bestSpoken;
    [ObservableProperty] private string? bestNote;
    [ObservableProperty] private string? bestDetail;
    [ObservableProperty] private string? bestCaution;
    [ObservableProperty] private string? allChangesText;

    public bool ShowSummary => FigureName is not null && SelectedAttempt is null;
    /// <summary>A gcode opened on its own. Any figure stays loaded and remembered behind it, out of the scene.</summary>
    public bool ShowOrphan => OrphanText is not null;
    public string? SceneMessage => OrphanText ?? Message;
    public bool ShowLoading => IsLoading && !ShowOrphan;

    public void OnCollectionRefreshed()
    {
        if (shown is not null)
        {
            if (shownFolder != collection.ModelFolder || !collection.Has(shown)) ClearFigure("Aquest model ja no és a la carpeta.");
            else UpdateFigure(shown);
        }
        if (SelectedAttempt is null) return;
        attempt.Refresh();
        if (ShowOrphan) OrphanText = OrphanMessage();
    }

    string OrphanMessage() => attempt.HasFigure ? "Aquest gcode ja té una figura." : "Aquest gcode no té cap figura assignada.";

    void ClearFigure(string? message)
    {
        loading?.Cancel();
        shown = null;
        collection.RememberModel(null);
        IsLoading = false;
        MeshChanged?.Invoke(null);
        FigureName = null;
        Place = null;
        SizeText = null;
        ShowAttempts([]);
        Message = message;
    }

    public async void Open(string relativePath)
    {
        OrphanText = null;
        attempt.CloseComparison();
        SelectedAttempt = null;
        if (relativePath == shown)
        {
            UpdateFigure(relativePath); // the same figure may have just gained an attempt
            return;
        }
        shown = relativePath;
        shownFolder = collection.ModelFolder;
        collection.RememberModel(relativePath);
        UpdateFigure(relativePath);

        loading?.Cancel();
        var cts = loading = new CancellationTokenSource();
        Message = null;
        SizeText = null;
        IsLoading = false;
        MeshChanged?.Invoke(null);

        if (!relativePath.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
        {
            Message = "La previsualització de 3MF encara no està disponible.";
            return;
        }

        IsLoading = true;
        try
        {
            var path = Path.Combine(shownFolder!, relativePath);
            var mesh = await Task.Run(() => StlReader.Read(path), cts.Token);
            if (cts.IsCancellationRequested) return;
            SizeText = $"{Mm(mesh.Size.X)} × {Mm(mesh.Size.Z)} × {Mm(mesh.Size.Y)} mm";
            MeshChanged?.Invoke(mesh);
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is StlFormatException or IOException or UnauthorizedAccessException)
        {
            if (!cts.IsCancellationRequested) Message = "No s'ha pogut llegir el fitxer.";
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsLoading = false;
        }
    }

    static string Mm(float value) => value.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("ca-ES"));

    void UpdateFigure(string relativePath)
    {
        FigureName = collection.NameOf(relativePath);
        Place = $"{collection.FolderTitle(Path.GetDirectoryName(relativePath) ?? "")} · {Path.GetFileName(relativePath)}";
        ShowAttempts(repository.ListForModel(relativePath));
    }

    void ShowAttempts(IReadOnlyList<GcodeEntry> entries)
    {
        var rows = entries.Select(Row).ToList();
        var best = entries.Count > 0 ? repository.BestPrint(entries.Select(e => e.Id)) : null;
        var entry = entries.FirstOrDefault(e => e.Id == best?.GcodeId);
        var others = rows.Where(r => r.GcodeId != entry?.Id).ToList();

        HasAttempts = entries.Count > 0;
        BestAttempt = rows.FirstOrDefault(r => r.GcodeId == entry?.Id);
        if (!ShowOrphan) SelectedAttempt = rows.FirstOrDefault(r => r.GcodeId == SelectedAttempt?.GcodeId);
        AttemptsTitle = others.Count == 0 ? null : best is null ? "Intents" : "Altres intents";
        Attempts.Clear();
        foreach (var row in others) Attempts.Add(row);

        BestChanges.Clear();
        BestStars = best is null ? null : Stars.Text(best.Rating);
        BestSpoken = best is null ? null : Stars.Spoken(best.Rating);
        BestNote = string.IsNullOrWhiteSpace(best?.Note) ? null : best.Note.Trim();
        BestDetail = entry is null ? null : $"{entry.FileName} · {Dates.Short(best!.Date)} · {Profiles.Short(entry.ProcessProfile)}";
        BestCaution = entry is null ? null : AttemptText.Caution(entry);
        AllChangesText = null;
        if (entry is null) return;

        var changes = entry.ProcessChanges.Concat(entry.FilamentChanges).ToList();
        foreach (var c in KeyChanges.Pick(changes, KeyChangeCount))
            BestChanges.Add(new ChangeRow(ConfigLabels.Label(c.Key), ConfigLabels.Format(c.Key, c.Base), ConfigLabels.Format(c.Key, c.Value)));
        AllChangesText = changes.Count switch { 0 => "Veure l'intent", 1 => "Veure el canvi", var n => $"Tots els {n} canvis" };
    }

    AttemptRow Row(GcodeEntry e)
    {
        var prints = repository.Prints(e.Id);
        var rating = prints.Max(p => p.Rating);
        var state = AttemptText.State(prints.Count);
        var parts = new[] { Dates.Short(e.GeneratedAt), Profiles.Short(e.ProcessProfile), e.EstimatedTime, e.FilamentGrams is { } g ? $"{g:0} g" : null };
        return new AttemptRow(e.Id, e.FileName, string.Join(" · ", parts.OfType<string>()), rating is null ? state : Stars.Text(rating), rating is not null,
            rating is null ? state : Stars.Spoken(rating));
    }

    [RelayCommand]
    void SelectAttempt(AttemptRow row)
    {
        attempt.Show(row.GcodeId, shown);
        SelectedAttempt = row;
    }

    /// <summary>A gcode with no figure: the panel opens on an empty scene.</summary>
    public void OpenOrphan(long gcodeId)
    {
        attempt.Show(gcodeId);
        OrphanText = OrphanMessage();
        SelectedAttempt = Row(repository.Get(gcodeId));
    }

    /// <summary>Notes and dates save without a refresh, so the summary catches up here.</summary>
    public async void CloseAttempt()
    {
        if (SelectedAttempt is null) return;
        attempt.CloseComparison();
        OrphanText = null;
        SelectedAttempt = null;
        await collection.UpdateFiguresAsync();
    }

    [RelayCommand]
    void Send()
    {
        if (shown is not null) collection.SendFigure(shown);
    }
}
