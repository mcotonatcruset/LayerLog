namespace LayerLog.Core.Models;

/// <summary>Draws a mesh with a small software rasterizer, so thumbnails need no GPU or window.</summary>
public static class ModelThumbnail
{
    public const int Size = 320;
    /// <summary>The figures' material, shared with the 3D view.</summary>
    public static readonly (float R, float G, float B) Ivory = (0.9f, 0.87f, 0.8f);
    const float Turn = 0.42f, Tilt = 0.3f, Margin = 2;
    static readonly (float X, float Y, float Z) Light = (0.32f, 0.76f, 0.56f);

    /// <summary>BGRA pixels, top row first, transparent around the figure.</summary>
    public static byte[] Render(StlMesh mesh)
    {
        var pixels = new byte[Size * Size * 4];
        var count = mesh.Positions.Length / 3;
        if (count == 0) return pixels;

        var (x, y, z) = Project(mesh);
        var (minX, maxX, minY, maxY) = (x.Min(), x.Max(), y.Min(), y.Max());
        var scale = (Size - Margin * 2) / MathF.Max(MathF.Max(maxX - minX, maxY - minY), 1e-3f);
        var (offsetX, offsetY) = ((Size - (maxX - minX) * scale) / 2, (Size - (maxY - minY) * scale) / 2);
        for (var i = 0; i < count; i++)
        {
            x[i] = (x[i] - minX) * scale + offsetX;
            y[i] = (maxY - y[i]) * scale + offsetY;
        }

        var depth = new float[Size * Size];
        Array.Fill(depth, float.MinValue);
        for (var i = 0; i + 2 < count; i += 3) Fill(i, x, y, z, depth, pixels);
        return pixels;
    }

    // Same side of the figure as the viewer's opening angle, looking slightly down.
    static (float[] X, float[] Y, float[] Z) Project(StlMesh mesh)
    {
        var count = mesh.Positions.Length / 3;
        var (x, y, z) = (new float[count], new float[count], new float[count]);
        var (sinTurn, cosTurn, sinTilt, cosTilt) = (MathF.Sin(Turn), MathF.Cos(Turn), MathF.Sin(Tilt), MathF.Cos(Tilt));
        for (var i = 0; i < count; i++)
        {
            var (px, py, pz) = (mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2]);
            var near = px * sinTurn + pz * cosTurn;
            x[i] = px * cosTurn - pz * sinTurn;
            y[i] = py * cosTilt - near * sinTilt;
            z[i] = py * sinTilt + near * cosTilt;
        }
        return (x, y, z);
    }

    static void Fill(int i, float[] x, float[] y, float[] z, float[] depth, byte[] pixels)
    {
        var (x0, y0, z0, x1, y1, z1, x2, y2, z2) = (x[i], y[i], z[i], x[i + 1], y[i + 1], z[i + 1], x[i + 2], y[i + 2], z[i + 2]);
        var area = (x1 - x0) * (y2 - y0) - (y1 - y0) * (x2 - x0);
        if (area == 0) return;

        var (r, g, b) = Shade(i, x, y, z);
        var (left, right) = ((int)MathF.Max(0, MathF.Floor(MathF.Min(x0, MathF.Min(x1, x2)))), (int)MathF.Min(Size - 1, MathF.Ceiling(MathF.Max(x0, MathF.Max(x1, x2)))));
        var (top, bottom) = ((int)MathF.Max(0, MathF.Floor(MathF.Min(y0, MathF.Min(y1, y2)))), (int)MathF.Min(Size - 1, MathF.Ceiling(MathF.Max(y0, MathF.Max(y1, y2)))));
        for (var py = top; py <= bottom; py++)
            for (var px = left; px <= right; px++)
            {
                var (cx, cy) = (px + 0.5f, py + 0.5f);
                var a = ((x1 - cx) * (y2 - cy) - (y1 - cy) * (x2 - cx)) / area;
                var b1 = ((x2 - cx) * (y0 - cy) - (y2 - cy) * (x0 - cx)) / area;
                var c = 1 - a - b1;
                if (a < 0 || b1 < 0 || c < 0) continue;

                var at = py * Size + px;
                var d = a * z0 + b1 * z1 + c * z2;
                if (d <= depth[at]) continue;
                depth[at] = d;
                pixels[at * 4] = b;
                pixels[at * 4 + 1] = g;
                pixels[at * 4 + 2] = r;
                pixels[at * 4 + 3] = 255;
            }
    }

    // The normal comes from the triangle itself: both faces are lit, as many STL files have stray winding or empty normals.
    static (byte R, byte G, byte B) Shade(int i, float[] x, float[] y, float[] z)
    {
        var (ax, ay, az) = (x[i + 1] - x[i], y[i + 1] - y[i], z[i + 1] - z[i]);
        var (bx, by, bz) = (x[i + 2] - x[i], y[i + 2] - y[i], z[i + 2] - z[i]);
        var (nx, ny, nz) = (ay * bz - az * by, az * bx - ax * bz, ax * by - ay * bx);
        var length = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
        if (length == 0) length = 1;
        if (nz < 0) (nx, ny, nz) = (-nx, -ny, -nz);
        // y grew downwards here, because the screen flip already happened
        var lit = (nx * Light.X - ny * Light.Y + nz * Light.Z) / length;
        var shade = 0.42f + 0.58f * MathF.Max(0, lit);
        return ((byte)(255 * Ivory.R * shade), (byte)(255 * Ivory.G * shade), (byte)(255 * Ivory.B * shade));
    }
}
