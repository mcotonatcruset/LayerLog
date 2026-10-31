using System.Globalization;

namespace LayerLog.Core.Data;

public static class PrintDate
{
    static readonly string[] Formats = ["d/M/yyyy", "dd/MM/yyyy"];

    public static DateOnly? Parse(string text) =>
        DateOnly.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    public static string Format(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
