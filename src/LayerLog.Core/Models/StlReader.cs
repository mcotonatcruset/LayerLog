using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace LayerLog.Core.Models;

/// <summary>Triangle soup in Y-up coordinates: three vertices per triangle, three floats per vertex.</summary>
public sealed record StlMesh(float[] Positions, float[] Normals, (float X, float Y, float Z) Min, (float X, float Y, float Z) Size)
{
    public int TriangleCount => Positions.Length / 9;
}

public sealed class StlFormatException(string message) : Exception(message);

public static class StlReader
{
    const int HeaderSize = 84;
    const int TriangleSize = 50;

    public static StlMesh Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var count = bytes.Length >= HeaderSize ? BitConverter.ToUInt32(bytes, 80) : 0;
        // "solid" also opens many binary files, so the declared size decides first.
        if (bytes.Length >= HeaderSize && bytes.Length == HeaderSize + count * TriangleSize) return ReadBinary(bytes, (int)count);
        if (Encoding.ASCII.GetString(bytes, 0, Math.Min(5, bytes.Length)) == "solid") return ReadAscii(Encoding.ASCII.GetString(bytes));
        throw new StlFormatException("El fitxer no és un STL vàlid o està incomplet.");
    }

    static StlMesh ReadBinary(byte[] bytes, int count)
    {
        var positions = new float[count * 9];
        var normals = new float[count * 9];
        var span = bytes.AsSpan(HeaderSize);
        for (var i = 0; i < count; i++)
        {
            var t = MemoryMarshal.Cast<byte, float>(span.Slice(i * TriangleSize, 48));
            Store(positions, normals, i, t[..3], t[3..12]);
        }
        return Build(positions, normals);
    }

    static StlMesh ReadAscii(string text)
    {
        var vertices = new List<float>();
        var facetNormals = new List<float>();
        foreach (var raw in text.Split('\n'))
        {
            var parts = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts is ["vertex", var x, var y, var z]) { vertices.Add(Number(x)); vertices.Add(Number(y)); vertices.Add(Number(z)); }
            else if (parts is ["facet", "normal", var a, var b, var c]) { facetNormals.Add(Number(a)); facetNormals.Add(Number(b)); facetNormals.Add(Number(c)); }
        }
        var count = vertices.Count / 9;
        if (count == 0 || vertices.Count % 9 != 0 || facetNormals.Count != count * 3) throw new StlFormatException("El fitxer no és un STL vàlid o està incomplet.");

        var positions = new float[count * 9];
        var normals = new float[count * 9];
        var v = CollectionsMarshal.AsSpan(vertices);
        var n = CollectionsMarshal.AsSpan(facetNormals);
        for (var i = 0; i < count; i++) Store(positions, normals, i, n.Slice(i * 3, 3), v.Slice(i * 9, 9));
        return Build(positions, normals);
    }

    static float Number(string s) =>
        float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : throw new StlFormatException("El fitxer no és un STL vàlid o està incomplet.");

    // STL is Z-up; the scene is Y-up: (x, y, z) -> (x, z, -y).
    static void Store(float[] positions, float[] normals, int triangle, ReadOnlySpan<float> normal, ReadOnlySpan<float> vertices)
    {
        var (nx, ny, nz) = (normal[0], normal[1], normal[2]);
        if (nx == 0 && ny == 0 && nz == 0) (nx, ny, nz) = FaceNormal(vertices);
        for (var v = 0; v < 3; v++)
        {
            var o = triangle * 9 + v * 3;
            positions[o] = vertices[v * 3];
            positions[o + 1] = vertices[v * 3 + 2];
            positions[o + 2] = -vertices[v * 3 + 1];
            normals[o] = nx;
            normals[o + 1] = nz;
            normals[o + 2] = -ny;
        }
    }

    static (float, float, float) FaceNormal(ReadOnlySpan<float> v)
    {
        var (ax, ay, az) = (v[3] - v[0], v[4] - v[1], v[5] - v[2]);
        var (bx, by, bz) = (v[6] - v[0], v[7] - v[1], v[8] - v[2]);
        var (x, y, z) = (ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx);
        var length = MathF.Sqrt(x * x + y * y + z * z);
        return length == 0 ? (0, 0, 0) : (x / length, y / length, z / length);
    }

    static StlMesh Build(float[] positions, float[] normals)
    {
        if (positions.Length == 0) throw new StlFormatException("El fitxer no conté cap triangle.");
        float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
        for (var i = 0; i < positions.Length; i += 3)
        {
            minX = Math.Min(minX, positions[i]); maxX = Math.Max(maxX, positions[i]);
            minY = Math.Min(minY, positions[i + 1]); maxY = Math.Max(maxY, positions[i + 1]);
            minZ = Math.Min(minZ, positions[i + 2]); maxZ = Math.Max(maxZ, positions[i + 2]);
        }
        return new StlMesh(positions, normals, (minX, minY, minZ), (maxX - minX, maxY - minY, maxZ - minZ));
    }
}
