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
        return AllWordsIn(relativePath, query);
    }

    /// <summary>Every word has to appear in the name or the path (folder included); printed means at least one attempt.</summary>
    public static bool Matches(Figure figure, string query, FigureFilter filter)
    {
        var attempts = figure.Stats.Attempts;
        if (filter == FigureFilter.Printed && attempts == 0) return false;
        if (filter == FigureFilter.Unprinted && attempts > 0) return false;
        return AllWordsIn($"{figure.Name} {figure.RelativePath}", query);
    }

    static bool AllWordsIn(string text, string query) =>
        query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => CultureInfo.InvariantCulture.CompareInfo.IndexOf(text, word, Loose) >= 0);
}
