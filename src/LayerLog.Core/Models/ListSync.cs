namespace LayerLog.Core.Models;

public static class ListSync
{
    /// <summary>Makes current match wanted in place, keeping the instances it already has.</summary>
    public static void Apply<T>(IList<T> current, IReadOnlyList<T> wanted, Func<T, T, bool> same, Action<T, T>? matched = null)
    {
        for (var i = 0; i < wanted.Count; i++)
        {
            var found = -1;
            for (var j = i; j < current.Count && found < 0; j++)
                if (same(current[j], wanted[i])) found = j;

            if (found < 0)
            {
                current.Insert(i, wanted[i]);
                continue;
            }
            var item = current[found];
            if (found != i)
            {
                current.RemoveAt(found);
                current.Insert(i, item);
            }
            matched?.Invoke(item, wanted[i]);
        }
        while (current.Count > wanted.Count) current.RemoveAt(current.Count - 1);
    }
}
