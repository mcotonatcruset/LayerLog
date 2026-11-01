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

    CollectionViewModel Model => (CollectionViewModel)DataContext;

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
        (DataContext as CollectionViewModel)?.SetLayout(Math.Max(1, (int)((List.ActualWidth - SidePadding + Gap) / (MinCardWidth + Gap))), VisualTreeHelper.GetDpi(this).DpiScaleX);

    void OnCardClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: FigureCard { IsRenaming: false } card }) Model.OpenCommand.Execute(card);
    }

    void OnCardKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox || sender is not ListBoxItem { DataContext: FigureCard card } item) return;
        switch (e.Key)
        {
            case Key.Return:
                Model.OpenCommand.Execute(card);
                break;
            case Key.Space:
                card.IsMarked = !card.IsMarked;
                break;
            case Key.F2:
                card.IsRenaming = true;
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

    void OnMenuOpen(object sender, RoutedEventArgs e) => Model.OpenCommand.Execute(CardOf(sender));

    void OnMenuRename(object sender, RoutedEventArgs e) => CardOf(sender).IsRenaming = true;

    async void OnMenuPart(object sender, RoutedEventArgs e) => await Model.TogglePartAsync(CardOf(sender));

    static FigureCard CardOf(object element) => (FigureCard)((FrameworkElement)element).DataContext;

    void OnNameShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        var box = (TextBox)sender;
        box.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            box.Focus();
            box.SelectAll();
        });
    }

    void OnNameKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Return or Key.Escape)) return;
        e.Handled = true;
        var box = (TextBox)sender;
        var card = (FigureCard)box.DataContext;
        var item = Descendants(List).OfType<ListBoxItem>().FirstOrDefault(i => Descendants(i).Contains(box));
        if (e.Key == Key.Escape) box.Text = card.Figure.Name;
        item?.Focus(); // leaving the box is what saves it
    }

    async void OnNameLeft(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var card = (FigureCard)box.DataContext;
        if (!card.IsRenaming) return;
        card.IsRenaming = false;
        await Model.RenameAsync(card, box.Text);
    }
}
