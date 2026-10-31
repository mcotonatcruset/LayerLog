using LayerLog.Core.Creality;

namespace LayerLog.Core.Tests;

public class CrealityLauncherTests
{
    [Fact]
    public void Paths_with_spaces_and_accents_stay_single_arguments()
    {
        var p = CrealityLauncher.BuildStart("c.exe", [@"C:\a b\Pòsit 1.stl", @"C:\c\d.stl"]);
        Assert.Equal(2, p.ArgumentList.Count);
        Assert.Equal(@"C:\a b\Pòsit 1.stl", p.ArgumentList[0]);
        Assert.False(p.UseShellExecute);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void Group_warning_starts_at_two_files(int files, bool expected) =>
        Assert.Equal(expected, CrealityLauncher.GroupWarningNeeded(files));

    [Fact]
    public void Saved_exe_wins_when_it_exists()
    {
        var file = Path.GetTempFileName();
        Assert.Equal(file, CrealityLauncher.FindExe(file, []));
        File.Delete(file);
    }

    [Fact]
    public void Exe_is_found_under_any_version_folder()
    {
        var root = Directory.CreateTempSubdirectory("creality").FullName;
        var exe = Path.Combine(Directory.CreateDirectory(Path.Combine(root, "Creality Print 7.0")).FullName, "CrealityPrint.exe");
        File.WriteAllText(exe, "");
        Assert.Equal(exe, CrealityLauncher.FindExe(@"C:\gone.exe", [root]));
        Directory.Delete(root, true);
    }

    [Fact]
    public void Missing_exe_returns_null() =>
        Assert.Null(CrealityLauncher.FindExe(null, [@"C:\does\not\exist"]));
}
