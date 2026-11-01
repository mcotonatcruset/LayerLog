using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LayerLog.App.Collection;

/// <summary>The panel of rows, which can bring a row that isn't realized into view.</summary>
public sealed class RowPanel : VirtualizingStackPanel
{
    public void Bring(int index) => BringIndexIntoView(index);
}

public partial class CollectionView : UserControl
{
    const double MinCardWidth = 240, Gap = 16, SidePadding = 48 + 8;

    CollectionViewModel? Model => DataContext as CollectionViewModel;

    public CollectionView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyLayout();
        // Coming back from a figure, the keyboard carries on from its card.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FocusSelected);
        };
    }

    int column; // where an arrow key lands when it comes down onto a row of cards from a header or an entry

    void FocusSelected()
    {
        if (Model is not { } model) return;
        if (model.OpenOrphanId is { } id)
        {
            var row = model.Rows.ToList().FindIndex(r => r is EntryRow e && e.Entry.Id == id);
            if (row >= 0) FocusStop(row, 0);
        }
        else if (model.Selected is { } path) FocusCard(path);
    }

    public void FocusFirst()
    {
        if (Model is { } model && model.Rows.ToList().FindIndex(IsStop) is var row and >= 0) FocusStop(row, 0);
    }

    /// <summary>A figure shows up to twice, in Recents and in its folder. The folder's copy is the one that stays on screen.</summary>
    void FocusCard(string path, string? instead = null)
    {
        if (Model is not { } model) return;
        var rows = model.Rows.ToList();
        var inFolder = rows.FindIndex(r => r is CardRow c && c.Cards.Any(x => x.Figure.RelativePath == path));
        if (inFolder < 0 && instead is not null)
        {
            FocusCard(instead); // the card left the folder: its neighbour takes the keyboard
            return;
        }
        var inRecents = rows.FindIndex(r => r is HeroRow c && c.Cards.Any(x => x.Figure.RelativePath == path));
        var folder = model.Sections.FirstOrDefault(s => s.Cards.Any(x => x.Figure.RelativePath == path));
        var header = rows.FindIndex(r => r is SectionRow s && s.Section == folder);
        var row = inFolder >= 0 ? inFolder : inRecents >= 0 ? inRecents : header;
        if (row < 0) return;
        var cards = RowCards(rows[row]);
        FocusStop(row, Math.Max(0, cards.ToList().FindIndex(x => x.Figure.RelativePath == path)));
    }

    static IReadOnlyList<FigureCard> RowCards(CollectionRow row) => row switch { HeroRow h => h.Cards, CardRow c => c.Cards, _ => [] };

    // Headers aren't stops, and a section's toggle is disabled while searching.
    static bool IsStop(CollectionRow row) => row is not HeaderRow && row is not SectionRow { Section.IsSearching: true };

    bool FocusStop(int row, int col)
    {
        if (Model is not { } model || Descendants(List).OfType<RowPanel>().FirstOrDefault() is not { } panel) return false;
        panel.Bring(row);
        List.UpdateLayout();
        if (List.ItemContainerGenerator.ContainerFromIndex(row) is not ContentPresenter host) return false;
        host.UpdateLayout();
        var inside = Descendants(host);
        UIElement? target = model.Rows[row] switch
        {
            HeroRow or CardRow => inside.OfType<ListBoxItem>().ElementAtOrDefault(Math.Min(col, RowCards(model.Rows[row]).Count - 1)),
            SectionRow => inside.OfType<ToggleButton>().FirstOrDefault(t => t.Name == "Toggle"),
            _ => inside.OfType<Button>().FirstOrDefault(),
        };
        return target?.Focus() ?? false;
    }

    // Arrows walk the rows, not the screen: a row is its own ListBox, and a virtualized list only has the rows around the view.
    // Up and down visit every row with something to focus; left and right step through the cards, and wrap onto the next row of cards.
    void OnListKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || e.OriginalSource is TextBox || e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right) || Model is not { } model) return;
        if (Keyboard.FocusedElement is not DependencyObject focused || ItemsControl.ContainerFromElement(List, focused) is not ContentPresenter host) return;
        var row = List.ItemContainerGenerator.IndexFromContainer(host);
        if (row < 0) return;
        var rows = model.Rows;
        var isCard = focused is ListBoxItem && RowCards(rows[row]).Count > 0;
        var col = isCard ? ((ListBox)ItemsControl.ItemsControlFromItemContainer(focused)).ItemContainerGenerator.IndexFromContainer(focused) : column;
        if (isCard) column = col;

        var vertical = e.Key is Key.Up or Key.Down;
        if (!vertical && !isCard) return; // left and right on a header button or the parts toggle stay as they are
        e.Handled = true;
        var step = e.Key is Key.Down or Key.Right ? 1 : -1;
        if (vertical)
        {
            for (var next = row + step; next >= 0 && next < rows.Count; next += step)
                if (IsStop(rows[next]) && FocusStop(next, column)) return; // a row that can't take it hands on to the next
        }
        else if (col + step >= 0 && col + step < RowCards(rows[row]).Count) _ = Move(row, col + step);
        else
            for (var next = row + step; next >= 0 && next < rows.Count; next += step)
                if (RowCards(rows[next]).Count > 0 && Move(next, step > 0 ? 0 : RowCards(rows[next]).Count - 1)) return;

        bool Move(int toRow, int toCol)
        {
            column = toCol;
            return FocusStop(toRow, toCol);
        }
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var deeper in Descendants(child)) yield return deeper;
        }
    }

    void OnListResized(object sender, SizeChangedEventArgs e) => ApplyLayout();

    void ApplyLayout() =>
        Model?.SetLayout(Math.Max(1, (int)((List.ActualWidth - SidePadding + Gap) / (MinCardWidth + Gap))), VisualTreeHelper.GetDpi(this).DpiScaleX);

    void OnCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FigureCard card } item && !IsRenaming(item)) Model?.OpenCommand.Execute(card);
    }

    void OnCardKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox || sender is not ListBoxItem { DataContext: FigureCard card } item) return;
        switch (e.Key)
        {
            case Key.Return:
                Model?.OpenCommand.Execute(card);
                break;
            case Key.Space:
                card.IsMarked = !card.IsMarked;
                break;
            case Key.F2:
                StartRenaming(item, card);
                break;
            case Key.P:
                TogglePart(card);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    void OnMenuOpen(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FigureCard card }) Model?.OpenCommand.Execute(card);
    }

    void OnMenuRename(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: FigureCard card, Parent: ContextMenu { PlacementTarget: ListBoxItem item } }) StartRenaming(item, card);
    }

    void OnMenuPart(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FigureCard card }) TogglePart(card);
    }

    // The rows are rebuilt around the card, so the keyboard would be left with nothing: it goes back to the card, or to its neighbour if it left.
    async void TogglePart(FigureCard card)
    {
        if (Model is not { } model) return;
        var path = card.Figure.RelativePath;
        var row = model.Rows.OfType<CardRow>().FirstOrDefault(r => r.Cards.Contains(card));
        var at = row?.Cards.ToList().IndexOf(card) ?? -1;
        var near = row is null ? null : (row.Cards.ElementAtOrDefault(at + 1) ?? row.Cards.ElementAtOrDefault(at - 1))?.Figure.RelativePath;
        await model.TogglePartAsync(card);
        await Dispatcher.InvokeAsync(() => FocusCard(path, near), DispatcherPriority.Loaded);
    }

    static TextBox? Editor(ListBoxItem item) => Descendants(item).OfType<TextBox>().FirstOrDefault();

    static bool IsRenaming(ListBoxItem item) => Editor(item) is { Visibility: Visibility.Visible };

    static void StartRenaming(ListBoxItem item, FigureCard card)
    {
        if (Editor(item) is not { } box) return;
        box.SetCurrentValue(TextBox.TextProperty, card.Figure.Name);
        box.Visibility = Visibility.Visible;
    }

    void OnNameShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true || sender is not TextBox box) return;
        box.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            box.Focus();
            box.SelectAll();
        });
    }

    void OnNameKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Return or Key.Escape) || sender is not TextBox { DataContext: FigureCard card } box) return;
        e.Handled = true;
        if (e.Key == Key.Escape) box.SetCurrentValue(TextBox.TextProperty, card.Figure.Name);
        Descendants(List).OfType<ListBoxItem>().FirstOrDefault(i => Editor(i) == box)?.Focus(); // leaving the box is what saves it
    }

    async void OnNameLeft(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not TextBox { DataContext: FigureCard card, Visibility: Visibility.Visible } box || Model is not { } model) return;
        box.Visibility = Visibility.Collapsed;
        await model.RenameAsync(card, box.Text);
    }
}
