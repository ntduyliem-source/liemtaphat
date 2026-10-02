using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

// Explicit host integration runner: calls the published clipboard adapter, then
// asks Word's document API to consume it. This is not pointer/IME/UI acceptance.
internal static class Program
{
    private static readonly List<object> Results = [];
    private static int failed;
    private static string output = "";
    private static dynamic? word;
    private static dynamic? anchor;
    private static DataObject? backup;
    private static bool restored;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--external-clipboard") return ExternalClipboardVerification.Run(args[1]);
        if (args.Length == 2 && args[0] == "--word-ui") return WordUiVerification.Run(args[1]);
        if (args.Length == 4 && args[0] == "--file-picker") return FilePickerVerification.Run(args[1], args[2], args[3]);
        if (args.Length != 2) { Console.Error.WriteLine("Usage: <published-desktop-directory> <new-report-directory>"); return 2; }
        var published = Path.GetFullPath(args[0]);
        var dll = Path.Combine(published, "Locus.Desktop.Shared.dll");
        output = Path.GetFullPath(args[1]);
        if (!File.Exists(dll) || Directory.Exists(output) || Process.GetProcessesByName("WINWORD").Length != 0)
        { Console.Error.WriteLine("Refusing missing build, existing report directory, or an existing Word process."); return 2; }
        Directory.CreateDirectory(output);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(published, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                // Preserve clipboard data in memory only. Refuse unsupported formats
                // instead of destroying a clipboard that cannot be restored exactly.
                backup = CaptureClipboard();
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll);
                var type = assembly.GetType("Locus.Desktop.Shared.NativeClipboard", true)!;
                var adapter = Activator.CreateInstance(type)!;
                async Task<string> Write(string method, object payload, bool current = true, CancellationToken token = default)
                {
                    var task = (Task)type.GetMethod(method)!.Invoke(adapter, [payload, new Func<bool>(() => current), token])!;
                    await task;
                    var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                    return result.GetType().GetProperty("Status")!.GetValue(result)!.ToString()!;
                }

                word = MicrosoftWordHost.Create();
                word.Visible = false;
                anchor = word.Documents.Add();
                anchor.Content.Text = "Locus host acceptance anchor. Synthetic test document.";
                await Check("native-text/whole-paragraph-and-word-paste", async () =>
                {
                    var expected = "Bài thử máy: {x}^{2}; v=10 m/s; H2 + O2 -> H2O.\nGiữ nguyên 😀 e\u0301 và test@example.com.";
                    Require(await Write("WriteTextAsync", expected) == "Completed", "Clipboard write failed.");
                    Require(Clipboard.GetText() == expected, "Windows clipboard text differs.");
                    dynamic doc = word.Documents.Add();
                    try
                    {
                        doc.Content.Paste();
                        string actual = doc.Content.Text;
                        Require(actual.Replace("\r\n", "\n").Replace("\r", "\n").TrimEnd('\n') == expected, "Word pasted text differs.");
                    }
                    finally { doc.Close(false); Marshal.FinalReleaseComObject(doc); }
                });
                await Check("native-text/selected-formula", async () =>
                {
                    Require(await Write("WriteTextAsync", "{x}^{2}") == "Completed", "Clipboard write failed.");
                    Require(Clipboard.GetText() == "{x}^{2}", "Selected formula clipboard differs.");
                });
                await Check("native-png/bytes-and-white-bitmap", async () =>
                {
                    var pixels = new byte[] { 0, 0, 0, 0, 255, 0, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255 };
                    var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 8);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var memory = new MemoryStream(); encoder.Save(memory); var expected = memory.ToArray();
                    File.WriteAllBytes(Path.Combine(output, "expected.png"), expected);
                    Require(await Write("WritePngAsync", expected) == "Completed", "PNG clipboard write failed.");
                    Require(ReadBytes("PNG").SequenceEqual(expected), "PNG bytes changed.");
                    var fallback = (BitmapSource)Clipboard.GetDataObject()!.GetData(DataFormats.Bitmap)!;
                    var corner = new byte[4]; fallback.CopyPixels(new Int32Rect(0, 0, 1, 1), corner, 4, 0);
                    Require(corner.All(v => v == 255), "Transparent pixel did not get white bitmap fallback.");
                });
                await Check("native-png/word-paste-save-reopen", () =>
                {
                    var path = Path.Combine(output, "native-paste.docx");
                    dynamic doc = word.Documents.Add();
                    try
                    {
                        doc.Content.Paste();
                        Require((int)doc.InlineShapes.Count == 1, "Word did not paste one image.");
                        doc.SaveAs2(FileName: path);
                    }
                    finally { doc.Close(false); Marshal.FinalReleaseComObject(doc); }
                    dynamic reopened = word.Documents.Open(FileName: path, ReadOnly: true, AddToRecentFiles: false);
                    try { Require((int)reopened.InlineShapes.Count == 1, "Saved Word image was lost."); }
                    finally { reopened.Close(false); Marshal.FinalReleaseComObject(reopened); }
                    using var archive = ZipFile.OpenRead(path);
                    Require(archive.Entries.Any(e => e.FullName.StartsWith("word/media/", StringComparison.Ordinal)), "DOCX has no image payload.");
                    return Task.CompletedTask;
                });
                await Check("native-svg/mime-and-unicode-fallback", async () =>
                {
                    const string expected = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"60\" height=\"30\"><text x=\"2\" y=\"22\">x²</text></svg>";
                    Require(await Write("WriteSvgAsync", expected) == "Completed", "SVG clipboard write failed.");
                    Require(Encoding.UTF8.GetString(ReadBytes("image/svg+xml")) == expected, "SVG MIME payload differs.");
                    Require(Clipboard.GetText() == expected, "SVG Unicode fallback differs.");
                });
                await Check("native-clipboard/cancel-and-stale-preserve-existing", async () =>
                {
                    const string sentinel = "Locus host acceptance sentinel";
                    Require(await Write("WriteTextAsync", sentinel) == "Completed", "Sentinel write failed.");
                    Require(await Write("WriteTextAsync", "stale", false) == "Cancelled", "Stale payload was not cancelled.");
                    Require(Clipboard.GetText() == sentinel, "Stale payload overwrote clipboard.");
                    using var cancel = new CancellationTokenSource(); cancel.Cancel();
                    Require(await Write("WriteTextAsync", "cancelled", true, cancel.Token) == "Cancelled", "Cancelled token was ignored.");
                    Require(Clipboard.GetText() == sentinel, "Cancelled payload overwrote clipboard.");
                });
            }
            catch (Exception ex) { failed++; Results.Add(new { name = "harness", passed = false, error = ex.ToString() }); }
            finally
            {
                try
                {
                    if (word != null)
                    {
                        if ((int)word.Documents.Count == (anchor == null ? 0 : 1)) word.Quit(false);
                        else throw new InvalidOperationException("Unexpected Word document; leaving Word running.");
                        if (anchor != null) { Marshal.FinalReleaseComObject(anchor); anchor = null; }
                        Marshal.FinalReleaseComObject(word); word = null;
                    }
                }
                catch (Exception ex) { failed++; Results.Add(new { name = "word-cleanup", passed = false, error = ex.Message }); }
                try { if (backup != null) { Clipboard.SetDataObject(backup, true); restored = true; } }
                catch (Exception ex) { failed++; Results.Add(new { name = "clipboard-restore", passed = false, error = ex.Message }); }
                var report = new
                {
                    recordedUtc = DateTimeOffset.UtcNow, status = failed == 0 ? "PASSED" : "FAILED", total = Results.Count, failed,
                    publishedAssembly = dll, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(dll))),
                    clipboardRestored = restored, results = Results,
                    scope = "Published NativeClipboard API, actual Windows clipboard and Word COM paste/save/reopen. Synthetic text/pixels only. No Desktop click, file picker, native keystroke, IME, tray, pointer or fx acceptance."
                };
                File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(JsonSerializer.Serialize(new { report.status, report.total, report.failed, report.clipboardRestored, output }));
                app.Shutdown(failed == 0 ? 0 : 1);
            }
        };
        return app.Run();
    }

    private static DataObject CaptureClipboard()
    {
        var snapshot = new DataObject();
        var existing = Clipboard.GetDataObject();
        if (existing == null) return snapshot;
        foreach (var format in existing.GetFormats(false))
        {
            var value = existing.GetData(format, false);
            object copy = value switch
            {
                string text => text,
                byte[] bytes => bytes.ToArray(),
                MemoryStream stream => new MemoryStream(stream.ToArray()),
                BitmapSource bitmap => bitmap.Clone(),
                string[] strings => strings.ToArray(),
                null => throw new InvalidOperationException("Clipboard contains an unreadable format; refusing to replace it."),
                _ => throw new InvalidOperationException("Clipboard contains a format that cannot safely be backed up; refusing to replace it.")
            };
            snapshot.SetData(format, copy, false);
        }
        return snapshot;
    }
    private static byte[] ReadBytes(string format)
    {
        var value = Clipboard.GetDataObject()!.GetData(format, false);
        if (value is byte[] bytes) return bytes;
        if (value is not Stream stream) throw new InvalidOperationException("Missing clipboard stream: " + format);
        using (stream) { using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task Check(string name, Func<Task> action)
    {
        try { await action(); Results.Add(new { name, passed = true }); }
        catch (Exception ex) { failed++; Results.Add(new { name, passed = false, error = ex.ToString() }); }
    }
}
