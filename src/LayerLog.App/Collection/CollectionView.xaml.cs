using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace LayerLog.App.Collection;

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

    void FocusSelected()
    {
        if (Descendants(List).OfType<ListBoxItem>().FirstOrDefault(i => i.DataContext is FigureCard { IsSelected: true }) is { } item) item.Focus();
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
            case Key.Up or Key.Down: // each row is its own ListBox, which can't cross to the next one
                var direction = e.Key == Key.Up ? FocusNavigationDirection.Up : FocusNavigationDirection.Down;
                if (item.PredictFocus(direction) is ListBoxItem or ToggleButton) item.MoveFocus(new TraversalRequest(direction));
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

    async void OnMenuPart(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FigureCard card } && Model is { } model) await model.TogglePartAsync(card);
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
