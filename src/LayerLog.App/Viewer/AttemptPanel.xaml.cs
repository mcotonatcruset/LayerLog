using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LayerLog.App.Viewer;

public partial class AttemptPanel : UserControl
{
    public AttemptPanel()
    {
        InitializeComponent();
        // The panel is reused, so each attempt starts at the top with the keyboard on the way back.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is not true) return;
            Scroller.ScrollToTop();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => BackButton.Focus());
        };
    }

    readonly HashSet<FrameworkElement> asking = [];

    // Opening starts on Cancel·lar, the first button, and cancelling returns to the «Esborrar» that asked.
    // A row is built visible and then bound to collapsed, which also raises this: judged when it runs, that is nothing to move.
    void OnConfirmShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        var confirm = (FrameworkElement)sender;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (confirm.IsVisible)
            {
                asking.Add(confirm);
                confirm.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
            else if (asking.Remove(confirm)) (confirm.Tag as IInputElement)?.Focus();
        });
    }

    public void FocusCompare() => CompareButton.Focus();

    // Arrows move the highlight, Return or a click picks, Esc closes the list and nothing else.
    void OnChoicesOpened(object sender, EventArgs e) => Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
    {
        if (Choices.Items.Count == 0) return;
        Choices.SelectedIndex = 0;
        (Choices.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
    });

    void OnChoicesKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ((AttemptViewModel)DataContext).IsChoosing = false;
            CompareButton.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Return && Choices.SelectedItem is CompareChoice choice)
        {
            Pick(choice);
            e.Handled = true;
        }
    }

    void OnChoiceClicked(object sender, MouseButtonEventArgs e)
    {
        if (((ListBoxItem)sender).DataContext is CompareChoice choice) Pick(choice);
    }

    void Pick(CompareChoice choice) => ((AttemptViewModel)DataContext).CompareWithCommand.Execute(choice);

    void OnDeleted(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AddPrintButton.Focus());
}
