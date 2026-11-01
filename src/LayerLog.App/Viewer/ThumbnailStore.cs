using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LayerLog.Core.Models;

namespace LayerLog.App.Viewer;

/// <summary>Thumbnails on disk, named after the file's path, size and date so an edited model gets a new one, and after the picture size so a new size redraws them.</summary>
public static class ThumbnailStore
{
    static string Folder => Path.Combine(AppPaths.DataFolder, "thumbnails");

    // ponytail: pictures of edited or deleted models stay behind, up to about 80 KB each; prune them if the folder ever matters
    public static string PathFor(string root, ModelFile model)
    {
        var name = SHA256.HashData(Encoding.UTF8.GetBytes($"{root}|{model.RelativePath}|{model.Size}|{model.Modified.Ticks}|{ModelThumbnail.Size}"));
        return Path.Combine(Folder, Convert.ToHexString(name)[..32] + ".png");
    }

    public static byte[] Load(string root, ModelFile model, string path)
    {
        if (File.Exists(path)) return File.ReadAllBytes(path);

        var pixels = ModelThumbnail.Render(StlReader.Read(Path.Combine(root, model.RelativePath)));
        var bitmap = BitmapSource.Create(ModelThumbnail.Size, ModelThumbnail.Size, 96, 96, PixelFormats.Bgra32, null, pixels, ModelThumbnail.Size * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);

        Directory.CreateDirectory(Folder);
        File.WriteAllBytes(path, stream.ToArray());
        return stream.ToArray();
    }
}
