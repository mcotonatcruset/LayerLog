using System.Globalization;

namespace LayerLog.Core.Models;

public enum FigureFilter { All, Printed, Unprinted }

public static class ModelSearch
{
    const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Every word has to appear in the name or the path (folder included); printed means at least one real print, not just a gcode.</summary>
    public static bool Matches(Figure figure, string query, FigureFilter filter)
    {
        var prints = figure.Stats.Prints;
        if (filter == FigureFilter.Printed && prints == 0) return false;
        if (filter == FigureFilter.Unprinted && prints > 0) return false;
        var text = $"{figure.Name} {figure.RelativePath}";
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, word, Loose) >= 0);
    }
}
