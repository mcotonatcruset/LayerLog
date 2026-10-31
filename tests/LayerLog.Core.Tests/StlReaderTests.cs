using System.Text;
using LayerLog.Core.Models;

namespace LayerLog.Core.Tests;

public sealed class StlReaderTests : IDisposable
{
    readonly string file = Path.GetTempFileName();

    public void Dispose() => File.Delete(file);

    void WriteBinary(int declared, params float[] triangleData)
    {
        using var w = new BinaryWriter(File.Create(file));
        w.Write(new byte[80]);
        w.Write(declared);
        foreach (var f in triangleData) w.Write(f);
        w.Write((ushort)0);
    }

    [Fact]
    public void Reads_binary_and_converts_z_up_to_y_up()
    {
        WriteBinary(1, 0, 0, 1, /* v */ 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var m = StlReader.Read(file);
        Assert.Equal(1, m.TriangleCount);
        Assert.Equal([1f, 3f, -2f], m.Positions[..3]);
        Assert.Equal([0f, 1f, 0f], m.Normals[..3]);
        Assert.Equal((6f, 6f, 6f), m.Size);
    }

    [Fact]
    public void Reads_ascii()
    {
        File.WriteAllText(file, """
            solid x
              facet normal 0 0 1
                outer loop
                  vertex 0 0 0
                  vertex 1.5 0 0
                  vertex 0 1 0
                endloop
              endfacet
            endsolid x
            """, Encoding.ASCII);
        var m = StlReader.Read(file);
        Assert.Equal(1, m.TriangleCount);
        Assert.Equal(1.5f, m.Positions[3]);
    }

    [Fact]
    public void Binary_starting_with_solid_is_still_binary()
    {
        WriteBinary(1, 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var bytes = File.ReadAllBytes(file);
        Encoding.ASCII.GetBytes("solid").CopyTo(bytes, 0);
        File.WriteAllBytes(file, bytes);
        Assert.Equal(1, StlReader.Read(file).TriangleCount);
    }

    [Fact]
    public void Missing_normals_are_computed()
    {
        WriteBinary(1, 0, 0, 0, /* v */ 0, 0, 0, 1, 0, 0, 0, 1, 0);
        Assert.Equal([0f, 1f, 0f], StlReader.Read(file).Normals[..3]);
    }

    [Fact]
    public void Truncated_binary_is_rejected()
    {
        WriteBinary(1000, 0, 0, 1, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        Assert.Throws<StlFormatException>(() => StlReader.Read(file));
    }

    [Fact]
    public void Empty_or_garbage_file_is_rejected()
    {
        File.WriteAllText(file, "no sóc un model");
        Assert.Throws<StlFormatException>(() => StlReader.Read(file));
    }
}
