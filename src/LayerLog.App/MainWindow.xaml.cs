using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using LayerLog.App.Sd;

namespace LayerLog.App;

public partial class MainWindow : Window
{
    const int WmDeviceChange = 0x0219, SmCxFrame = 32, SmCxPaddedBorder = 92;

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int index);

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
        StateChanged += (_, _) => ShowWindowState();
    }

    void OnMinimize(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);
    void OnMaximize(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
        else SystemCommands.MaximizeWindow(this);
    }
    void OnClose(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    void ShowWindowState()
    {
        var maximized = WindowState == WindowState.Maximized;
        // A maximized window overhangs the screen by its frame on all sides, which would clip the edges. SystemParameters leaves out the padded border.
        var frame = (GetSystemMetrics(SmCxFrame) + GetSystemMetrics(SmCxPaddedBorder)) / VisualTreeHelper.GetDpi(this).DpiScaleX;
        Root.Margin = new Thickness(maximized ? frame : 0);
        var name = maximized ? "Restaurar" : "Maximitzar";
        MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = name;
        AutomationProperties.SetName(MaximizeButton, name);
    }

    nint WindowMessages(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmDeviceChange) sd.RescanSoon();
        return 0;
    }
}
