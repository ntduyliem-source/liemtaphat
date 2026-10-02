using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Desktop;
using Locus.Desktop.Rendering;
using InputMode = Locus.Core.Detection.InputMode;
using RenderOptions = Locus.Desktop.Rendering.RenderOptions;

internal static class Program
{
    private static readonly List<object> results = new();
    private static int passed, failed;
    private static string output = "";
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--clipboard-fixture") return ClipboardFixture.Run(args.Skip(1).ToArray());
        output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/m2"); Directory.CreateDirectory(output);
        var app = new App(); app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
        {
            try { await Run(); }
            catch (Exception error) { failed++; results.Add(new { id = "harness", status = "FAIL", error = error.ToString() }); }
            finally { Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background); }
        });
        Dispatcher.Run();
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "locus-m2-verification/1", capturedAtUtc = DateTimeOffset.UtcNow,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = Environment.OSVersion.ToString(), summary = new { passed, failed }, results,
            limits = new[] { "WPF composition events are injected in-process; this is not a real Telex/VNI IME test.", "DataObject checks do not prove paste compatibility in another app.", "No Word connector or document write guard tested." }
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Desktop checks: {passed} PASS / {failed} FAIL");
        return failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Test(string id, Action test)
    {
        try { test(); passed++; results.Add(new { id, status = "PASS" }); }
        catch (Exception error) { failed++; results.Add(new { id, status = "FAIL", error = error.Message }); Console.WriteLine("FAIL " + id + ": " + error); }
    }
    private static async Task TestAsync(string id, Func<Task> test)
    {
        try { await test(); passed++; results.Add(new { id, status = "PASS" }); }
        catch (Exception error) { failed++; results.Add(new { id, status = "FAIL", error = error.Message }); Console.WriteLine("FAIL " + id + ": " + error); }
    }
    private static Candidate Candidate(string raw, int index = 0) => new AnalysisEngine().Analyze(new SourceSnapshot(raw)).Regions.Single().Candidates[index];
    private static void Throws(Action action)
    {
        try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { return; }
        throw new Exception("Expected rejection.");
    }

    private static async Task Run()
    {
        string[] samples = { "can2", "x mũ 2", "1 trên 2", "q+7/11", "(x+1)/2", "a-(b-c)", "(a^b)^c", "a^(b^c)", "x^-2", "-x^2", "(-x)^2", "1/(1+1/x)", "sqrt(1+sqrt(2+3/4))", "2/3x", "y = x^2 + 1", "x <= 0", "1.25*x", "a+(b+c)", "x*(y/z)", "(x^2+1)^(1/2)", "can(2+3", "căn x cộng 1", "a/(b/(c/d))" };
        var gallery = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Locus M2 export verification</title><style>body{font:16px system-ui;background:#f5f5ef;color:#20382f;padding:24px}article{background:white;margin:16px 0;padding:20px;border:1px solid #ddd;border-radius:10px}.pair{display:flex;gap:32px;align-items:center;flex-wrap:wrap}.pair img{max-width:45%;max-height:230px;background:repeating-conic-gradient(#eee 0% 25%,white 0% 50%) 50%/16px 16px}h1{font-size:26px}</style><h1>M2 · SVG và PNG từ cùng candidate</h1>");
        for (int i = 0; i < samples.Length; i++)
        {
            var raw = samples[i]; int ordinal = i;
            Test("render/" + raw, () =>
            {
                var analysis = new AnalysisEngine().Analyze(new SourceSnapshot(raw));
                Check(analysis.Regions.Count == 1, "Sample must parse.");
                foreach (var candidate in analysis.Regions[0].Candidates)
                {
                    var scene = FormulaRenderer.Render(candidate);
                    Check(scene.CandidateId == candidate.Id, "Scene candidate changed.");
                    Check(scene.Parts.Count > 0 && scene.Drawing.IsFrozen, "Scene must be immutable vector geometry.");
                    var svg = XDocument.Parse(scene.ToSvg()); XNamespace ns = "http://www.w3.org/2000/svg";
                    Check(svg.Root!.Attribute("data-candidate-id")!.Value == candidate.Id, "SVG identity mismatch.");
                    Check(!svg.Descendants(ns + "text").Any() && !svg.Descendants(ns + "image").Any(), "SVG must contain standalone outlines, not font or raster references.");
                    Check(svg.Descendants(ns + "path").Count() == scene.Parts.Count, "Every primitive must be exported.");
                    Check(svg.Descendants().All(n => n.Attributes().All(a => !a.Name.LocalName.Contains("href"))), "External reference found.");
                    Check(scene.Parts.All(p => p.Geometry.IsFrozen), "All exported geometry must be immutable.");
                    foreach (var part in scene.Parts)
                    {
                        var bounds = part.Geometry.Bounds;
                        Check(bounds.IsEmpty || bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= scene.Width && bounds.Bottom <= scene.Height,
                            $"Ink clipped: {part.Role} {bounds} in {scene.Width}x{scene.Height}.");
                    }
                    var image = scene.Bitmap(); Check(image.PixelWidth == Math.Ceiling(scene.Width * 2), "PNG width differs.");
                    var pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0);
                    Check(pixels.Where((_, n) => n % 4 == 3).Any(alpha => alpha > 0), "Blank raster.");
                    Check(pixels[3] == 0, "Transparent export gained a background.");
                    var png = scene.ToPng(); Check(png.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), "Invalid PNG header.");
                    string name = "formula-" + ordinal.ToString("00") + "-" + candidate.Kind;
                    File.WriteAllText(Path.Combine(output, name + ".svg"), scene.ToSvg()); File.WriteAllBytes(Path.Combine(output, name + ".png"), png);
                    gallery.Append("<article><b>").Append(System.Net.WebUtility.HtmlEncode(raw + " · " + candidate.Kind)).Append("</b><p>SVG / PNG</p><div class='pair'><img style='width:")
                        .Append(scene.Width).Append("px' src='").Append(name).Append(".svg'><img style='width:").Append(scene.Width).Append("px' src='").Append(name).Append(".png'></div></article>");
                }
            });
        }
        File.WriteAllText(Path.Combine(output, "gallery.html"), gallery.ToString());
        Test("render/scope-semantics", () =>
        {
            Check(FormulaRenderer.Render(Candidate("a-(b-c)")).Parts.Count(p => p.Role == "group") == 2, "Subtraction grouping lost.");
            Check(FormulaRenderer.Render(Candidate("(a^b)^c")).Parts.Count(p => p.Role == "group") == 2, "Power basis grouping lost.");
            Check(FormulaRenderer.Render(Candidate("1/(1+1/x)")).Parts.Count(p => p.Role == "fraction-rule") == 2, "Nested fractions lost.");
            Check(FormulaRenderer.Render(Candidate("can2")).Parts.Any(p => p.Role == "radical-rule"), "Radical missing.");
        });
        Test("render/options-and-limits", () =>
        {
            var candidate = Candidate("x^2"); var scene = FormulaRenderer.Render(candidate, new RenderOptions(32, 3, true));
            var image = scene.Bitmap(); byte[] pixel = new byte[4]; image.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
            Check(pixel.All(b => b == 255), "White background missing.");
            Check(scene.ToSvg().Contains("<rect"), "SVG background missing.");
            Check(scene.Width < FormulaRenderer.Render(candidate, new RenderOptions(64)).Width, "Font size ignored.");
            Throws(() => FormulaRenderer.Render(candidate, new RenderOptions(double.NaN)));
            Throws(() => FormulaRenderer.Render(candidate, new RenderOptions(40, 99)));
        });
        Test("clipboard/data-contract", () =>
        {
            var candidate = Candidate("x+1/2", 1); var scene = FormulaRenderer.Render(candidate);
            foreach (var format in Enum.GetValues<CopyFormat>())
            {
                var data = ClipboardService.CreateData(candidate, scene, format);
                if (format == CopyFormat.Png) { Check(data.GetDataPresent("PNG", false), "PNG bytes missing."); Check(data.ContainsImage(), "Bitmap fallback missing."); }
                else if (format == CopyFormat.Svg) { Check(data.GetDataPresent("image/svg+xml", false), "SVG stream missing."); Check(data.GetText().Contains(candidate.Id), "SVG text identity wrong."); }
                else Check(data.ContainsText(), "Text format missing.");
            }
            Throws(() => ClipboardService.CreateData(Candidate("x^3"), scene, CopyFormat.Png));
            Check(ClipboardService.CreateData(candidate, scene, CopyFormat.Source).GetText() == "x+1/2", "Repair must not overwrite raw source.");
        });
        Test("preferences/roundtrip-and-corruption", () =>
        {
            string file = Path.Combine(output, "test-preferences.json");
            var expected = new Preferences("<<", ">>", 2, 3, 2, true); expected.Save(file);
            Check(Preferences.Load(file) == expected, "Preferences not retained.");
            Check(!File.ReadAllText(file).Contains("raw"), "Input must not be stored in preferences.");
            File.WriteAllText(file, "{broken"); Check(Preferences.Load(file) == new Preferences(), "Corruption must use defaults.");
            File.WriteAllText(file, "{\"Open\":null}"); Check(Preferences.Load(file) == new Preferences(), "Null marker must be handled.");
        });
        await TestAsync("session/direct-repair-source", async () =>
        {
            using var session = new EditorSession(); session.Update("x+1/2", InputMode.Explicit, MarkerConfiguration.Default);
            Check(await session.AnalyzeAsync() && session.CanExport, "Direct not available.");
            var repair = session.Result!.Regions[0].Candidates.Single(c => c.Kind == "repair");
            Check(session.Select(repair.Id) && session.CanExport && session.Raw == "x+1/2", "Choosing repair rewrote input.");
            Check(session.Result.Regions[0].ContentEligibility == "blocked", "Choosing repair granted automatic eligibility.");
            session.Update("can(2+3", InputMode.Explicit, MarkerConfiguration.Default); await session.AnalyzeAsync();
            Check(session.SelectedCandidate == null && !session.CanExport, "Repair-only result was implicitly selected.");
        });
        await TestAsync("session/stale-after-source-and-config", async () =>
        {
            var pending = new TaskCompletionSource<AnalysisResult>();
            using var session = new EditorSession((source, options, token) => pending.Task);
            session.Update("x^2", InputMode.Explicit, MarkerConfiguration.Default); var task = session.AnalyzeAsync();
            session.Update("x^3", InputMode.Markers, new MarkerConfiguration("<<", ">>"));
            pending.SetResult(new AnalysisEngine().Analyze(new SourceSnapshot("x^2", 1)));
            Check(!await task && session.Result == null && !session.CanExport, "Stale result published.");
        });
        await TestAsync("session/composition-gate", async () =>
        {
            int invocations = 0;
            using var session = new EditorSession((source, options, token) => { invocations++; return Task.FromResult(new AnalysisEngine().Analyze(source, options, token)); });
            session.Update("x mu", InputMode.Explicit, MarkerConfiguration.Default);
            session.BeginComposition(); session.Update("x mu\u0303 2", InputMode.Explicit, MarkerConfiguration.Default);
            Check(!await session.AnalyzeAsync() && invocations == 0 && !session.CanExport, "Composition was parsed or exported.");
            session.EndComposition(); Check(await session.AnalyzeAsync() && session.CanExport && session.Raw == "x mu\u0303 2", "Committed NFD lost.");
            var candidate = session.SelectedCandidate!; long revision = session.Revision;
            session.BeginComposition(); Throws(() => session.RequireCurrent(candidate.Id, revision));
        });
        await TestAsync("session/closed-before-late-response", async () =>
        {
            var pending = new TaskCompletionSource<AnalysisResult>(); var session = new EditorSession((s, o, t) => pending.Task);
            session.Update("x^2", InputMode.Explicit, MarkerConfiguration.Default); var task = session.AnalyzeAsync(); session.Dispose();
            pending.SetResult(new AnalysisEngine().Analyze(new SourceSnapshot("x^2", 1)));
            Check(!await task && !session.CanExport, "Closed editor published late result.");
        });
        await TestAsync("window/composition-wiring-and-source", async () =>
        {
            var window = new MainWindow(false);
            var editor = (TextBox)window.FindName("Editor");
            editor.Text = "x mũ 2"; await window.AnalyzeNowAsync(); Check(window.Scene != null, "Preview not generated.");
            var composition = new TextComposition(InputManager.Current, editor, "u");
            editor.RaiseEvent(new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputStartEvent });
            Check(window.Session.IsComposing && window.Scene == null, "Start composition did not invalidate UI.");
            editor.Text = "x mu\u0303 2"; await window.AnalyzeNowAsync(); Check(window.Scene == null, "UI published mid-composition.");
            // Synthetic completion must not insert another character into the already updated TextBox.
            editor.RaiseEvent(new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, composition) { RoutedEvent = TextCompositionManager.TextInputEvent, Handled = true });
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            await window.AnalyzeNowAsync(); Check(window.Scene != null && editor.Text == "x mu\u0303 2", "Composition commit changed raw.");
            editor.Text = "x^3"; Check(window.Scene == null && !window.Session.CanExport, "Old preview survived TextChanged.");
            window.Close();
        });
        await TestAsync("window/layout-render", async () =>
        {
            var window = new MainWindow(false); var editor = (TextBox)window.FindName("Editor");
            editor.Text = "(x+1)/(sqrt(2)+3)"; await window.AnalyzeNowAsync();
            var content = (FrameworkElement)window.Content;
            // Render the actual content in an explicit viewport, independent of an unshown Window's own size negotiation.
            window.Content = null;
            content.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, 14d);
            content.SetValue(System.Windows.Documents.TextElement.ForegroundProperty, window.Foreground);
            foreach (var size in new[] { new Size(1120, 760), new Size(850, 610) })
            {
                content.Width = size.Width - content.Margin.Left - content.Margin.Right;
                content.Height = size.Height - content.Margin.Top - content.Margin.Bottom;
                content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.DrawRectangle(window.Background, null, new Rect(size));
                    context.DrawRectangle(new VisualBrush(content) { AutoLayoutContent = false, Stretch = Stretch.Fill }, null,
                        new Rect(content.Margin.Left, content.Margin.Top, content.ActualWidth, content.ActualHeight));
                }
                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var file = File.Create(Path.Combine(output, $"desktop-{size.Width:0}.png")); encoder.Save(file);
                Check(content.DesiredSize.Width <= size.Width + 1, $"Horizontal window overflow: desired={content.DesiredSize}, viewport={size}, actual={content.RenderSize}.");
            }
            window.Close();
        });
        await TestAsync("window/custom-markers-and-region-selection", async () =>
        {
            var window = new MainWindow(false);
            ((ComboBox)window.FindName("ModeBox")).SelectedIndex = 2;
            ((TextBox)window.FindName("OpenMarker")).Text = "<<";
            ((TextBox)window.FindName("CloseMarker")).Text = ">>";
            var editor = (TextBox)window.FindName("Editor");
            editor.Text = "Đặt <<q mũ 7>> và <<3 trên 5>>.";
            await window.AnalyzeNowAsync();
            Check(window.Session.Result!.Regions.Count == 2 && window.Scene != null, "Custom wrapped regions missing.");
            ((ComboBox)window.FindName("RegionBox")).SelectedIndex = 1;
            Check(window.Session.SelectedCandidate!.ContentSpan.Equals(window.Session.Result.Regions[1].ContentSpan), "Wrong region selected.");
            Check(window.Scene!.CandidateId == window.Session.SelectedCandidate.Id, "Preview belongs to previous region.");
            Check(window.Session.SelectedCandidate.Source.Slice(window.Session.SelectedCandidate.ReplacementSpan) == "<<3 trên 5>>", "Source wrapper changed.");
            ((TextBox)window.FindName("CloseMarker")).Text = "]]";
            Check(window.Scene == null && !window.Session.CanExport, "Changing marker kept old copy action enabled.");
            window.Close();
        });
        await TestAsync("window/select-repair-keeps-raw-and-scene-id", async () =>
        {
            var window = new MainWindow(false); var editor = (TextBox)window.FindName("Editor");
            editor.Text = "q+7/11"; await window.AnalyzeNowAsync();
            var panel = (StackPanel)window.FindName("CandidatePanel");
            ((Button)panel.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.Session.SelectedCandidate!.Kind == "repair" && window.Scene!.CandidateId == window.Session.SelectedCandidate.Id, "Card choice not reflected in export scene.");
            Check(editor.Text == "q+7/11", "Repair selection mutated editor.");
            var before = window.Scene!;
            ((ComboBox)window.FindName("FontBox")).SelectedIndex = 3;
            Check(window.Scene!.CandidateId == before.CandidateId && window.Scene.Width > before.Width, "Render option changed selected formula.");
            window.Close();
        });
        await TestAsync("window/older-completion-cannot-end-new-composition", async () =>
        {
            var window = new MainWindow(false); var editor = (TextBox)window.FindName("Editor");
            var composition = new TextComposition(InputManager.Current, editor, "");
            void Raise(RoutedEvent routed) => editor.RaiseEvent(new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, composition) { RoutedEvent = routed, Handled = routed == TextCompositionManager.TextInputEvent });
            Raise(TextCompositionManager.PreviewTextInputStartEvent); Raise(TextCompositionManager.TextInputEvent);
            Raise(TextCompositionManager.PreviewTextInputStartEvent);
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            Check(window.Session.IsComposing && !window.Session.CanExport, "Earlier queued completion ended a new composition.");
            Raise(TextCompositionManager.TextInputEvent); await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            Check(!window.Session.IsComposing, "Current completion not applied."); window.Close();
        });
        await TestAsync("window/repair-only-region-clears-previous-selection", async () =>
        {
            var window = new MainWindow(false);
            ((ComboBox)window.FindName("ModeBox")).SelectedIndex = 2;
            ((TextBox)window.FindName("Editor")).Text = "lc[x^2] và lc[can(2+3]";
            await window.AnalyzeNowAsync();
            Check(window.Session.CanExport, "First direct region unavailable.");
            ((ComboBox)window.FindName("RegionBox")).SelectedIndex = 1;
            Check(window.Session.SelectedCandidate == null && !window.Session.CanExport && window.Scene == null, "Previous region remained exportable.");
            ((Button)((StackPanel)window.FindName("CandidatePanel")).Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(window.Session.CanExport && window.Session.SelectedCandidate!.Kind == "repair", "Explicit repair choice failed.");
            window.Close();
        });
        Test("file-export/complete-write-and-failure-preserves-destination", () =>
        {
            string directory = Path.Combine(output, "file-export"); Directory.CreateDirectory(directory);
            var scene = FormulaRenderer.Render(Candidate("(x+1)/2"));
            string svg = Path.Combine(directory, "cong-thuc.svg"), png = Path.Combine(directory, "cong-thuc.png");
            ImageFileExporter.Save(svg, scene, "svg"); ImageFileExporter.Save(png, scene, "png");
            Check(File.ReadAllText(svg) == scene.ToSvg() && File.ReadAllBytes(png).SequenceEqual(scene.ToPng()), "Saved bytes differ from preview scene.");
            byte[] before = File.ReadAllBytes(png);
            using (var locked = new FileStream(png, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bool failedSafely = false;
                try { ImageFileExporter.Save(png, FormulaRenderer.Render(Candidate("x^3")), "png"); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failedSafely = true; }
                Check(failedSafely && File.ReadAllBytes(png).SequenceEqual(before), "Failed replacement modified existing file.");
            }
            Check(!Directory.EnumerateFiles(directory, ".locus-*.tmp").Any(), "Temporary file was left behind.");
        });
        await TestAsync("window/middle-edit-undo-redo-and-unclosed-marker", async () =>
        {
            var window = new MainWindow(false); var editor = (TextBox)window.FindName("Editor");
            window.ShowActivated = false; window.Show(); editor.Focus(); await Dispatcher.Yield(DispatcherPriority.Loaded);
            editor.IsUndoEnabled = false; editor.Text = "x mũ 2 + 1"; editor.IsUndoEnabled = true; await window.AnalyzeNowAsync();
            editor.Select(5, 1); editor.BeginChange(); editor.SelectedText = "3"; editor.EndChange();
            Check(editor.Text == "x mũ 3 + 1" && window.Scene == null, "Middle edit retained stale preview.");
            editor.Undo(); Check(editor.Text == "x mũ 2 + 1", "Undo did not restore exact raw: " + editor.Text);
            editor.Redo(); Check(editor.Text == "x mũ 3 + 1", "Redo lost middle edit.");
            await window.AnalyzeNowAsync(); Check(window.Scene != null, "Reanalysis after redo failed.");
            ((ComboBox)window.FindName("ModeBox")).SelectedIndex = 2; editor.Text = "lc[x mũ 3";
            await window.AnalyzeNowAsync(); Check(!window.Session.CanExport && editor.Text == "lc[x mũ 3", "Unclosed marker exported or changed raw.");
            window.Close();
        });
        await TestAsync("window/explicit-analysis-cancels-pending-debounce", async () =>
        {
            var window = new MainWindow(false);
            ((TextBox)window.FindName("Editor")).Text = "x+1/2";
            await window.AnalyzeNowAsync();
            ((Button)((StackPanel)window.FindName("CandidatePanel")).Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var scene = window.Scene;
            await Task.Delay(300);
            Check(ReferenceEquals(scene, window.Scene) && window.Session.SelectedCandidate!.Kind == "repair", "Pending debounce replaced an explicitly selected result.");
            window.Close();
        });
    }
}
