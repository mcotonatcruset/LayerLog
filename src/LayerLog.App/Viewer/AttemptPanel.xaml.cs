using System.Windows.Controls;
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
}
