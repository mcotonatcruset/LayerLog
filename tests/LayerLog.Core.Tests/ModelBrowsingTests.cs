using LayerLog.Core.Models;
using Xunit;

namespace LayerLog.Core.Tests;

public class ModelBrowsingTests
{
    [Theory]
    [InlineData("WH40K\\Tyranids\\Hormagaunt.stl", 0, "tyranids horma", FigureFilter.All, true)]
    [InlineData("WH40K\\Tyranids\\Hormagaunt.stl", 0, "necron", FigureFilter.All, false)]
    [InlineData("Otros\\Dragón.stl", 0, "DRAGON", FigureFilter.All, true)]
    [InlineData("a.stl", 2, "", FigureFilter.Printed, true)]
    [InlineData("a.stl", 0, "", FigureFilter.Printed, false)]
    [InlineData("a.stl", 2, "", FigureFilter.Unprinted, false)]
    [InlineData("a.stl", 0, "", FigureFilter.Unprinted, true)]
    public void Search_combines_words_and_filter(string path, int gcodes, string query, FigureFilter filter, bool expected) =>
        Assert.Equal(expected, ModelSearch.Matches(path, gcodes, query, filter));

    [Fact]
    public void Thumbnail_of_nothing_is_transparent()
    {
        var pixels = ModelThumbnail.Render(new StlMesh([], [], (0, 0, 0), (0, 0, 0)));
        Assert.Equal(ModelThumbnail.Size * ModelThumbnail.Size * 4, pixels.Length);
        Assert.All(pixels, p => Assert.Equal(0, p));
    }

    [Fact]
    public void Thumbnail_keeps_the_figure_upright_and_fills_the_frame()
    {
        // A triangle pointing up along Y: narrow at the top of the picture, wide at the bottom
        float[] positions = [0, 0, 0, 10, 0, 0, 5, 10, 0];
        var pixels = ModelThumbnail.Render(new StlMesh(positions, new float[9], (0, 0, 0), (10, 10, 0)));

        int Width(int row) => Enumerable.Range(0, ModelThumbnail.Size).Count(x => pixels[(row * ModelThumbnail.Size + x) * 4 + 3] == 255);
        var rows = Enumerable.Range(0, ModelThumbnail.Size).Where(r => Width(r) > 0).ToList();

        // Measured inside the figure, because the turned view leaves the base at a slant
        Assert.True(Width(rows[rows.Count * 3 / 4]) > Width(rows[rows.Count / 4]) * 2);
        Assert.True(rows.Count > ModelThumbnail.Size * 0.9);
    }
}
