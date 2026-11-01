using LayerLog.Core.Data;

namespace LayerLog.Core.Models;

public sealed record FigureStats(int Attempts, int Prints, DateTime? LastAttempt, int? BestRating, string? BestProfile);

public sealed record Figure(string RelativePath, string Name, string Folder, bool IsPart, FigureStats Stats);

public static class FigureCatalog
{
    static readonly FigureStats NoStats = new(0, 0, null, null, null);

    public static IReadOnlyList<Figure> Build(IEnumerable<ModelFile> models,
        IReadOnlyDictionary<string, FigureMeta> metas, IReadOnlyDictionary<string, FigureStats> stats) =>
        models.Select(m =>
        {
            metas.TryGetValue(m.RelativePath, out var meta);
            return new Figure(m.RelativePath, meta?.Alias ?? Path.GetFileNameWithoutExtension(m.RelativePath),
                Path.GetDirectoryName(m.RelativePath) ?? "", meta?.IsPart ?? false, stats.GetValueOrDefault(m.RelativePath, NoStats));
        }).ToList();

    /// <summary>Most recently printed figures first; parts and never-printed figures are left out.</summary>
    public static IReadOnlyList<Figure> Recent(IEnumerable<Figure> figures, int count) =>
        figures.Where(f => !f.IsPart && f.Stats.Attempts > 0)
            .OrderByDescending(f => f.Stats.LastAttempt).Take(count).ToList();
}
