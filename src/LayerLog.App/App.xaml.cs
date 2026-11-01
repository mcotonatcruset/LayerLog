using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace LayerLog.App;

public partial class App : Application
{
    public App() => DispatcherUnhandledException += OnUnhandled;

    // Last resort: async void handlers end up here, and a closing window with no message helps nobody.
    void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            File.AppendAllText(Path.Combine(AppPaths.DataFolder, "errors.log"), $"{DateTime.Now:s} {e.Exception}\n\n");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nowhere to write it: the message below still shows.
        }
        e.Handled = true;
        if (MainWindow?.IsVisible != true)
        {
            // Failed before the window came up (a corrupt database, say): nothing to continue with, and no invisible process left behind.
            MessageBox.Show("LayerLog no s'ha pogut iniciar. Mira errors.log a la carpeta de dades.", "LayerLog", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }
        MessageBox.Show("Hi ha hagut un error inesperat. Pots continuar fent servir LayerLog.", "LayerLog", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
