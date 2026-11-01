namespace LayerLog.Core.Profiles;

public static class KeyChanges
{
    // Settings that matter most when comparing profiles, in the order they should be shown.
    static readonly string[] Priority =
    [
        "layer_height", "wall_loops", "outer_wall_speed", "raft_layers",
        "support_type", "nozzle_temperature", "hot_plate_temp",
    ];

    public static IReadOnlyList<ConfigChange> Pick(IEnumerable<ConfigChange> changes, int count) =>
        changes.OrderBy(c => Rank(c.Key)).Take(count).ToList();

    // Priority keys first, then the other labelled keys, then unknown keys. OrderBy is stable, so input order is kept within a group.
    static int Rank(string key)
    {
        var index = Array.IndexOf(Priority, key);
        if (index >= 0) return index;
        return ConfigLabels.IsKnown(key) ? Priority.Length : Priority.Length + 1;
    }
}
