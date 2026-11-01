using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using LayerLog.Core.Models;

namespace LayerLog.App.Collection;

public sealed partial class FigureCard(Figure figure, Action markedChanged) : ObservableObject
{
    /// <summary>The picture's width in Recents, and in a folder, in device-independent pixels.</summary>
    public const double HeroSize = 250, CompactSize = 130;

    public string? ThumbnailPath { get; set; }
    public int DecodedWidth { get; set; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(RatingStars), nameof(RatingNote), nameof(Detail), nameof(PartLabel), nameof(Spoken))]
    private Figure figure = figure;

    [ObservableProperty] private BitmapImage? thumbnail;
    [ObservableProperty] private bool isSelected;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Spoken))] private bool isMarked;

    public string Extension => Path.GetExtension(Figure.RelativePath).TrimStart('.').ToUpperInvariant();
    public string PartLabel => Figure.IsPart ? "Treure de les peces" : "Marcar com a peça";

    public string? RatingStars => Figure.Stats.BestRating is { } rating ? Stars.Text(rating) : null;
    public string? RatingNote => Figure.Stats is { Prints: > 0, BestRating: null } ? Stars.Spoken(null) : null;

    public string Detail
    {
        get
        {
            var stats = Figure.Stats;
            var state = stats.Attempts switch
            {
                0 => AttemptText.State(0),
                1 => $"1 intent · {AttemptText.State(stats.Prints)}",
                var n => $"{n} intents · {AttemptText.State(stats.Prints)}",
            };
            if (stats.BestProfile is { } profile) state += $" · millor: {Profiles.Short(profile)}";
            return Figure.IsPart ? $"Peça · {state}" : state;
        }
    }

    public string Spoken => string.Join(", ", new[] { Figure.Name, Figure.Stats.BestRating is { } r ? Stars.Spoken(r) : RatingNote, Detail, IsMarked ? "seleccionada" : null }.OfType<string>());

    partial void OnIsMarkedChanged(bool value) => markedChanged();
}
