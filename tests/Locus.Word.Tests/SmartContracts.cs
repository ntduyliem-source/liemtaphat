using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;
using Locus.Word;

internal static partial class Program
{
    private static readonly WordDetectionSettings smartSettings = WordDetectionSettings.Upgrade(MarkerConfiguration.Default, DetectionDomains.All);
    private static ManualFormulaState Smart(string raw) => ManualSourceAnalysis.Analyze(raw, smartSettings);
    private static AssistanceProposal Products(ManualFormulaState state) => ChemistryAssistance.Analyze(state.Draft!.Region,
        new AssistanceContext(smartSettings.Fingerprint, new[] { new ConditionFact("activation", "ignition") }), DetectionDomains.Chemistry).Proposals.Single();
    private static int RunSmartContracts(string[] args)
    {
        directory = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/phase-e/contracts"); Directory.CreateDirectory(directory);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        foreach (var item in new[] { new[] { "lc[x mũ 2]", "math" }, new[] { "toan-[x+1/2]", "math" }, new[] { "ly-[v=10 m/s]", "physics" }, new[] { "hoa-[H2SO4]", "chemistry" } })
            Test("smart/marker/" + item[1] + "/" + item[0], () => {
                if (item[0].StartsWith("lc[", StringComparison.Ordinal)) { Check(Smart(item[0]).Selected!.Document.Domain == item[1], "Common detection differs."); return; }
                var state = ManualSourceAnalysis.Analyze(item[0], WordDetectionSettings.Upgrade(MarkerConfiguration.Default, DetectionDomains.None));
                Check(state.Selected!.Document.Domain == item[1] && state.OriginalSource == item[0], "Marker intent/source differs.");
            });
        Test("smart/settings-migration-conflict", () => {
            var migrated = WordDetectionSettings.Deserialize("[\"hoa-[\",\"]\",\"7\"]");
            Check(migrated.Common.Open == "hoa-[" && !migrated.Profiles.Profiles.Single(p => p.Id == "chemistry").Enabled, "Legacy custom marker changed.");
            Check(WordDetectionSettings.Deserialize(migrated.Serialize()).Fingerprint == migrated.Fingerprint, "Settings roundtrip differs.");
            Reject(() => smartSettings.WithCommon("hoa-[", "]", DetectionDomains.All));
        });
        Test("smart/reject-incomplete-mixed-opaque", () => {
            foreach (var raw in new[] { "hoa-[H2+O2=", "lc[x^2] suffix", "https://example.org/x", "ly-[H2+O2=]", "hoa-[h2+o2=h20]" })
            {
                if (raw == "hoa-[h2+o2=h20]") { var state = Smart(raw); Reject(() => state.Balance(out _)); }
                else Reject(() => Smart(raw));
            }
        });
        Test("smart/balance-cancel-exact-coefficients", () => {
            var original = Smart("hoa-[3H2+O2=H2O]"); var balanced = original.Balance(out _);
            Check(balanced.CanCancelBalance && balanced.OriginalSource == original.OriginalSource, "Balance changed raw.");
            Check(ReactionBalancer.VerifyConservation(balanced.Selected!.Document.Root), "Unbalanced result.");
            var reopened = ManagedSnapshot.Decode(new ManagedSnapshot(balanced).Encode());
            var cancelled = reopened.State.CancelBalance();
            Check(cancelled.Selected!.Id == original.Selected!.Id && cancelled.KeepFromAuto && !cancelled.CanCancelBalance, "Cancel did not restore exact source reading.");
            Check(cancelled.Balance(out _).CanCancelBalance, "Explicit balance ignored manual override.");
            File.WriteAllText(Path.Combine(directory, "balanced.tag"), new ManagedSnapshot(balanced).Encode());
        });
        Test("smart/already-balanced-and-no-solution", () => {
            var original = Smart("hoa-[4H2+2O2=4H2O]"); Check(ReferenceEquals(original, original.Balance(out _)), "Already balanced coefficients changed.");
            var impossible = Smart("hoa-[H2=O2]"); Check(ReferenceEquals(impossible, impossible.Balance(out _)), "Impossible result accepted.");
        });
        Test("smart/products-separate-balance-and-source", () => {
            var draft = Smart("hoa-[3H2+O2=]"); Check(draft.Draft != null && draft.Selected == null, "Draft became a complete reading.");
            Reject(() => new ManagedSnapshot(draft));
            var accepted = draft.AcceptProducts(Products(draft));
            Check(accepted.Products != null && accepted.CanCancelBalance, "Products/balance missing.");
            var reopened = ManagedSnapshot.Decode(new ManagedSnapshot(accepted).Encode());
            var cancelled = reopened.State.CancelBalance();
            Check(cancelled.Products != null && cancelled.Result!.Source.Raw == "3H2+O2->H2O" && cancelled.KeepFromAuto, "Cancel balance removed products or original coefficients.");
            var again = ManagedSnapshot.Decode(new ManagedSnapshot(cancelled).Encode());
            Check(again.State.Identity == cancelled.Identity && again.OriginalSource == "hoa-[3H2+O2=]", "Reopen recomputed the snapshot.");
            Check(again.State.DropProducts().Draft!.Id == draft.Draft!.Id, "Drop products did not restore the draft.");
            File.WriteAllText(Path.Combine(directory, "products.tag"), new ManagedSnapshot(accepted).Encode());
            File.WriteAllText(Path.Combine(directory, "products-cancelled.tag"), new ManagedSnapshot(cancelled).Encode());
        });
        Test("smart/legacy-snapshot-exact-roundtrip", () => {
            var old = new ManagedSnapshot(Smart("x+1/2").Readings!); string tag = old.Encode();
            Check(tag.StartsWith(ManagedSnapshot.Prefix, StringComparison.Ordinal) && ManagedSnapshot.Decode(tag).Encode() == tag, "Legacy snapshot changed.");
            Check(ManagedSnapshot.HasEntry(new ManagedSnapshot(old.State, old.EntryId).Encode(), old.EntryId), "Cross-version duplicate ID missed.");
        });
        Test("smart/metadata-drift-tamper-and-budget", () => {
            var state = Smart("hoa-[3H2+O2=H2O]"); var balance = state.Balance(out _); string tag = new ManagedSnapshot(balance).Encode();
            Reject(() => ManagedSnapshot.Decode(tag.Substring(0, tag.Length - 5) + "AAAAA"));
            Reject(() => new ManualFormulaState(state.Readings, result: balance.Result));
            Reject(() => new ManualFormulaState(Smart("hoa-[H2+Cl2=HCl]").Readings, result: balance.Result, beforeBalance: balance.BeforeBalance));
            Reject(() => ManagedSnapshot.Decode(new string('x', ManagedSnapshot.MaxTagCodeUnits + 1)));
            Check(CandidateExporter.ToOmml(ManagedSnapshot.Decode(tag).Selected) == CandidateExporter.ToOmml(balance.Selected!), "Export differs from the stored result.");
        });
        Test("smart/panel-balance-preview-snapshot", () => {
            using var panel = new ManualPanel(); var state = Smart("hoa-[3H2+O2=H2O]"); panel.Present(state, "source", "test", smartSettings); panel.Show();
            string before = panel.PreviewIdentity; panel.AssistanceAction("balance");
            Check(panel.PreviewIdentity != before && panel.PreviewIdentity == panel.Formula!.Identity && panel.PreviewCandidateId == panel.Formula.Selected!.Id, "Preview is stale.");
            panel.AssistanceAction("cancel-balance"); Check(panel.Formula!.Selected!.Id == state.Selected!.Id && panel.Formula.KeepFromAuto, "Panel cancellation differs.");
            panel.Present(state.Balance(out _), "managed", "reopen", smartSettings); SavePanel(panel, "balance-panel.png");
        });
        Test("smart/panel-products-explicit-preview", () => {
            using var panel = new ManualPanel(); panel.Present(Smart("hoa-[3H2+O2=]"), "source", "test", smartSettings); panel.Show();
            Check(!panel.Controls.Find("confirmFormula", true).Single().Enabled, "Draft may convert.");
            panel.AssistanceAction("condition", "activation=ignition");
            string proposal = panel.Proposals.Single().Id;
            Reject(() => panel.AssistanceAction("accept-product", proposal));
            panel.AssistanceAction("preview-product", proposal); Check(!panel.Controls.Find("confirmFormula", true).Single().Enabled, "Unaccepted proposal may convert.");
            SavePanel(panel, "products-proposal-panel.png"); panel.AssistanceAction("accept-product", proposal);
            Check(panel.Formula!.Products != null && panel.PreviewIdentity == panel.Formula.Identity, "Product acceptance differs.");
            panel.AssistanceAction("cancel-balance"); Check(panel.Formula.Result!.Source.Raw == "3H2+O2->H2O", "Panel cancel lost coefficients.");
            panel.AssistanceAction("drop-products"); Check(panel.Formula.Selected == null, "Drop products kept complete result.");
        });
        Write("report.json", new { capturedAtUtc = DateTime.UtcNow.ToString("o"), passed = results.Count - failures, failed = failures, results,
            limits = new[] { "Pure core/Word state contracts and isolated WinForms controls. Native Word transaction/keyboard acceptance is separate." } });
        Console.WriteLine($"Smart Word contracts: {results.Count - failures} PASS / {failures} FAIL"); return failures == 0 ? 0 : 1;
    }
    private static void SavePanel(ManualPanel panel, string name)
    {
        panel.PerformLayout();
        using var bitmap = new Bitmap(panel.Width, panel.Height); panel.DrawToBitmap(bitmap, new Rectangle(Point.Empty, panel.Size)); bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
        foreach (var control in Descendants(panel).Where(c => c.Visible))
            Check(control.Left >= 0 && control.Top >= 0 && control.Right <= control.Parent!.ClientSize.Width && control.Bottom <= control.Parent.ClientSize.Height, "Clipped control: " + control.Text + " " + control.Bounds + " parent " + control.Parent!.ClientSize);
    }
}
