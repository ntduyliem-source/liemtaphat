using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Locus.Core.Export;
using Locus.Desktop;

// Explicit integration mode: uses the real window copy handler and system clipboard.
// Never runs as part of the ordinary test suite. External consumers are separate processes.
internal static class ClipboardFixture
{
    internal static int Run(string[] args)
    {
        string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
        var format = Enum.Parse<CopyFormat>(args[1]); int choice = args.Length > 2 ? int.Parse(args[2]) : 0;
        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        int exitCode = 1;
        Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
        {
            var window = new MainWindow(false);
            try
            {
                ((TextBox)window.FindName("Editor")).Text = format == CopyFormat.Source ? "x mu\u0303 2" : "x+1/2";
                await window.AnalyzeNowAsync();
                ((Button)((StackPanel)window.FindName("CandidatePanel")).Children[choice]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var scene = window.Scene!; var candidate = window.Session.SelectedCandidate!;
                ImageFileExporter.Save(Path.Combine(directory, "expected.svg"), scene, "svg");
                ImageFileExporter.Save(Path.Combine(directory, "expected.png"), scene, "png");
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(scene.Bitmap(whiteBackground: true)));
                using (var stream = File.Create(Path.Combine(directory, "expected-white.png"))) encoder.Save(stream);
                await window.CopyAsync(format);
                string status = ((TextBlock)window.FindName("ExportStatus")).Text;
                if (!status.StartsWith("Đã ", StringComparison.Ordinal)) throw new InvalidOperationException(status);
                string? expectedText = format switch
                {
                    CopyFormat.Svg => scene.ToSvg(), CopyFormat.Latex => CandidateExporter.ToLatex(candidate),
                    CopyFormat.Source => CandidateExporter.ToOriginalText(candidate), CopyFormat.MathMl => CandidateExporter.ToMathMl(candidate),
                    CopyFormat.Omml => CandidateExporter.ToOmml(candidate), _ => null
                };
                File.WriteAllText(Path.Combine(directory, "fixture.json"), JsonSerializer.Serialize(new
                {
                    capturedAtUtc = DateTimeOffset.UtcNow, format = format.ToString(), candidate.Id, candidate.Kind,
                    raw = window.Session.Raw, latex = CandidateExporter.ToLatex(candidate), expectedOmml = CandidateExporter.ToOmml(candidate), expectedText,
                    clipboardFormats = Clipboard.GetDataObject()?.GetFormats(false), status
                }, new JsonSerializerOptions { WriteIndented = true }));
                exitCode = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { window.Close(); Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        });
        Dispatcher.Run(); return exitCode;
    }
}
