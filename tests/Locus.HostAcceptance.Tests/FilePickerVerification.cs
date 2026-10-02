using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

// Drives only the published adapter API. A human or Computer Use must operate
// every actual Windows dialog. No fake dialog results or keyboard injection.
internal static class FilePickerVerification
{
    internal static int Run(string publishedDirectory, string fixturePath, string reportDirectory)
    {
        var published = Path.GetFullPath(publishedDirectory);
        var output = Path.GetFullPath(reportDirectory);
        if (Directory.Exists(output)) throw new IOException("Refusing to overwrite an earlier file-picker report.");
        var fixture = File.ReadAllBytes(Path.GetFullPath(fixturePath));
        Directory.CreateDirectory(output);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var path = Path.Combine(published, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var dll = Path.Combine(published, "Locus.Desktop.Shared.dll");
        var type = AssemblyLoadContext.Default.LoadFromAssemblyPath(dll).GetType("Locus.Desktop.Shared.NativeFiles", true)!;
        var adapter = Activator.CreateInstance(type)!;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var label = new TextBlock { Margin = new Thickness(24), TextWrapping = TextWrapping.Wrap };
        var window = new Window { Title = "Locus — Kiểm hộp lưu G", Width = 560, Height = 260, Content = label };
        app.MainWindow = window;
        var results = new List<object>(); int failed = 0; int pending = 0;
        window.Loaded += async (_, _) =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try
            {
                foreach (var step in new[] { "cancel", "save", "stale" })
                {
                    var target = Path.Combine(output, step + ".locus");
                    label.Text = "Bài " + step + ". " + (step == "cancel" ? "Hủy hộp lưu." : "Chọn đúng tệp: " + target);
                    File.WriteAllText(Path.Combine(output, "step.json"), JsonSerializer.Serialize(new { step, target, utc = DateTimeOffset.UtcNow }));
                    bool current = true;
                    // Stale check flips while the real modal remains open. It must
                    // return Cancelled when the user subsequently presses Save.
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (_, _) => { current = false; timer.Stop(); };
                    if (step == "stale") timer.Start();
                    try
                    {
                        var task = (Task)type.GetMethod("SaveAsAsync")!.Invoke(adapter, [Path.GetFileName(target), "application/json", fixture, new Func<bool>(() => current), CancellationToken.None])!;
                        await task;
                        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
                        var status = result.GetType().GetProperty("Status")!.GetValue(result)!.ToString();
                        bool matchesExpected = step == "save"
                            ? status == "Completed" && File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(fixture)
                            : status == "Cancelled" && !File.Exists(target);
                        // Cancelled alone cannot prove the stale guard: Escape
                        // produces the same result as pressing Save after expiry.
                        // Keep that step pending until independent action evidence
                        // confirms the operator pressed Save, never auto-pass it.
                        bool? passed = step == "stale" && matchesExpected ? null : matchesExpected;
                        if (passed == false) failed++;
                        if (passed is null) pending++;
                        results.Add(new { step, passed, status, targetExists = File.Exists(target),
                            acceptance = passed is null ? "NEEDS_SAVE_ACTION_EVIDENCE" : passed.Value ? "PASSED" : "FAILED" });
                    }
                    finally { timer.Stop(); }
                }
            }
            catch (Exception e) { failed++; results.Add(new { step = "harness", passed = false, error = e.ToString() }); }
            finally
            {
                var report = new { recordedUtc = DateTimeOffset.UtcNow, status = failed > 0 ? "FAILED" : pending > 0 ? "INCOMPLETE" : "PASSED", total = results.Count, failed, pending, results,
                    publishedAssembly = dll, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(dll))),
                    scope = "Published NativeFiles adapter with actual Windows SaveFileDialog. Dialog operation requires external user/Computer Use. Does not test the Desktop editor Open or Save button wiring." };
                File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine(JsonSerializer.Serialize(new { report.status, report.total, report.failed, output }));
                app.Shutdown(failed > 0 ? 1 : pending > 0 ? 2 : 0);
            }
        };
        window.Show();
        return app.Run();
    }
}
