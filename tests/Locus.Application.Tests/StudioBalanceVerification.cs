using System.Text.Json;
using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;

public static class StudioBalanceVerification
{
    public static async Task<int> Run(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<object>(); int failed = 0;
        void Assert(bool value, string reason = "Studio balance contract failed") { if (!value) throw new Exception(reason); }
        async Task Check(string name, Func<Task> action)
        {
            try { await action(); checks.Add(new { name, passed = true }); }
            catch (Exception e) { failed++; checks.Add(new { name, passed = false, error = e.ToString() }); Console.WriteLine($"FAIL {name}: {e.Message}"); }
        }
        var settings = new FormulaSettings(EnabledDomains: DetectionDomains.All, MarkerProfiles: MarkerPreferences.Default);
        async Task<FormulaSession> Ready(string raw, IAnalysisScheduler? scheduler = null)
        {
            var s = new FormulaSession(scheduler ?? new NativeAnalysisScheduler());
            s.Configure(settings); s.UpdateSource(raw); Assert(await s.AnalyzeContentAsync(), s.Error); return s;
        }
        int Managed(FormulaSession s) => s.State.Content!.Regions.Count(r => r.ResultOverride?.ManagedBalance == true);
        string Text(FormulaSession s) => s.State.Content!.ResultText(0, s.State.Raw.Length);

        await Check("no-selection-balances-and-cancels-all", async () =>
        {
            using var s = await Ready("H2+O2=H2O\nN2+H2=NH3"); var before = s.State; string original = Text(s);
            Assert(s.CanRunStudioBalance && !s.HasStudioBalance());
            Assert(await s.StudioBalanceAsync(), s.BalanceMessage); Assert(Managed(s) == 2 && s.State.Raw == before.Raw);
            Assert(await s.StudioBalanceAsync()); Assert(Managed(s) == 0 && Text(s) == original);
        });
        await Check("bare-draft-fills-without-selection-cancel-keeps-products", async () =>
        {
            using var s = await Ready("3H2+O2="); var before = s.State;
            Assert(s.State.Content!.Regions.Count == 1 && s.State.Content.Regions[0].Raw == before.Raw);
            Assert(await s.StudioBalanceAsync(), s.BalanceMessage); Assert(Managed(s) == 1 && s.State.Raw == before.Raw);
            Assert(s.BalanceMessage.Contains("Điền theo trường hợp:"));
            Assert(await s.StudioBalanceAsync()); var r = s.State.Content!.Regions.Single();
            Assert(r.Display?.Source.Raw == "3H2+O2->H2O", r.Display?.Source.Raw ?? "No display");
            Assert(r.ResultOverride?.ProductProposal != null && !r.ResultOverride.ManagedBalance);
            Assert(await s.StudioBalanceAsync() && Managed(s) == 1);
        });
        await Check("paragraph-fill-balanced-and-unbalanced-preserve-prose", async () =>
        {
            const string raw = "Bài học 🧪\r\nH2+O2=\r\nHCl+NaOH=\r\nXét hoa-[N2+H2=NH3].\r\nGiữ lc[x^2] và ly-[v=10 m/s].";
            using var s = await Ready(raw); Assert(await s.StudioBalanceAsync(), s.BalanceMessage);
            Assert(Managed(s) == 2, "Only two reactions need changed coefficients: " + Managed(s)); Assert(s.State.Raw == raw);
            Assert(Text(s).StartsWith("Bài học 🧪\r\n") && Text(s).Contains("\r\nXét ") && Text(s).Contains("Giữ "));
            Assert(await s.StudioBalanceAsync()); Assert(Managed(s) == 0 && s.State.Content!.Regions.Count(r => r.ResultOverride?.ProductProposal != null) == 2);
        });
        await Check("single-cancel-leaves-others-balanced", async () =>
        {
            using var s = await Ready("hoa-[H2+O2=]\nhoa-[N2+H2=NH3]"); Assert(await s.StudioBalanceAsync(), s.BalanceMessage);
            var first = s.State.Content!.Regions[0]; s.SetBalanceSelection(s.State.Content.Select(first.Start, first.End));
            Assert(s.HasStudioBalance(first.Id) && await s.StudioBalanceAsync(first.Id)); Assert(Managed(s) == 1 && s.HasStudioBalance());
            Assert(s.State.Content!.Regions[0].ResultOverride?.ProductProposal != null);
            s.SetBalanceSelection(null); Assert(await s.StudioBalanceAsync() && Managed(s) == 0);
        });
        await Check("lowercase-equation-in-paragraph", async () =>
        {
            using var s = await Ready("x mũ 2 + 1\nfe+o2=feo"); string raw = s.State.Raw;
            Assert(await s.StudioBalanceAsync() && Managed(s) == 1 && s.State.Raw == raw);
        });
        await Check("unfinished-wrapper-skipped-without-blocking-other-equations", async () =>
        {
            using var s = await Ready("hoa-[N2+H2=NH3]\nhoa-[H2+O2="); string raw = s.State.Raw;
            Assert(await s.StudioBalanceAsync(), s.BalanceMessage);
            Assert(Managed(s) == 1 && s.State.Raw == raw && s.State.Content!.Regions.Last().ResultOverride == null);
        });
        await Check("fill-batch-and-cancel-are-separate-single-undo-steps", async () =>
        {
            using var s = await Ready("H2+O2=\nHCl+NaOH="); var before = s.State;
            Assert(await s.StudioBalanceAsync(), s.BalanceMessage); var balanced = s.State;
            Assert(s.Undo() && ReferenceEquals(s.State, before)); Assert(s.Redo() && ReferenceEquals(s.State, balanced));
            Assert(await s.StudioBalanceAsync()); var cancelled = s.State;
            Assert(s.Undo() && ReferenceEquals(s.State, balanced)); Assert(s.Redo() && ReferenceEquals(s.State, cancelled));
        });
        await Check("reopen-preserves-products-and-cancel-state", async () =>
        {
            using var s = await Ready("H2+O2="); Assert(await s.StudioBalanceAsync());
            string encoded = DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(), 1, s.State, new()));
            using var reopened = new FormulaSession(new NativeAnalysisScheduler()); reopened.Load(((FormulaDocument)DocumentCodec.Open(encoded).Document!).State);
            Assert(reopened.HasStudioBalance() && await reopened.StudioBalanceAsync());
            Assert(reopened.State.Content!.Regions.Single().ResultOverride?.ProductProposal != null && Managed(reopened) == 0);
            Assert(DocumentCodec.Open(DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(), 2, reopened.State, new()))).IsSupported);
            await File.WriteAllTextAsync(Path.Combine(output, "filled-and-cancelled.locus"), encoded);
        });
        await Check("ambiguous-or-unsupported-products-are-not-guessed", async () =>
        {
            foreach (string raw in new[] { "CO2+NaOH=", "N2+H2=", "h20+O2=", "NaCl+KNO3=" })
            {
                using var s = await Ready(raw); var before = s.State;
                Assert(!await s.StudioBalanceAsync() && ReferenceEquals(before, s.State), raw + " was filled");
                Assert(s.BalanceMessage.Length > 0);
            }
        });
        await Check("complete-equation-no-solution-remains-user-owned", async () =>
        {
            using var s = await Ready("hoa-[H2=CO2]"); var before = s.State;
            Assert(!await s.StudioBalanceAsync() && ReferenceEquals(before, s.State) && s.CanExport);
        });
        await Check("user-coefficients-already-balanced-no-false-history", async () =>
        {
            using var s = await Ready("4H2+2O2=4H2O"); var before = s.State; string text = Text(s);
            Assert(!await s.StudioBalanceAsync() && !s.HasStudioBalance() && Text(s) == text && ReferenceEquals(before,s.State));
            Assert(s.StudioChemistryOutcomes.Single().Status=="already-balanced");
        });
        await Check("kept-text-math-and-prose-remain-untouched", async () =>
        {
            using var s = await Ready("Giữ hoa-[H2+O2=H2O] và lc[x+1/2]. https://example.org/a=b");
            var chemical = s.State.Content!.Regions.First(r => r.Display?.Document.Domain == "chemistry");
            Assert(s.KeepContentText(chemical.Id, true)); var before = s.State;
            Assert(!await s.StudioBalanceAsync() && ReferenceEquals(s.State, before));
        });
        await Check("input-guard-rejects-entire-batch", async () =>
        {
            using var s = await Ready("H2+O2=\nN2+H2=NH3"); var before = s.State;
            Assert(!await s.StudioBalanceAsync(validateInput: () => Task.FromResult(false)) && ReferenceEquals(before, s.State));
        });
        await Check("changed-source-selection-and-stop-reject-late-worker", async () =>
        {
            foreach (string change in new[] { "source", "selection", "stop" })
            {
                var gate = new BalanceGate(); using var s = await Ready("H2+O2=H2O", gate); var before = s.State;
                var pending = s.StudioBalanceAsync(); await gate.Started.Task;
                if (change == "source") s.UpdateSource("x^2");
                else if (change == "selection") s.SetBalanceSelection(before.Content!.Select(0, before.Raw.Length));
                else s.CancelBalance();
                gate.Complete(); Assert(!await pending && !s.HasStudioBalance(), change);
                Assert(change == "source" ? s.State.Raw == "x^2" : ReferenceEquals(s.State, before));
            }
        });
        await Check("worker-error-is-atomic", async () =>
        {
            var gate = new BalanceGate(); using var s = await Ready("H2+O2=H2O", gate); var before = s.State;
            var pending = s.StudioBalanceAsync(); await gate.Started.Task; gate.Fail();
            Assert(!await pending && ReferenceEquals(before, s.State) && s.CanRunStudioBalance);
        });
        await Check("explicit-fill-wire-parity-does-not-change-ghost-default", () =>
        {
            var request = new ChemistryAssistanceRequest(new("H2+O2=", 9, settings), 0, []);
            Assert(ChemistryAssistanceWire.Analyze(request).Status == "needs-conditions");
            request = request with { UseUniqueCatalogConditions = true };
            var native = ChemistryAssistanceWire.Analyze(request);
            var worker = ChemistryAssistanceWire.Deserialize(AnalysisWire.Run(ChemistryAssistanceWire.Request(request)));
            Assert(native.Status == "available" && native.Proposals.SequenceEqual(worker.Proposals) && native.ContextId == worker.ContextId);
            return Task.CompletedTask;
        });
        var report = new { status = failed == 0 ? "PASSED" : "FAILED", total = checks.Count, failed, checks };
        await File.WriteAllTextAsync(Path.Combine(output, "studio-balance-tests.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { report.status, report.total, report.failed })); return failed == 0 ? 0 : 1;
    }

    private sealed class BalanceGate : IAnalysisScheduler, IContentBalanceScheduler
    {
        public TaskCompletionSource Started { get; } = new();
        private ContentBalanceRequest? request;
        private readonly TaskCompletionSource<ContentBalanceResponse> completion = new();
        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest input, CancellationToken token) => Task.FromResult(AnalysisWire.Analyze(input));
        public Task<ContentBalanceResponse> BalanceAsync(ContentBalanceRequest input, CancellationToken token)
        { request = input; Started.SetResult(); return completion.Task; }
        public void Complete() => completion.SetResult(ContentBalanceWire.Analyze(request!));
        public void Fail() => completion.SetException(new IOException("worker failed"));
    }
}
