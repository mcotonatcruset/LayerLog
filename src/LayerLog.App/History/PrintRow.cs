using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LayerLog.Core.Data;
using Microsoft.Win32;

namespace LayerLog.App.History;

public sealed record StarChoice(int Value, string Glyph, string Spoken);

public sealed partial class PrintRow : ObservableObject
{
    readonly HistoryRepository repository;
    readonly PhotoStore photos;
    readonly Action changed;
    PrintRecord record;

    [ObservableProperty] private string dateText;
    [ObservableProperty] private bool dateInvalid;
    [ObservableProperty] private string note;
    [ObservableProperty] private BitmapImage? photo;

    public PrintRow(PrintRecord record, HistoryRepository repository, PhotoStore photos, Action changed)
    {
        (this.record, this.repository, this.photos, this.changed) = (record, repository, photos, changed);
        dateText = PrintDate.Format(record.Date);
        note = record.Note;
        photo = Images.From(record.PhotoPath);
    }

    public string RatingText => Stars.Spoken(record.Rating);

    public IReadOnlyList<StarChoice> Choices =>
        Enumerable.Range(1, 5).Select(n => new StarChoice(n, n <= (record.Rating ?? 0) ? "★" : "☆", $"Valorar amb {n} de 5")).ToList();

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
    void Delete()
    {
        repository.DeletePrint(record.Id);
        changed();
    }
}
