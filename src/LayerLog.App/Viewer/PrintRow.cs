using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.Core.Data;
using LayerLog.Core.Models;
using Microsoft.Win32;

namespace LayerLog.App.Viewer;

// Stable items whose glyph changes: replacing them would take the keyboard focus off the star.
public sealed partial class StarChoice(int value) : ObservableObject
{
    public int Value { get; } = value;
    public string Spoken { get; } = $"Valorar amb {value} de 5";
    public override string ToString() => Spoken;

    [ObservableProperty] private string glyph = "☆";
}

public sealed partial class PrintRow : ObservableObject
{
    readonly HistoryRepository repository;
    readonly PhotoStore photos;
    readonly Action changed;
    readonly Action<PrintRow> deleted;
    PrintRecord record;

    [ObservableProperty] private string dateText;
    [ObservableProperty] private bool dateInvalid;
    [ObservableProperty] private string note;
    [ObservableProperty] private BitmapImage? photo;
    [ObservableProperty] private string? deleteWarning;

    public PrintRow(PrintRecord record, HistoryRepository repository, PhotoStore photos, Action changed, Action<PrintRow> deleted)
    {
        (this.record, this.repository, this.photos, this.changed, this.deleted) = (record, repository, photos, changed, deleted);
        dateText = PrintDate.Format(record.Date);
        note = record.Note;
        photo = Images.From(record.PhotoPath);
        PaintStars();
    }

    public override string ToString() => $"Impressió, {RatingText}";
    public string RatingText => Stars.Spoken(record.Rating);

    public IReadOnlyList<StarChoice> Choices { get; } = [.. Enumerable.Range(1, 5).Select(n => new StarChoice(n))];

    public bool IsConfirmingDelete => DeleteWarning is not null;

    partial void OnDeleteWarningChanged(string? value) => OnPropertyChanged(nameof(IsConfirmingDelete));

    void PaintStars()
    {
        foreach (var choice in Choices) choice.Glyph = choice.Value <= (record.Rating ?? 0) ? "★" : "☆";
        OnPropertyChanged(nameof(RatingText));
    }

    // A half-typed date just stays unsaved: the field is marked until it parses.
    partial void OnDateTextChanged(string value)
    {
        var parsed = PrintDate.Parse(value);
        DateInvalid = parsed is null;
        if (parsed is { } date) Save(record with { Date = date });
    }

    partial void OnNoteChanged(string value) => Save(record with { Note = value });

    void Save(PrintRecord updated)
    {
        record = updated;
        repository.UpdatePrint(updated);
    }

    [RelayCommand]
    void Rate(StarChoice choice)
    {
        // Clicking the current rating clears it, so a print can go back to unrated.
        Save(record with { Rating = record.Rating == choice.Value ? null : choice.Value });
        PaintStars();
        changed();
    }

    [RelayCommand]
    void AddPhoto()
    {
        var dialog = new OpenFileDialog { Title = "Tria una foto", Filter = "Imatges|*.jpg;*.jpeg;*.png;*.webp" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            Save(record with { PhotoPath = photos.Save(dialog.FileName, record.Id) });
            Photo = Images.From(record.PhotoPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Photo = null;
        }
    }

    [RelayCommand]
    void AskDelete() => DeleteWarning = AttemptText.DeleteWarning(record);

    [RelayCommand]
    void CancelDelete() => DeleteWarning = null;

    [RelayCommand]
    void Delete()
    {
        repository.DeletePrint(record.Id);
        deleted(this);
    }
}
