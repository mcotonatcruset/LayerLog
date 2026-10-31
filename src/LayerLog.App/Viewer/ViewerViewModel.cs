using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.Core.Creality;
using LayerLog.Core.Data;
using LayerLog.Core.Models;
using LayerLog.Core.Profiles;
using LayerLog.Core.Settings;
using Microsoft.Win32;

namespace LayerLog.App.Viewer;

public sealed record SettingRow(string Label, string Value);
public sealed record FigurePrint(long GcodeId, string FileName, string Detail, BitmapImage? Thumbnail);

public sealed partial class ViewerViewModel(HistoryRepository repository, AppSettings loaded, Action<string> toast, Action<long> openEntry) : ObservableObject
{
    static readonly string[] BestKeys = ["layer_height", "wall_loops", "sparse_infill_density", "outer_wall_speed", "nozzle_temperature", "hot_plate_temp"];

    AppSettings settings = loaded;
    List<ModelFile> models = [];
    CancellationTokenSource? loading;

    public string? ModelFolder => settings.ModelFolder;
    public ObservableCollection<ExplorerNode> Nodes { get; } = [];
    public ObservableCollection<SettingRow> BestRows { get; } = [];
    public ObservableCollection<FigurePrint> Prints { get; } = [];

    /// <summary>The loaded mesh, or null when nothing can be shown.</summary>
    public event Action<StlMesh?>? MeshChanged;

    [ObservableProperty] private string folderText = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool isLoading;
    [ObservableProperty] private string? figureName;
    [ObservableProperty] private string? fileName;
    [ObservableProperty] private string? sizeText;
    [ObservableProperty] private string? bestProfile;
    [ObservableProperty] private string? bestStars;
    [ObservableProperty] private string? bestSpoken;
    [ObservableProperty] private bool hasPrints;
    [ObservableProperty] private string sendText = "Enviar a Creality Print";
    [ObservableProperty] private bool showGroupHint;
    [ObservableProperty] private ExplorerNode? selectedNode;

    public async Task StartAsync()
    {
        var start = StartupState.From(settings, Directory.Exists(AppPaths.DefaultModels) ? AppPaths.DefaultModels : null);
        var folder = start.ModelFolder ?? AskFolder();
        if (folder is null)
        {
            Message = "Tria una carpeta de models per començar.";
            return;
        }
        await OpenFolderAsync(folder);
        if (start.ModelToOpen is not null) ShowModel(start.ModelToOpen);
    }

    public async Task LinkModelsAsync()
    {
        if (ModelFolder is not { } folder) return;
        var scanned = models;
        await Task.Run(() => Link(folder, scanned));
        UpdateCounts();
        if (SelectedNode?.RelativePath is { } path) UpdateFigure(path);
    }

    void Link(string folder, IReadOnlyList<ModelFile> scanned)
    {
        try
        {
            repository.RelinkMovedModels(folder, scanned);
            repository.LinkUnlinked(folder, scanned);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A model vanished mid-scan: the next scan links it.
        }
    }

    async Task OpenFolderAsync(string folder)
    {
        settings = settings with { ModelFolder = folder };
        Persist();
        FolderText = folder;
        models = [.. await Task.Run(() => ModelLibrary.Scan(folder))];
        Nodes.Clear();
        foreach (var node in ExplorerNode.Build(models, UpdateSend)) Nodes.Add(node);
        Message = models.Count == 0 ? "Aquesta carpeta no té cap model." : null;
        await LinkModelsAsync();
    }

    void UpdateCounts()
    {
        var counts = repository.ModelGcodeCounts();
        foreach (var file in Nodes.SelectMany(n => n.Files()))
            file.GcodeCount = counts.GetValueOrDefault(file.RelativePath!);
    }

    partial void OnSelectedNodeChanged(ExplorerNode? value)
    {
        if (value?.RelativePath is { } path) Open(path);
    }

    public void ShowModel(string relativePath)
    {
        var chain = new List<ExplorerNode>();
        if (!Find(Nodes, relativePath, chain))
        {
            toast("Aquest model ja no és a la carpeta.");
            return;
        }
        foreach (var node in chain[..^1]) node.IsExpanded = true;
        chain[^1].IsSelected = true;
        SelectedNode = chain[^1];
    }

    static bool Find(IEnumerable<ExplorerNode> nodes, string path, List<ExplorerNode> chain)
    {
        foreach (var node in nodes)
        {
            chain.Add(node);
            if (node.RelativePath == path || Find(node.Children, path, chain)) return true;
            chain.RemoveAt(chain.Count - 1);
        }
        return false;
    }

    async void Open(string relativePath)
    {
        settings = settings with { LastModel = relativePath };
        Persist();
        UpdateFigure(relativePath);

        loading?.Cancel();
        var cts = loading = new CancellationTokenSource();
        Message = null;
        SizeText = null;
        MeshChanged?.Invoke(null);

        if (!relativePath.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
        {
            Message = "La previsualització de 3MF encara no està disponible.";
            return;
        }

        IsLoading = true;
        try
        {
            var path = Path.Combine(ModelFolder!, relativePath);
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

    public void RefreshFigure()
    {
        if (SelectedNode?.RelativePath is { } path) UpdateFigure(path);
    }

    void UpdateFigure(string relativePath)
    {
        var entries = repository.ListForModel(relativePath);
        FigureName = Path.GetFileNameWithoutExtension(relativePath);
        FileName = Path.GetFileName(relativePath);
        HasPrints = entries.Count > 0;

        Prints.Clear();
        foreach (var e in entries)
        {
            var count = repository.Prints(e.Id).Count;
            var state = count == 0 ? "No imprès" : count == 1 ? "1 impressió" : $"{count} impressions";
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

    void UpdateSend()
    {
        var count = Nodes.SelectMany(n => n.Files()).Count(f => f.IsMarked);
        SendText = count > 1 ? $"Enviar {count} figures a Creality Print" : "Enviar a Creality Print";
        ShowGroupHint = CrealityLauncher.GroupWarningNeeded(count);
    }

    [RelayCommand]
    void OpenPrint(FigurePrint print) => openEntry(print.GcodeId);

    [RelayCommand]
    async Task ChangeFolder()
    {
        if (AskFolder() is { } folder) await OpenFolderAsync(folder);
    }

    string? AskFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Tria la carpeta de models" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    [RelayCommand]
    void Send()
    {
        var marked = Nodes.SelectMany(n => n.Files()).Where(f => f.IsMarked).Select(f => f.RelativePath!).ToList();
        if (marked.Count == 0 && SelectedNode?.RelativePath is { } current) marked.Add(current);
        if (marked.Count == 0)
        {
            toast("Tria una figura primer.");
            return;
        }

        var exe = CrealityLauncher.FindExe(settings.CrealityExe, CrealityLauncher.DefaultRoots) ?? AskExe();
        if (exe is null) return;
        if (exe != settings.CrealityExe)
        {
            settings = settings with { CrealityExe = exe };
            Persist();
        }

        try
        {
            Process.Start(CrealityLauncher.BuildStart(exe, marked.Select(m => Path.Combine(ModelFolder!, m)).ToList()));
            toast(marked.Count == 1 ? $"{Path.GetFileName(marked[0])} obert a Creality Print" : $"{marked.Count} figures obertes a Creality Print");
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            toast("No s'ha pogut obrir Creality Print.");
        }
    }

    static string? AskExe()
    {
        var dialog = new OpenFileDialog { Title = "On és CrealityPrint.exe?", Filter = "Creality Print|CrealityPrint.exe" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    void Persist()
    {
        try
        {
            settings.Save(AppPaths.Settings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            toast("No s'han pogut guardar els ajustos.");
        }
    }
}
