using System.Text.Json;
using Locus.Application;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;

internal static class ReactionDraftRepairVerification
{
    internal static async Task<int> Run(string output)
    {
        var checks = new List<object>(); int failed = 0;
        async Task Check(string id, Func<Task> test)
        {
            try { await test(); checks.Add(new { id, status = "PASS" }); }
            catch (Exception e) { failed++; checks.Add(new { id, status = "FAIL", error = e.Message }); Console.WriteLine(id + ": " + e.Message); }
        }
        void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        string Export(FormulaState state) => string.Concat(ContentExport.Capture(state).Parts.Select(p => p.Formula is {} c ? ChemistryProjection.Create(c.Document.Root).Source.Raw : p.Text));
        foreach (var domain in new[] { DetectionDomains.Chemistry, DetectionDomains.All })
        {
            foreach (string input in new[] { "Phản ứng H2+O2= 2 H2O.", "Phản ứng H2+O2 =2 H2O.", "Phản ứng H2 + O2 = 2H2O.", "Phản ứng H2 +O2= 2H2O." })
                await Check("R03/complete/" + domain + "/" + input, async () =>
                {
                    using var session = new FormulaSession(new NativeAnalysisScheduler());
                    session.Configure(new(EnabledDomains: domain, MarkerProfiles: MarkerPreferences.Default)); session.UpdateSource(input);
                    Require(await session.AnalyzeContentAsync(), "Analyze failed");
                    var before = session.State;
                    Require(before.Content!.Regions.Count == 1 && before.Content.Regions[0].Display?.Document.Root.Type == "ChemReaction", "Complete reaction split/lost");
                    Require(await session.StudioBalanceAsync(), "Balance failed");
                    Require(Export(session.State) == "Phản ứng 2H2+O2->2H2O.", "Duplicated or lost product: " + Export(session.State));
                    Require(session.State.Content!.Regions.All(r => r.ResultOverride?.ProductProposal == null), "Invented product ownership");
                    Require(session.State.Raw == input, "Raw source changed");
                    Require(session.Undo() && ReferenceEquals(session.State, before), "Undo did not restore prebalance state");
                    Require(session.Redo() && Export(session.State) == "Phản ứng 2H2+O2->2H2O.", "Redo changed result");
                });
            foreach (string rhs in new[] { "2", "H2O+", "Xx", "2 Xx", "Xx+H2O", ".5H2O", "0.5H2O" })
                await Check("R03/occupied-invalid/" + domain + "/" + rhs, async () =>
                {
                    string raw = "Phản ứng H2+O2= " + rhs + ".";
                    Require(ReactionDraftLocator.Find(new(raw)).Count == 0, "Occupied RHS reported empty");
                    using var session = new FormulaSession(new NativeAnalysisScheduler());
                    session.Configure(new(EnabledDomains: domain, MarkerProfiles: MarkerPreferences.Default)); session.UpdateSource(raw); await session.AnalyzeContentAsync();
                    await session.StudioBalanceAsync();
                    Require(session.State.Content!.Regions.All(r => r.ResultOverride?.ProductProposal == null), "Invented products for invalid RHS");
                    Require(session.State.Raw == raw, "Invalid source changed");
                });
            foreach (string raw in new[] { "Phản ứng H₂+O₂=", "Phản ứng H₂+O₂=.\nHCl+NaOH=" })
                await Check("R08/unicode/" + domain + "/" + raw, async () =>
                {
                    using var session = new FormulaSession(new NativeAnalysisScheduler());
                    session.Configure(new(EnabledDomains: domain, MarkerProfiles: MarkerPreferences.Default)); session.UpdateSource(raw); await session.AnalyzeContentAsync();
                    var draft = session.State.Content!.Regions.First();
                    Require(draft.Raw == "H₂+O₂=", "Unicode draft boundary lost");
                    Require(draft.Start == 9 && draft.End == 15, "Unicode UTF16 spans changed");
                    Require(await session.StudioBalanceAsync(), "Unicode fill failed");
                    Require(Export(session.State).StartsWith("Phản ứng 2H2+O2->2H2O", StringComparison.Ordinal), "Unicode result differs from ASCII");
                    Require(session.State.Raw == raw, "Unicode raw changed");
                });
        }
        await Check("R03/draft-boundaries", () =>
        {
            foreach (string raw in new[] { "H2+O2=; HCl+NaOH=", "H₂+O₂=\nHCl+NaOH=" })
                Require(ReactionDraftLocator.Find(new(raw)).Count == 2, "Adjacent drafts merged");
            Require(ReactionDraftLocator.Find(new("SO₄²⁻+Ba²⁺=")).Single().Equals(new TextSpan(0, 11)), "Charged Unicode draft lost");
            return Task.CompletedTask;
        });
        await Check("R08/explicit-chemistry-command-guards", async () =>
        {
            foreach (var domain in new[] { DetectionDomains.Math, DetectionDomains.Physics })
            {
                using var session = new FormulaSession(new NativeAnalysisScheduler());
                session.Configure(new(EnabledDomains: domain, MarkerProfiles: MarkerPreferences.Default)); session.UpdateSource("H₂+O₂="); await session.AnalyzeContentAsync();
                var before = session.State;
                Require(!session.CanRunStudioBalance && !await session.StudioBalanceAsync() && ReferenceEquals(before, session.State), "Disabled chemistry command changed state");
            }
            using var all = new FormulaSession(new NativeAnalysisScheduler());
            all.Configure(new(EnabledDomains: DetectionDomains.All, MarkerProfiles: MarkerPreferences.Default)); all.UpdateSource("x+1=2"); await all.AnalyzeContentAsync();
            var math = all.State;
            Require(!await all.StudioBalanceAsync() && ReferenceEquals(math, all.State), "Explicit chemistry reinterpreted selected math formula");
        });
        await Check("R03/wrapped-draft-cancel-drop-history", async () =>
        {
            const string raw = "Nguồn hoa-[H₂+O₂=].";
            using var session = new FormulaSession(new NativeAnalysisScheduler());
            session.Configure(new(EnabledDomains: DetectionDomains.All, MarkerProfiles: MarkerPreferences.Default)); session.UpdateSource(raw); await session.AnalyzeContentAsync();
            var original = session.State;
            Require(await session.StudioBalanceAsync(), "Wrapped Unicode draft not filled");
            var balanced = session.State; var region = balanced.Content!.Regions.Single();
            Require(Export(balanced) == "Nguồn 2H2+O2->2H2O.", "Wrapped products wrong");
            Require(await session.StudioBalanceAsync(region.Id), "Cancel failed");
            Require(Export(session.State) == "Nguồn H2+O2->H2O.", "Cancel removed products instead of coefficients");
            Require(await session.RunStudioChemistryAsync(session.StudioCommand(StudioChemistryAction.DropProducts, new[] { region.Id })), "Drop products failed");
            Require(Export(session.State) == raw && session.State.Raw == raw, "Drop did not restore original source");
            Require(session.Undo() && Export(session.State) == "Nguồn H2+O2->H2O.", "Drop undo failed");
            Require(session.Undo() && ReferenceEquals(session.State, balanced), "Cancel undo failed");
            Require(session.Undo() && ReferenceEquals(session.State, original), "Fill undo failed");
        });
        await Check("R03/charged-and-multispecies-complete", async () =>
        {
            foreach (string raw in new[] { "Ion Na^+ +Cl^-= NaCl.", "Ion Ba²⁺+SO₄²⁻= BaSO4.", "Phản ứng HCl+NaOH= NaCl+H2O." })
            {
                using var session = new FormulaSession(new NativeAnalysisScheduler());
                session.Configure(new(EnabledDomains: DetectionDomains.Chemistry, MarkerProfiles: MarkerPreferences.Default)); session.UpdateSource(raw); await session.AnalyzeContentAsync();
                Require(session.State.Content!.Regions.Count == 1 && session.State.Content.Regions[0].Display?.Document.Root.Type == "ChemReaction", "Complete reaction lost: " + raw);
                Require(ReactionDraftLocator.Find(new(raw)).Count == 0, "Complete ionic reaction classified draft");
                await session.StudioBalanceAsync();
                Require(session.State.Content.Regions.All(r => r.ResultOverride?.ProductProposal == null) && session.State.Raw == raw, "Claimed entered products");
            }
        });
        File.WriteAllText(Path.Combine(output, "reaction-draft-repair-verification.json"), JsonSerializer.Serialize(new { checks, passed = checks.Count - failed, failed }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"FIX1 chemistry draft: {checks.Count - failed}/{checks.Count}"); return failed;
    }
}
