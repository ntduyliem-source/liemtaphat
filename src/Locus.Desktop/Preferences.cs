using System.IO;
using System.Text.Json;
using Locus.Core;

namespace Locus.Desktop;

public sealed record Preferences(string Open = "lc[", string Close = "]", int Mode = 0, int FontIndex = 1, int ScaleIndex = 1, bool WhiteBackground = false)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Locus", "preferences.json");
    public bool IsValid => new MarkerConfiguration(Open, Close).IsValid && Mode is >= 0 and <= 2 && FontIndex is >= 0 and <= 3 && ScaleIndex is >= 0 and <= 2;

    public static Preferences Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 8192) return new();
            var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path));
            return value is { Open: not null, Close: not null } && value.IsValid ? value : new();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new(); }
    }

    public void Save(string path)
    {
        if (!IsValid) throw new ArgumentException("Invalid settings.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(this)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
