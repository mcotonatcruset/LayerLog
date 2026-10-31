using System.Runtime.InteropServices;

namespace LayerLog.Core.Import;

public static partial class VolumeInfo
{
    public static string FormatSerial(uint serial) => $"{serial >> 16:X4}-{serial & 0xFFFF:X4}";

    public static string? ReadSerial(string root) =>
        GetVolumeInformationW(root, null, 0, out var serial, out _, out _, null, 0) ? FormatSerial(serial) : null;

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetVolumeInformationW(string root, char[]? name, int nameSize, out uint serial, out uint maxComponent, out uint flags, char[]? fileSystem, int fileSystemSize);
}
