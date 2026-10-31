namespace LayerLog.Core.Data;

public sealed class PhotoStore(string folder)
{
    public string Save(string sourcePath, long printId)
    {
        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, $"{printId}-{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}");
        File.Copy(sourcePath, target);
        return target;
    }
}
