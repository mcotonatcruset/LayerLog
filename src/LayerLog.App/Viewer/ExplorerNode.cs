using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using LayerLog.Core.Models;

namespace LayerLog.App.Viewer;

public sealed partial class ExplorerNode(string name, string? relativePath, Action? onMarkedChanged) : ObservableObject
{
    public string Name { get; } = name;
    public string? RelativePath { get; } = relativePath;
    public bool IsFile => RelativePath is not null;
    public ObservableCollection<ExplorerNode> Children { get; } = [];
    public string Extension => Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();
    public string? Folder => Path.GetDirectoryName(RelativePath);
    public string? ThumbnailPath { get; set; }

    [ObservableProperty] private BitmapImage? thumbnail;
    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isMarked;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasCount))] private int gcodeCount;

    public bool HasCount => GcodeCount > 0;

    partial void OnIsMarkedChanged(bool value) => onMarkedChanged?.Invoke();

    public IEnumerable<ExplorerNode> Files() => IsFile ? [this] : Children.SelectMany(c => c.Files());

    public void Collapse()
    {
        IsExpanded = false;
        foreach (var child in Children) child.Collapse();
    }

    /// <summary>Updates a tree in place, so open folders, marks and the selection survive.</summary>
    public static void Merge(IList<ExplorerNode> current, IReadOnlyList<ExplorerNode> wanted) =>
        ListSync.Apply(current, wanted, (a, b) => a.Name == b.Name && a.IsFile == b.IsFile, (a, b) => Merge(a.Children, [.. b.Children]));

    public static List<ExplorerNode> Build(IEnumerable<ModelFile> models, Action onMarkedChanged)
    {
        var root = new ExplorerNode("", null, null);
        foreach (var model in models)
        {
            var current = root;
            var parts = model.RelativePath.Split(Path.DirectorySeparatorChar);
            foreach (var folder in parts[..^1])
            {
                var next = current.Children.FirstOrDefault(c => !c.IsFile && c.Name == folder);
                if (next is null) current.Children.Add(next = new ExplorerNode(folder, null, null));
                current = next;
            }
            current.Children.Add(new ExplorerNode(parts[^1], model.RelativePath, onMarkedChanged));
        }
        return [.. root.Children];
    }
}
