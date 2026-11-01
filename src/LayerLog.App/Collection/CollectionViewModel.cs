using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.App.Viewer;
using LayerLog.Core.Creality;
using LayerLog.Core.Data;
using LayerLog.Core.Models;
using LayerLog.Core.Settings;
using Microsoft.Win32;

namespace LayerLog.App.Collection;

public sealed partial class CollectionSection(string folder, IEnumerable<FigureCard> cards, Action changed) : ObservableObject
{
    public string Folder { get; } = folder;
    public List<FigureCard> Cards { get; } = [.. cards];

    [ObservableProperty] private string title = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsOpen))] private bool isExpanded = true;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsOpen))] private bool isSearching;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PartsText))] private bool showParts;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasParts), nameof(PartsText))] private int partCount;

    public bool IsOpen => IsExpanded || IsSearching; // a search opens every section it finds something in
    public bool HasParts => PartCount > 0;
    public string PartsText => ShowParts ? "Amagar les peces" : $"Mostrar les peces ({PartCount})";

    partial void OnIsExpandedChanged(bool value) => changed();
    partial void OnShowPartsChanged(bool value) => changed();
}

// One flat list, so a single virtualized panel scrolls every section.
public abstract record CollectionRow(string Key);
public sealed record HeaderRow(string Title, string? Note = null) : CollectionRow($"h{Title}");
public sealed record SectionRow(CollectionSection Section) : CollectionRow($"s{Section.Folder}");
public sealed record HeroRow(IReadOnlyList<FigureCard> Cards) : CollectionRow("r" + string.Join('|', Cards.Select(c => c.Figure.RelativePath)));
public sealed record CardRow(IReadOnlyList<FigureCard> Cards) : CollectionRow("c" + string.Join('|', Cards.Select(c => c.Figure.RelativePath)));
public sealed record EntryRow(GcodeEntry Entry) : CollectionRow($"e{Entry.Id}")
{
    public string Date => Dates.Short(Entry.GeneratedAt);
}

public sealed partial class CollectionViewModel(HistoryRepository repository, AppSettings loaded, Action<string> toast, Action<string> open, Action<long> openOrphan) : ObservableObject
{
    const int RecentCount = 5;
    const double MaxThumbnail = ModelThumbnail.Size;

    AppSettings settings = loaded;
    List<ModelFile> models = [];
    CancellationTokenSource? thumbnails;
    ModelWatcher? watcher;
    FigureFilter filter;
    string? selected;
    double dpiScale = 1;
    bool refreshing, refreshAgain, scanned;

    public string? ModelFolder => settings.ModelFolder;
    public bool HasMarks => MarkedText is not null;
    /// <summary>The figure open or last open: where the keyboard goes back to.</summary>
    public string? Selected => selected;
    /// <summary>A gcode with no figure that is open, so its row gets the keyboard back.</summary>
    public long? OpenOrphanId { get; private set; }
    public ObservableCollection<CollectionSection> Sections { get; } = [];
    public ObservableCollection<FigureCard> Recent { get; } = [];
    public ObservableCollection<GcodeEntry> WithoutFigure { get; } = [];
    public ObservableCollection<CollectionRow> Rows { get; } = [];

    /// <summary>Models or their figure data changed: names, attempts, or which ones exist.</summary>
    public event Action? Refreshed;

    [ObservableProperty] private string folderText = "";
    [ObservableProperty] private string query = "";
    [ObservableProperty] private string? message;
    [ObservableProperty] private bool needsFolder;
    [ObservableProperty] private int columns = 5;
    [ObservableProperty] private string sendText = "Enviar a Creality Print";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasMarks))] private string? markedText;
    [ObservableProperty] private bool showGroupHint;
    [ObservableProperty] private bool hasRows;

    IEnumerable<FigureCard> Cards => Sections.SelectMany(s => s.Cards);
    IEnumerable<FigureCard> Marked => Cards.Where(c => c.IsMarked);
    bool Searching => !string.IsNullOrWhiteSpace(Query) || filter != FigureFilter.All;

    /// <summary>Opens the remembered folder. Returns the figure to reopen, known before the slower scan.</summary>
    public string? Start(out Task scan)
    {
        var start = StartupState.From(settings, Directory.Exists(AppPaths.DefaultModels) ? AppPaths.DefaultModels : null);
        if (start.ModelFolder is { } folder) scan = OpenFolderAsync(folder);
        else
        {
            settings = settings with { ModelFolder = null }; // gone, or never chosen
            RefreshOrphans();
            scan = Task.CompletedTask;
        }
        return start.ModelToOpen;
    }

    /// <summary>Until the first scan ends nothing is known to be missing, so everything counts as there.</summary>
    public bool Has(string relativePath) => ModelFolder is not null && (!scanned || models.Any(m => m.RelativePath == relativePath));

    public string NameOf(string relativePath) =>
        Cards.FirstOrDefault(c => c.Figure.RelativePath == relativePath)?.Figure.Name // no cards yet while a remembered figure opens
        ?? repository.FigureMetas().GetValueOrDefault(relativePath)?.Alias
        ?? Path.GetFileNameWithoutExtension(relativePath);

    /// <summary>A figure's folder as shown to the user: the path inside the model folder, or that folder's name at its root.</summary>
    public string FolderTitle(string folder) =>
        folder.Length > 0 ? folder.Replace(Path.DirectorySeparatorChar.ToString(), " / ") : Path.GetFileName(ModelFolder!.TrimEnd(Path.DirectorySeparatorChar));

    public void RememberModel(string? relativePath)
    {
        selected = relativePath;
        if (relativePath is not null) OpenOrphanId = null;
        settings = settings with { LastModel = relativePath };
        Persist();
        ShowSelected();
    }

    public void RememberOrphan(long gcodeId) => OpenOrphanId = gcodeId;

    void ShowSelected()
    {
        foreach (var card in Cards) card.IsSelected = card.Figure.RelativePath == selected;
    }

    public void SetLayout(int columnCount, double scale)
    {
        Columns = columnCount;
        if (scale == dpiScale) return;
        dpiScale = scale;
        LoadThumbnails();
    }

    partial void OnColumnsChanged(int value) => UpdateRows();

    public async Task LinkModelsAsync()
    {
        if (ModelFolder is not { } folder)
        {
            RefreshOrphans(); // no folder: every gcode is without a figure, and still worth browsing
            return;
        }
        var scanned = models;
        await Task.Run(() => Link(folder, scanned));
        await UpdateFiguresAsync();
    }

    void RefreshOrphans()
    {
        ListSync.Apply(WithoutFigure, repository.EntriesWithoutFigure(new HashSet<string>()), (a, b) => a.Id == b.Id);
        UpdateRows();
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
        Message = null;
        NeedsFolder = false;
        Sections.Clear();
        Recent.Clear();
        WithoutFigure.Clear();
        Rows.Clear();
        models = [];
        scanned = false;
        watcher?.Dispose();
        watcher = new ModelWatcher(folder, Dispatcher.CurrentDispatcher, RefreshOnChange);
        await RefreshModelsAsync();
    }

    async void RefreshOnChange() => await RefreshModelsAsync();

    async Task RefreshModelsAsync()
    {
        if (refreshing)
        {
            refreshAgain = true;
            return;
        }
        refreshing = true;
        try
        {
            do
            {
                refreshAgain = false;
                var folder = ModelFolder!;
                models = [.. await Task.Run(() => ModelLibrary.Scan(folder))];
                scanned = true;
                await LinkModelsAsync();
            }
            while (refreshAgain);
        }
        finally
        {
            refreshing = false;
        }
    }

    /// <summary>Merges into what's on screen, so collapsed sections, marks and the selection survive.</summary>
    public async Task UpdateFiguresAsync()
    {
        if (!scanned) return; // an empty list here means not known yet, not an empty folder
        var current = models;
        var paths = current.Select(m => m.RelativePath).ToHashSet();
        var (metas, stats, orphans) = await Task.Run(() => (repository.FigureMetas(), repository.FigureStats(), repository.EntriesWithoutFigure(paths)));
        if (!ReferenceEquals(current, models)) return; // a newer scan took over and will finish this

        var figures = FigureCatalog.Build(current, metas, stats);
        var wanted = figures.GroupBy(f => f.Folder).Select(g => new CollectionSection(g.Key, g.Select(f => new FigureCard(f, UpdateSend)), UpdateRows)).ToList();
        ListSync.Apply(Sections, wanted, (a, b) => a.Folder == b.Folder,
            (a, b) => ListSync.Apply(a.Cards, b.Cards, (x, y) => x.Figure.RelativePath == y.Figure.RelativePath, (x, y) => x.Figure = y.Figure));
        foreach (var section in Sections) section.Title = FolderTitle(section.Folder);

        var cards = Cards.ToDictionary(c => c.Figure.RelativePath);
        ListSync.Apply(Recent, [.. FigureCatalog.Recent(figures, RecentCount).Select(f => cards[f.RelativePath])], ReferenceEquals);
        ListSync.Apply(WithoutFigure, orphans, (a, b) => a.Id == b.Id);

        ShowSelected();
        UpdateRows();
        UpdateSend();
        Refreshed?.Invoke();
        LoadThumbnails();
    }

    // One at a time and off the UI thread: a large STL takes a moment to read and the cards fill in as they arrive.
    // ponytail: every thumbnail is decoded and held, visible or not, about 70 KB each at 100 %; load them per realized row if folders reach tens of thousands
    async void LoadThumbnails()
    {
        if (ModelFolder is null) return;
        thumbnails?.Cancel();
        var cts = thumbnails = new CancellationTokenSource();
        var folder = ModelFolder!;
        var cards = Cards.ToDictionary(c => c.Figure.RelativePath);
        var recent = Recent.ToHashSet();
        foreach (var file in models.Where(m => m.RelativePath.EndsWith(".stl", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (!cards.TryGetValue(file.RelativePath, out var card)) continue;
            var size = recent.Contains(card) ? FigureCard.HeroSize : FigureCard.CompactSize;
            var width = (int)Math.Ceiling(Math.Min(size * dpiScale, MaxThumbnail));
            var path = ThumbnailStore.PathFor(folder, file);
            if (card.ThumbnailPath == path && card.DecodedWidth == width) continue;
            try
            {
                var image = await Task.Run(() => Images.From(ThumbnailStore.Load(folder, file, path), width));
                if (cts.IsCancellationRequested) return;
                card.Thumbnail = image;
            }
            catch (Exception e) when (e is StlFormatException or IOException or UnauthorizedAccessException)
            {
                // Without a picture the card shows its file type, and the viewer reports the error if it is opened.
                if (cts.IsCancellationRequested) return;
            }
            card.ThumbnailPath = path;
            card.DecodedWidth = width;
        }
    }

    partial void OnQueryChanged(string value) => UpdateRows();

    // ponytail: rebuilt on every keystroke, O(figures); fine for thousands, debounce the query beyond that
    void UpdateRows()
    {
        var rows = new List<CollectionRow>();
        if (!Searching && Recent.Count > 0)
        {
            rows.Add(new HeaderRow("Recents"));
            rows.AddRange(Recent.Chunk(Columns).Select(c => new HeroRow(c)));
        }

        var found = 0;
        foreach (var section in Sections)
        {
            var matching = section.Cards.Where(c => ModelSearch.Matches(c.Figure, Query, filter)).ToList();
            section.PartCount = matching.Count(c => c.Figure.IsPart);
            section.IsSearching = Searching;
            if (matching.Count == 0) continue;
            found += matching.Count;
            rows.Add(new SectionRow(section));
            if (section.IsOpen)
                rows.AddRange(matching.Where(c => section.ShowParts || !c.Figure.IsPart).Chunk(Columns).Select(c => new CardRow(c)));
        }

        if (!Searching && WithoutFigure.Count > 0)
        {
            rows.Add(new HeaderRow("Sense figura", "Obre'n un per assignar-li el model"));
            rows.AddRange(WithoutFigure.Select(e => new EntryRow(e)));
        }

        ListSync.Apply(Rows, rows, (a, b) => a.Key == b.Key);
        HasRows = rows.Count > 0;
        ShowEmptyState(found);
    }

    void ShowEmptyState(int found = 0)
    {
        NeedsFolder = ModelFolder is null || (scanned && models.Count == 0);
        Message = ModelFolder is null ? "Tria una carpeta de models per començar."
            : !scanned ? null // not known yet: neither «empty» nor «no match» is true
            : models.Count == 0 ? "Aquesta carpeta no té cap model."
            : found == 0 ? "Cap figura coincideix amb la cerca." : null;
    }

    void UpdateSend()
    {
        var count = Marked.Count();
        SendText = count > 1 ? $"Enviar {count} figures a Creality Print" : "Enviar a Creality Print";
        MarkedText = count switch { 0 => null, 1 => "1 figura seleccionada", _ => $"{count} figures seleccionades" };
        ShowGroupHint = CrealityLauncher.GroupWarningNeeded(count);
    }

    [RelayCommand]
    void SetFilter(FigureFilter value)
    {
        filter = value;
        UpdateRows();
    }

    [RelayCommand]
    void ClearQuery() => Query = "";

    [RelayCommand]
    void ClearMarks()
    {
        foreach (var card in Cards) card.IsMarked = false;
    }

    [RelayCommand]
    void Open(FigureCard card) => open(card.Figure.RelativePath);

    // Any row: a recycled container briefly shows its next row through the old template, and the command must take it.
    [RelayCommand]
    void OpenEntry(CollectionRow row)
    {
        if (row is EntryRow entry) openOrphan(entry.Entry.Id);
    }

    [RelayCommand]
    void CollapseAll()
    {
        foreach (var section in Sections) section.IsExpanded = false;
    }

    public async Task RenameAsync(FigureCard card, string name)
    {
        if (name.Trim() == card.Figure.Name) return;
        await WriteMetaAsync([card], (file, hash) => repository.SetAlias(file.RelativePath, hash, file.Size, name), "No s'ha pogut canviar el nom.");
    }

    /// <summary>Applies to every marked figure when the card is one of them.</summary>
    public async Task TogglePartAsync(FigureCard card)
    {
        var isPart = !card.Figure.IsPart;
        var targets = card.IsMarked ? Marked.ToList() : [card];
        await WriteMetaAsync(targets, (file, hash) => repository.SetIsPart(file.RelativePath, hash, file.Size, isPart), "No s'ha pogut marcar la peça.");
    }

    async Task WriteMetaAsync(IReadOnlyList<FigureCard> targets, Action<ModelFile, string> write, string failure)
    {
        var folder = ModelFolder!;
        var files = models.Where(m => targets.Any(c => c.Figure.RelativePath == m.RelativePath)).ToList();
        try
        {
            await Task.Run(() =>
            {
                foreach (var file in files) write(file, ModelLibrary.HashFile(Path.Combine(folder, file.RelativePath)));
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            toast(failure);
        }
        await UpdateFiguresAsync();
    }

    [RelayCommand]
    async Task ChangeFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Tria la carpeta de models" };
        if (dialog.ShowDialog() == true) await OpenFolderAsync(dialog.FolderName);
    }

    [RelayCommand]
    void Send() => Launch(Marked.Select(c => c.Figure.RelativePath).ToList());

    public void SendFigure(string relativePath) => Launch([relativePath]);

    void Launch(IReadOnlyList<string> figures)
    {
        if (figures.Count == 0) return;
        var exe = CrealityLauncher.FindExe(settings.CrealityExe, CrealityLauncher.DefaultRoots) ?? AskExe();
        if (exe is null) return;
        if (exe != settings.CrealityExe)
        {
            settings = settings with { CrealityExe = exe };
            Persist();
        }

        try
        {
            Process.Start(CrealityLauncher.BuildStart(exe, figures.Select(m => Path.Combine(ModelFolder!, m)).ToList()));
            toast(figures.Count == 1 ? $"{Path.GetFileName(figures[0])} obert a Creality Print" : $"{figures.Count} figures obertes a Creality Print");
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
