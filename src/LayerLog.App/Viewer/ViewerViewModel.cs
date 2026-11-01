using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.App.Collection;
using LayerLog.Core.Data;
using LayerLog.Core.Models;
using LayerLog.Core.Profiles;

namespace LayerLog.App.Viewer;

public sealed record SettingRow(string Label, string Value);
public sealed record FigurePrint(long GcodeId, string FileName, string Detail, BitmapImage? Thumbnail);

public sealed partial class ViewerViewModel(HistoryRepository repository, CollectionViewModel collection, Action<long> openEntry) : ObservableObject
{
    static readonly string[] BestKeys = ["layer_height", "wall_loops", "sparse_infill_density", "outer_wall_speed", "nozzle_temperature", "hot_plate_temp"];

    CancellationTokenSource? loading;
    string? shown, shownFolder;

    public ObservableCollection<SettingRow> BestRows { get; } = [];
    public ObservableCollection<FigurePrint> Prints { get; } = [];

    /// <summary>The loaded mesh, or null when nothing can be shown.</summary>
    public event Action<StlMesh?>? MeshChanged;

    [ObservableProperty] private string? message;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? figureName;
    [ObservableProperty] private string? fileName;
    [ObservableProperty] private string? sizeText;
    [ObservableProperty] private string? bestProfile;
    [ObservableProperty] private string? bestStars;
    [ObservableProperty] private string? bestSpoken;
    [ObservableProperty] private bool hasPrints;

    public void OnCollectionRefreshed()
    {
        if (shown is null) return;
        if (shownFolder != collection.ModelFolder || !collection.Has(shown)) ClearFigure("Aquest model ja no és a la carpeta.");
        else UpdateFigure(shown);
    }

    void ClearFigure(string? message)
    {
        loading?.Cancel();
        shown = null;
        collection.RememberModel(null);
        IsLoading = false;
        MeshChanged?.Invoke(null);
        FigureName = null;
        FileName = null;
        SizeText = null;
        BestProfile = null;
        Prints.Clear();
        BestRows.Clear();
        Message = message;
    }

    public async void Open(string relativePath)
    {
        if (relativePath == shown) return;
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
        var entries = repository.ListForModel(relativePath);
        FigureName = collection.NameOf(relativePath);
        FileName = Path.GetFileName(relativePath);
        HasPrints = entries.Count > 0;

        Prints.Clear();
        foreach (var e in entries)
        {
            var count = repository.Prints(e.Id).Count;
            var state = AttemptText.State(count);
            Prints.Add(new FigurePrint(e.Id, e.FileName, $"{Dates.Short(e.GeneratedAt)} · {state}", Images.From(e.Thumbnail)));
        }

        BestRows.Clear();
        var best = entries.Count > 0 ? repository.BestPrint(entries.Select(e => e.Id)) : null;
        BestStars = best is null ? null : Stars.Text(best.Rating);
        BestSpoken = best is null ? null : Stars.Spoken(best.Rating);
        BestProfile = null;
        if (best is null) return;

        var entry = entries.First(e => e.Id == best.GcodeId);
        BestProfile = Profiles.Short(entry.ProcessProfile);
        foreach (var key in BestKeys.Where(entry.Config.ContainsKey))
            BestRows.Add(new SettingRow(ConfigLabels.Label(key), ConfigLabels.Format(key, entry.Config[key].Split(',')[0])));
    }

    [RelayCommand]
    void OpenPrint(FigurePrint print) => openEntry(print.GcodeId);

    [RelayCommand]
    void Send()
    {
        if (shown is not null) collection.SendFigure(shown);
    }
}
