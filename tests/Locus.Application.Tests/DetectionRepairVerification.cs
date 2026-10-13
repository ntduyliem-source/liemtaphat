using Locus.Application;
using Locus.Core.Detection;
using Locus.Core.Export;
using System.Text.Json;

public static class DetectionRepairVerification
{
    public static async Task<int> Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<object>(); int failures = 0;
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); results.Add(new { name, passed = true }); }
            catch (Exception e) { failures++; results.Add(new { name, passed = false, error = e.ToString() }); Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
        }
        async Task<FormulaSession> Session(string raw, DetectionDomains domains, string open = "lc[", string close = "]")
        {
            var session = new FormulaSession(new NativeAnalysisScheduler());
            session.Configure(new FormulaSettings(Open: open, Close: close, EnabledDomains: domains, MarkerProfiles: MarkerPreferences.Default));
            session.UpdateSource(raw);
            Assert(await session.AnalyzeContentAsync(), session.Error);
            Assert(session.State.Raw == raw && session.State.Content!.Raw == raw, "Analysis changed raw source.");
            Assert(string.Concat(session.State.Content!.Blocks().Select(b => raw[b.Start..b.End])) == raw, "Blocks lost source coverage.");
            return session;
        }
        foreach (var domains in new[] { DetectionDomains.Math, DetectionDomains.All })
        {
            await Check($"R01/{domains}/unsupported-call-and-neighbor", async () =>
            {
                foreach (var call in new[] { "sin(x+1)", "sin (x+1)", "log (x+1)", "sin\u00a0((x + 1)/2)" })
                {
                    using var plain = await Session(call, domains);
                    Assert(plain.State.Content!.Regions.Count == 0 && plain.State.Content.ResultText(0, call.Length) == call, "Unsupported call changed meaning.");
                    var raw = "😀 " + call + "; y=2";
                    using var neighbor = await Session(raw, domains);
                    var content = neighbor.State.Content!;
                    Assert(content.Regions.Count == 1 && content.Regions[0].Raw == "y=2", "An unsupported function argument became a formula.");
                    Assert(content.ResultText(0, raw.Length) == raw, "Neighbor conversion changed protected function text.");
                }
                using var prose = await Session("Tính (x+1).", domains);
                Assert(prose.State.Content!.Regions.Single().Raw == "(x+1)", "Prose before a group was blocked.");
                using var marked = await Session("lc[sin (x+1)]", domains);
                Assert(marked.State.Content!.Regions.All(r => r.Display == null), "Wrapper bypassed unsupported grammar.");
            });
            await Check($"R04/{domains}/parentheses-markers-and-custom-wrapper", async () =>
            {
                foreach (var raw in new[] { "(x-1/2)", "lc[(x-1/2)]", "toan-[(x-1/2)]", "😀 Tính (x-1/2)." })
                {
                    using var session = await Session(raw, domains);
                    var region = session.State.Content!.Regions.Single();
                    var candidate = region.Display ?? throw new InvalidOperationException("Missing displayed formula.");
                    Assert(candidate.Document.Root.Operator == "subtract" && candidate.Document.Root.Children[1].Operator == "divide", "Wrong fraction AST.");
                    Assert(CandidateExporter.ToLatex(candidate) == "x-\\frac{1}{2}", "Wrong fraction output.");
                    Assert(region.Raw == raw[region.Start..region.End], "Source span drifted.");
                }
                using var custom = await Session("Trước $(x-1/2)$ sau.", domains, "$(", ")$");
                Assert(custom.State.Content!.Regions.Single().Raw == "$(x-1/2)$", "Custom wrapper was not preserved.");
            });
            await Check($"R05/{domains}/quoted-utf16-prose", async () =>
            {
                foreach (var (open, close) in new[] { ("\"", "\""), ("'", "'"), ("“", "”"), ("‘", "’") })
                {
                    const string formula = "x mu\u0303 2 cộng 1";
                    var prefix = "😀 Tính " + open; var suffix = close + ".";
                    using var session = await Session(prefix + formula + suffix, domains);
                    var content = session.State.Content!; var region = content.Regions.Single();
                    Assert(region.Raw == formula && region.Start == prefix.Length && region.End == prefix.Length + formula.Length, "Quotes or UTF-16 offsets changed.");
                    Assert(content.ResultText(0, content.Raw.Length) == prefix + "{x}^{2}+1" + suffix, "Text outside the formula changed.");
                }
            });
            await Check($"R06/{domains}/unsupported-prefix-and-unary-control", async () =>
            {
                foreach (var invalid in new[] { "A1 trừ x^2", "abc cộng x^2", "abc co\u0323\u0302ng x^2" })
                {
                    using var session = await Session(invalid + "; y=2", domains);
                    Assert(session.State.Content!.Regions.Single().Raw == "y=2", "Opaque prefix lost from expression.");
                    Assert(session.State.Content.ResultText(0, session.State.Raw.Length) == invalid + "; y=2", "Invalid expression changed.");
                }
                using var unary = await Session("trừ x^2", domains);
                Assert(unary.State.Content!.Regions.Single().Display?.Document.Root.Type == "Unary", "Valid unary expression blocked.");
            });
            await Check($"R09/{domains}/unicode-date-context-and-opaque-text", async () =>
            {
                foreach (var formula in new[] { "x · 28/09", "x ≤ 28/09", "28/09 ≥ x" })
                {
                    using var session = await Session("Xét " + formula + ".", domains);
                    Assert(session.State.Content!.Regions.Single().Raw == formula, "Operator allowed date guard to hide an operand.");
                }
                const string raw = "Ngày 28/09; https://e.test/x^2; hello@example.org; C:\\toan\\x^2; /home/x^2; data-set/1; y=2.";
                using var guards = await Session(raw, domains);
                Assert(guards.State.Content!.Regions.Single().Raw == "y=2", "Protected prose became a formula.");
                Assert(guards.State.Content.ResultText(0, raw.Length) == raw, "Protected prose changed during export.");
            });
        }
        await Check("R07/quantity-arithmetic-without-spaces", async () =>
        {
            foreach (var domains in new[] { DetectionDomains.Physics, DetectionDomains.All })
            {
                using var session = await Session("Giá trị 10 kg/t.", domains);
                var region = session.State.Content!.Regions.Single();
                Assert(region.Raw == "10 kg/t" && region.Display?.Document.Root.Operator == "divide", "Quantity division lost its full source span.");
                Assert(region.Display!.Document.Root.Children[0].Type == "Quantity" && region.Display.Document.Root.Children[1].Name == "t", "Unit or variable changed meaning.");
            }
            using var guard = await Session("10 kg/file.txt; y=2.", DetectionDomains.All);
            Assert(guard.State.Content!.Regions.Single().Raw == "y=2", "Path after a number became quantity arithmetic.");
        });
        var report = new { status = failures == 0 ? "PASSED" : "FAILED", total = results.Count, failed = failures, results };
        await File.WriteAllTextAsync(Path.Combine(output, "detection-repair-tests.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { report.status, report.total, report.failed }));
        return failures == 0 ? 0 : 1;
    }
}
