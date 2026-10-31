using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace LayerLog.App;

public static class AppPaths
{
    public static string DataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LayerLog");
    public static string Database => Path.Combine(DataFolder, "layerlog.db");
    public static string Settings => Path.Combine(DataFolder, "settings.json");
    public static string Photos => Path.Combine(DataFolder, "photos");
    public static string CrealityProfiles => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Creality", "Creality Print");
    public static string DefaultModels => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Impresora3D");
}

public static class Images
{
    public static BitmapImage? From(byte[]? bytes)
    {
        if (bytes is null) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            return Load(i => i.StreamSource = stream);
        }
        catch (Exception e) when (e is NotSupportedException or FileFormatException)
        {
            return null;
        }
    }

    public static BitmapImage? From(string? path)
    {
        if (path is null || !File.Exists(path)) return null;
        try
        {
            return Load(i => { i.UriSource = new Uri(path); i.DecodePixelWidth = 480; });
        }
        catch (Exception e) when (e is NotSupportedException or FileFormatException or IOException)
        {
            return null;
        }
    }

    static BitmapImage Load(Action<BitmapImage> source)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        source(image);
        image.EndInit();
        image.Freeze();
        return image;
    }
}

public static class Dates
{
    static readonly CultureInfo Catalan = CultureInfo.GetCultureInfo("ca-ES");
    public static string Short(DateTime d) => d.ToString("d MMM yyyy", Catalan);
    public static string Short(DateOnly d) => d.ToString("d MMM yyyy", Catalan);
}

public static class Stars
{
    public static string Text(int? rating) => rating is { } r ? new string('★', r) + new string('☆', 5 - r) : "Sense valorar";
    public static string Spoken(int? rating) => rating is { } r ? $"{r} de 5" : "Sense valorar";
}

public static class Profiles
{
    // "0.12mm Fine @Creality Ender-3 V3 SE 0.4 nozzle - Copiar(1)" -> "0.12mm Fine (personalitzat)"
    public static string Short(string profile)
    {
        var at = profile.IndexOf(" @", StringComparison.Ordinal);
        var name = at > 0 ? profile[..at] : profile;
        return profile.Contains(" - Copiar") ? $"{name} (personalitzat)" : name;
    }
}

public sealed class NotBoolToVisibility : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class NullToCollapsed : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c) =>
        value is null or "" ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
