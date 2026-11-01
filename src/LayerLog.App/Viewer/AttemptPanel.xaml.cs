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

    // Opening starts on Cancel·lar, the first button, and cancelling returns to the «Esborrar» that asked.
    void OnConfirmShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        var confirm = (FrameworkElement)sender;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (e.NewValue is true) confirm.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            else (confirm.Tag as IInputElement)?.Focus();
        });
    }

    void OnDeleted(object sender, RoutedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AddPrintButton.Focus());
}
