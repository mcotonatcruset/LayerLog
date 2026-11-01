using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Shaders;
using HelixToolkit.Wpf.SharpDX;
using LayerLog.Core.Models;
using SharpDX.DXGI;
using DiffuseMaterial = HelixToolkit.Wpf.SharpDX.DiffuseMaterial;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;

namespace LayerLog.App.Viewer;

public partial class ViewerView : UserControl
{
    const double TurnRadiansPerSecond = 0.09;

    readonly PerspectiveCamera camera = new() { FieldOfView = 28, UpDirection = new Vector3D(0, 1, 0), NearPlaneDistance = 1, FarPlaneDistance = 5000 };
    ViewerViewModel? model;
    Point3D target;
    double orbitRadius, orbitHeight, angle;
    TimeSpan lastFrame;
    bool turning;
    int meshRequest;

    public ViewerView()
    {
        InitializeComponent();
        View.EffectsManager = new DefaultEffectsManager();
        View.Camera = camera;
        Mesh.Material = new PhongMaterial
        {
            DiffuseColor = new Color4(ModelThumbnail.Ivory.R, ModelThumbnail.Ivory.G, ModelThumbnail.Ivory.B, 1),
            AmbientColor = new Color4(ModelThumbnail.Ivory.R, ModelThumbnail.Ivory.G, ModelThumbnail.Ivory.B, 1),
            SpecularColor = new Color4(0.12f, 0.12f, 0.12f, 1),
            SpecularShininess = 18,
        };
        Sweep.Texture = BuildSweep();
        Sweep.SamplerDescription = DefaultSamplers.PointSamplerWrap; // keeps the dither intact at any window size
        ContactShadow.Material = new DiffuseMaterial { DiffuseMap = BuildShadowTexture() };
        ContactShadow.CullMode = SharpDX.Direct3D11.CullMode.None;
        View.PreviewMouseDown += (_, _) => StopTurning();
        View.PreviewMouseWheel += (_, _) => StopTurning();
        DataContextChanged += (_, e) =>
        {
            model = e.NewValue as ViewerViewModel;
            if (model is not null) model.MeshChanged += ShowMesh;
        };
        IsVisibleChanged += (_, e) =>
        {
            CompositionTarget.Rendering -= Turn;
            if (e.NewValue is true && turning) CompositionTarget.Rendering += Turn;
        };
        Unloaded += (_, _) => CompositionTarget.Rendering -= Turn;
    }

    void OnTreeSelection(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // Only a figure changes what's open: the tree also moves its selection to folders and to nothing as rows collapse or results open.
        if (model is not null && e.NewValue is ExplorerNode { IsFile: true } node) model.SelectedNode = node;
    }

    async void ShowMesh(StlMesh? mesh)
    {
        var request = ++meshRequest;
        var old = Mesh.Geometry;
        Mesh.Geometry = null;
        (old as IDisposable)?.Dispose();
        (ContactShadow.Geometry as IDisposable)?.Dispose();
        ContactShadow.Geometry = null; // no figure, no shadow
        StopTurning();
        if (mesh is null) return;

        var geometry = await Task.Run(() => Build(mesh));
        if (request != meshRequest) return;
        Mesh.Geometry = geometry;
        Frame(mesh);
    }

    static MeshGeometry3D Build(StlMesh mesh)
    {
        var count = mesh.Positions.Length / 3;
        var positions = new Vector3Collection(count);
        var normals = new Vector3Collection(count);
        var indices = new IntCollection(count);
        var (cx, cz) = (mesh.Min.X + mesh.Size.X / 2, mesh.Min.Z + mesh.Size.Z / 2);
        for (var i = 0; i < count; i++)
        {
            positions.Add(new Vector3(mesh.Positions[i * 3] - cx, mesh.Positions[i * 3 + 1] - mesh.Min.Y, mesh.Positions[i * 3 + 2] - cz));
            normals.Add(new Vector3(mesh.Normals[i * 3], mesh.Normals[i * 3 + 1], mesh.Normals[i * 3 + 2]));
            indices.Add(i);
        }
        return new MeshGeometry3D { Positions = positions, Normals = normals, Indices = indices };
    }

    // The studio sweep, drawn once as a screen-sized picture behind the scene. The camera orbits, so a wall in the scene
    // would swing away, and a gradient this slow bands in 8 bits without the dither.
    // ponytail: the sweep ignores a tilted or panned view; move it with the camera if that ever looks wrong
    static TextureModel BuildSweep()
    {
        const int Width = 1920, Height = 1080;
        var pixels = new byte[Width * Height * 4];
        var dither = new Random(5);
        var at = 0;
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                var (u, v) = (x / (float)Width, y / (float)Height);
                var floor = Glow((u - 0.5f) / 0.62f, (v - 0.68f) / 0.42f);
                var wall = 0.6f * Glow((u - 0.5f) / 0.8f, (v - 0.4f) / 0.8f);
                var light = 1 - (1 - floor) * (1 - wall);
                var noise = dither.NextSingle() - dither.NextSingle(); // triangular, one level either way
                pixels[at++] = (byte)(0x47 + (0x7D - 0x47) * light + noise + 0.5f);
                pixels[at++] = (byte)(0x3D + (0x71 - 0x3D) * light + noise + 0.5f);
                pixels[at++] = (byte)(0x23 + (0x4C - 0x23) * light + noise + 0.5f);
                pixels[at++] = 255;
            }
        return new TextureModel(pixels, Format.B8G8R8A8_UNorm, Width, Height);
    }

    static float Glow(float x, float y)
    {
        var edge = 1 - MathF.Min(1, x * x + y * y);
        return edge * edge * (3 - 2 * edge);
    }

    // A black blot, densest under the figure and gone at the rim. It sits on the floor of the scene so it turns with the figure.
    static TextureModel BuildShadowTexture()
    {
        const int Size = 128;
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var (u, v) = (2f * x / (Size - 1) - 1, 2f * y / (Size - 1) - 1);
                var fade = MathF.Max(0, 1 - u * u - v * v);
                pixels[(y * Size + x) * 4 + 3] = (byte)(255 * 0.6f * fade * fade);
            }
        return new TextureModel(pixels, Format.B8G8R8A8_UNorm, Size, Size);
    }

    static MeshGeometry3D BuildContactShadow(float width, float depth)
    {
        var floor = -MathF.Max(width, depth) / 500;
        return new MeshGeometry3D
        {
            Positions = [new(-width, floor, -depth), new(width, floor, -depth), new(width, floor, depth), new(-width, floor, depth)],
            TextureCoordinates = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)],
            Normals = [Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY],
            Indices = [0, 1, 2, 0, 2, 3],
        };
    }

    void Frame(StlMesh mesh)
    {
        double span = Math.Max(mesh.Size.Y, Math.Max(mesh.Size.X, mesh.Size.Z));
        ContactShadow.Geometry = BuildContactShadow(mesh.Size.X * 0.8f, mesh.Size.Z * 0.8f);
        target = new Point3D(0, mesh.Size.Y * 0.47, 0);
        orbitRadius = span * 3.2; // leaves a figure as tall as it is wide at over 60 % of the view
        orbitHeight = span * 0.76;
        angle = 0.42;
        camera.UpDirection = new Vector3D(0, 1, 0); // dragging the view tilts it, and a new figure starts level
        camera.FarPlaneDistance = span * 40;
        camera.NearPlaneDistance = span / 50;
        PlaceCamera();
        lastFrame = default;
        turning = SystemParameters.ClientAreaAnimation;
        CompositionTarget.Rendering -= Turn;
        if (turning) CompositionTarget.Rendering += Turn;
    }

    void Turn(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == lastFrame) return;
        if (lastFrame != default) angle += (now - lastFrame).TotalSeconds * TurnRadiansPerSecond;
        lastFrame = now;
        PlaceCamera();
    }

    void PlaceCamera()
    {
        camera.Position = new Point3D(target.X + orbitRadius * Math.Sin(angle), target.Y + orbitHeight - orbitRadius * 0.12, target.Z + orbitRadius * Math.Cos(angle));
        camera.LookDirection = target - camera.Position;
    }

    void StopTurning()
    {
        turning = false;
        CompositionTarget.Rendering -= Turn;
    }
}
