using System.IO;
using System.Text.Json;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Desktop.Rendering;

namespace Locus.Desktop;

/// <summary>Explicit command-line package verification with authored inputs, no UI/clipboard/user settings.</summary>
internal static class PackageSmoke
{
    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        foreach (var raw in new[] { "can2", "x mũ 2", "1 trên 2", "q+7/11", "lc[x^2]", "😀 Đặt <<q mũ 7>>." })
        {
            var markers = raw.Contains("<<", StringComparison.Ordinal) ? new MarkerConfiguration("<<", ">>") : MarkerConfiguration.Default;
            var mode = raw.Contains("lc[", StringComparison.Ordinal) || raw.Contains("<<", StringComparison.Ordinal) ? InputMode.Markers : InputMode.Explicit;
            var result = new AnalysisEngine().Analyze(new SourceSnapshot(raw), new AnalysisOptions(mode, markers: markers));
            if (result.Regions.Count != 1) throw new InvalidOperationException("Package analysis failed.");
            var candidate = result.Regions[0].Candidates[0]; var scene = FormulaRenderer.Render(candidate);
            if (!scene.ToSvg().Contains(candidate.Id, StringComparison.Ordinal) || scene.ToPng().Length < 50) throw new InvalidOperationException("Package export failed.");
            results.Add(new { raw, candidate.Id, svg = true, png = true });
        }
        File.WriteAllText(Path.Combine(directory, "package-smoke.json"), JsonSerializer.Serialize(new
        {
            status = "PASS", capturedAtUtc = DateTimeOffset.UtcNow, baseDirectory = AppContext.BaseDirectory,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            runtimeDirectory = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
            windowsDesktopAssembly = typeof(System.Windows.Window).Assembly.Location,
            coreAssembly = typeof(SourceSnapshot).Assembly.Location, samples = results,
            scope = "Packaged process: same core and vector exports; no network API, Word, UI input or clipboard exercised."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
}
