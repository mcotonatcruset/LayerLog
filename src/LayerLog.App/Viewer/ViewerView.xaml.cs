using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using LayerLog.Core.Models;
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
            DiffuseColor = new Color4(0.9f, 0.87f, 0.8f, 1),
            AmbientColor = new Color4(0.9f, 0.87f, 0.8f, 1),
            SpecularColor = new Color4(0.12f, 0.12f, 0.12f, 1),
            SpecularShininess = 18,
        };
        Floor.Material = new VertColorMaterial();
        Floor.CullMode = SharpDX.Direct3D11.CullMode.None;
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

    // A soft pool of light on the floor: unlit, fading to the background colour at the rim.
    static MeshGeometry3D BuildFloor(float span)
    {
        const int Segments = 96;
        var rings = new (float Radius, Color4 Color)[]
        {
            (0, new Color4(0.135f, 0.135f, 0.14f, 1)),
            (span * 0.75f, new Color4(0.11f, 0.112f, 0.12f, 1)),
            (span * 1.9f, new Color4(0.09f, 0.094f, 0.106f, 1)),
        };
        var positions = new Vector3Collection();
        var colors = new Color4Collection();
        var indices = new IntCollection();
        foreach (var (radius, color) in rings)
            for (var s = 0; s < Segments; s++)
            {
                var a = s * MathF.Tau / Segments;
                positions.Add(new Vector3(radius * MathF.Cos(a), -span / 500, radius * MathF.Sin(a)));
                colors.Add(color);
            }
        for (var ring = 0; ring < rings.Length - 1; ring++)
            for (var s = 0; s < Segments; s++)
            {
                int a = ring * Segments + s, b = ring * Segments + (s + 1) % Segments, c = a + Segments, d = b + Segments;
                indices.Add(a); indices.Add(b); indices.Add(c);
                indices.Add(b); indices.Add(d); indices.Add(c);
            }
        return new MeshGeometry3D { Positions = positions, Colors = colors, Indices = indices };
    }

    void Frame(StlMesh mesh)
    {
        double span = Math.Max(mesh.Size.Y, Math.Max(mesh.Size.X, mesh.Size.Z));
        (Floor.Geometry as IDisposable)?.Dispose();
        Floor.Geometry = BuildFloor((float)span);
        target = new Point3D(0, mesh.Size.Y * 0.47, 0);
        orbitRadius = span * 4.4;
        orbitHeight = span * 1.05;
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
