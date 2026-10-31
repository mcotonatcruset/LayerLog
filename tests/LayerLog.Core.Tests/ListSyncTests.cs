using LayerLog.Core.Models;

namespace LayerLog.Core.Tests;

public class ListSyncTests
{
    sealed class Item(string name)
    {
        public string Name { get; } = name;
    }

    static List<Item> Items(params string[] names) => names.Select(n => new Item(n)).ToList();

    static void Sync(List<Item> current, List<Item> wanted) =>
        ListSync.Apply(current, wanted, (a, b) => a.Name == b.Name);

    [Fact]
    public void Inserts_new_items_in_order()
    {
        var current = Items("a", "c");
        Sync(current, Items("a", "b", "c"));
        Assert.Equal(["a", "b", "c"], current.Select(i => i.Name));
    }

    [Fact]
    public void Removes_missing_items()
    {
        var current = Items("a", "b", "c");
        Sync(current, Items("a", "c"));
        Assert.Equal(["a", "c"], current.Select(i => i.Name));
    }

    [Fact]
    public void Keeps_existing_instances()
    {
        var current = Items("a", "c");
        var a = current[0];
        var c = current[1];
        Sync(current, Items("a", "b", "c"));
        Assert.Same(a, current[0]);
        Assert.Same(c, current[2]);
    }

    [Fact]
    public void Follows_the_wanted_order()
    {
        var current = Items("b", "a");
        var a = current[1];
        Sync(current, Items("a", "b"));
        Assert.Equal(["a", "b"], current.Select(i => i.Name));
        Assert.Same(a, current[0]);
    }

    [Fact]
    public void Reports_matched_pairs()
    {
        var current = Items("a");
        var wanted = Items("a", "b");
        var pairs = new List<(Item, Item)>();
        ListSync.Apply(current, wanted, (a, b) => a.Name == b.Name, (a, b) => pairs.Add((a, b)));
        Assert.Single(pairs);
        Assert.Same(wanted[0], pairs[0].Item2);
    }

    [Fact]
    public void Empty_wanted_clears_the_list()
    {
        var current = Items("a", "b");
        Sync(current, []);
        Assert.Empty(current);
    }
}
