namespace Locus.Editor;

/// <summary>Host navigation and immutable release resources have independent roots.</summary>
public sealed class EditorAssetPaths
{
    public string Root { get; private set; } = "./";

    public void Initialize(string root) => Root = root.TrimEnd('/') + "/";

    public string Resolve(string path) => Root + path.TrimStart('.', '/');
}
