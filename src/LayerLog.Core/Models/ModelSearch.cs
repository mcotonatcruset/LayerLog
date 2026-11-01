using System.Globalization;

namespace LayerLog.Core.Models;

public enum FigureFilter { All, Printed, Unprinted }

public static class ModelSearch
{
    const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Every word has to appear in the name or the path (folder included); printed means at least one attempt.</summary>
    public static bool Matches(Figure figure, string query, FigureFilter filter)
    {
        var attempts = figure.Stats.Attempts;
        if (filter == FigureFilter.Printed && attempts == 0) return false;
        if (filter == FigureFilter.Unprinted && attempts > 0) return false;
        var text = $"{figure.Name} {figure.RelativePath}";
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, word, Loose) >= 0);
    }
}
