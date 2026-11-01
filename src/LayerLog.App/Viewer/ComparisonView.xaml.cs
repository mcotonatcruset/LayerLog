using System.Windows.Controls;
using System.Windows.Threading;

namespace LayerLog.App.Viewer;

public partial class ComparisonView : UserControl
{
    public ComparisonView()
    {
        InitializeComponent();
        // The keyboard follows the view in; ViewerView sends it back to «Comparar amb…» on the way out.
        IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => BackButton.Focus());
        };
    }
}
