using System.Globalization;

namespace LayerLog.Core.Models;

public enum FigureFilter { All, Printed, Unprinted }

public static class ModelSearch
{
    const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Every word has to appear somewhere in the path, so a folder name narrows the search too.</summary>
    public static bool Matches(string relativePath, int gcodeCount, string query, FigureFilter filter)
    {
        if (filter == FigureFilter.Printed && gcodeCount == 0) return false;
        if (filter == FigureFilter.Unprinted && gcodeCount > 0) return false;
        return query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => CultureInfo.InvariantCulture.CompareInfo.IndexOf(relativePath, word, Loose) >= 0);
    }
}
