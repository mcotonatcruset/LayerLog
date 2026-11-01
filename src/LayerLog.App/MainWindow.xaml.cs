using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LayerLog.App.Collection;
using LayerLog.App.Sd;

namespace LayerLog.App;

public partial class MainWindow : Window
{
    const int WmDeviceChange = 0x0219, SmCxFrame = 32, SmCxPaddedBorder = 92;

    [DllImport("user32.dll")]
    static extern int GetSystemMetrics(int index);

    readonly MainViewModel model = new();
    readonly SdWatcher sd;
    bool started;

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
            started = true; // the remembered figure opens without the transition
            sd.Rescan();
        };
        Closed += (_, _) => sd.Dispose();
        StateChanged += (_, _) => ShowWindowState();
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Screen) && started && model.ShowFigure) FadeIn(FigureScreen);
        };
    }

    // One short fade, from the collection into a figure. Windows asks for no animation: none.
    static void FadeIn(UIElement element)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = new QuadraticEase() }, HandoffBehavior.SnapshotAndReplace);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        if (e.Key == Key.K) FocusSearch();
        else if (e.Key == Key.Return && Keyboard.FocusedElement is not TextBox) SendFromKeyboard(); // never while typing
        else return;
        e.Handled = true;
    }

    void FocusSearch()
    {
        if (model.ShowFigure) model.BackCommand.Execute(null);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Search.Focus();
            Search.SelectAll();
        });
    }

    // On a figure: that figure. In the collection: the selected cards, or the one the keyboard is on.
    void SendFromKeyboard()
    {
        if (model.ShowFigure) { if (!model.Viewer.ShowOrphan) model.Viewer.SendCommand.Execute(null); }
        else if (model.Collection.HasMarks) model.Collection.SendCommand.Execute(null);
        else if (Keyboard.FocusedElement is FrameworkElement { DataContext: FigureCard card }) model.Collection.SendFigure(card.Figure.RelativePath);
    }

    // From the search box the arrows and Return carry on into the results.
    void OnSearchKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Down or Key.Return) || Keyboard.Modifiers != ModifierKeys.None) return;
        CollectionScreen.FocusFirst();
        e.Handled = true;
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
