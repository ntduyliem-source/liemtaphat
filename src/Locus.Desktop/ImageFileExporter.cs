using System.IO;
using System.Text;
using Locus.Desktop.Rendering;

namespace Locus.Desktop;

public static class ImageFileExporter
{
    /// <summary>Complete rendering before writing; a failed write leaves an existing destination intact.</summary>
    public static void Save(string path, FormulaScene scene, string extension)
    {
        byte[] bytes = extension switch
        {
            "svg" => new UTF8Encoding(false).GetBytes(scene.ToSvg()),
            "png" => scene.ToPng(),
            _ => throw new ArgumentException("Định dạng ảnh chưa được hỗ trợ.", nameof(extension))
        };
        string destination = Path.GetFullPath(path);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".locus-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(flushToDisk: true); }
            File.Move(temporary, destination, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
