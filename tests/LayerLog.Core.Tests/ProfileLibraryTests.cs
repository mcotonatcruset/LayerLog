using LayerLog.Core.Profiles;

namespace LayerLog.Core.Tests;

public class ProfileLibraryTests
{
    const string Name = "0.12mm Fine @Creality Ender-3 V3 SE 0.4 nozzle";

    internal static ProfileLibrary Library() => new(Path.Combine("Fixtures", "profiles"));

    [Fact]
    public void Merges_inherits_chain()
    {
        var b = Library().Resolve(ProfileKind.Process, Name, "7.3.0.6157")!;
        Assert.Equal("0.12", b.Values["layer_height"]); // child wins over parent
        Assert.Equal("150", b.Values["travel_speed"]);  // only in parent
    }

    [Fact]
    public void Exact_version_is_not_approximate()
    {
        var b = Library().Resolve(ProfileKind.Process, Name, "7.3.0.6157")!;
        Assert.Equal("7.3", b.SourceVersion);
        Assert.False(b.IsApproximate);
    }

    [Fact]
    public void Nearest_version_is_marked_approximate()
    {
        var b = Library().Resolve(ProfileKind.Process, Name, "7.1.1.4472")!;
        Assert.Equal("7.3", b.SourceVersion);
        Assert.True(b.IsApproximate);
    }

    [Fact]
    public void Skips_versions_without_the_profile()
    {
        // 7.2 exists next to 7.1 but lacks this profile; it must fall through to 7.3.
        var b = Library().Resolve(ProfileKind.Process, Name, "7.1.1.4472")!;
        Assert.NotEqual("7.2", b.SourceVersion);
    }

    [Fact]
    public void Unknown_profile_returns_null() =>
        Assert.Null(Library().Resolve(ProfileKind.Process, "No existeix", "7.3.0.6157"));

    [Fact]
    public void Missing_creator_folder_returns_null() =>
        Assert.Null(new ProfileLibrary(Path.Combine("Fixtures", "nowhere")).Resolve(ProfileKind.Process, Name, "7.3.0.6157"));

    [Fact]
    public void Inherits_cycle_does_not_hang() =>
        Assert.NotNull(Library().Resolve(ProfileKind.Process, "cycle-a", "7.3.0.6157"));
}
