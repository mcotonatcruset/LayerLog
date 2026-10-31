using System.Windows;
using System.Windows.Interop;
using LayerLog.App.Sd;

namespace LayerLog.App;

public partial class MainWindow : Window
{
    const int WmDeviceChange = 0x0219;

    readonly MainViewModel model = new();
    readonly SdWatcher sd;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = model;
        sd = new SdWatcher(Dispatcher);
        sd.CardChanged += model.OnCardChanged;
        sd.CardContentChanged += model.OnCardContentChanged;
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)!.AddHook(WindowMessages);
        Loaded += async (_, _) =>
        {
            await model.StartAsync();
            sd.Rescan();
        };
        Closed += (_, _) => sd.Dispose();
    }

    nint WindowMessages(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmDeviceChange) sd.RescanSoon();
        return 0;
    }
}
