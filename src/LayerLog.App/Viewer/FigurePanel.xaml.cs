using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace LayerLog.App.Viewer;

public partial class FigurePanel : UserControl
{
    public FigurePanel()
    {
        InitializeComponent();
        // Coming back from an attempt the focused control has just gone: the keyboard carries on here.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => MoveFocus(new TraversalRequest(FocusNavigationDirection.First)));
        };
    }
}
