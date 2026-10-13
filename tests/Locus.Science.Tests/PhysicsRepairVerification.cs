using System.Text.Json;
using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;

internal static class PhysicsRepairVerification
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
        Candidate Parse(string raw, DetectionDomains domain) => AnalysisWire.Analyze(new(raw, 17,
            new(EnabledDomains: domain, MarkerProfiles: MarkerPreferences.Default))).Regions.Single().Candidates.First(c => c.Document.Domain == "physics");
        foreach (var domain in new[] { DetectionDomains.Physics, DetectionDomains.All })
        {
            await Check("R02/group-exponent/" + domain, () =>
            {
                const string raw = "10 kg*(m/s)^2";
                var candidate = Parse(raw, domain); var quantity = candidate.Document.Root;
                Require(quantity.Type == "Quantity" && quantity.Children[0].Value == "10", "Exponent escaped unit factor and squared quantity");
                var product = quantity.Children[1];
                Require(product.Type == "Binary" && product.Operator == "multiply" && product.Children[0].Name == "kg", "kg changed");
                var power = product.Children[1];
                Require(power.Type == "Power" && power.Children[0].Type == "Binary" && power.Children[0].Operator == "divide" && power.Children[1].Value == "2", "Missing grouped unit power");
                Require(power.SourceSpans.Single().Equals(new TextSpan(6, 13)), "Group exponent source span changed");
                Require(candidate.Source.Raw == raw && !candidate.Diagnostics.Any(), "Source or direct warning changed");
                Require(CandidateExporter.ToLatex(candidate) == @"10\,\mathrm{kg} \cdot {\left(\frac{\mathrm{m}}{\mathrm{s}}\right)}^{2}", "Export has wrong exponent scope");
                var set = new CandidateSet(candidate.Source, candidate.ContentSpan, candidate.ReplacementSpan, new[] { candidate });
                var json = CandidateSetSerializer.Serialize(set);
                Require(CandidateSetSerializer.Serialize(CandidateSetSerializer.Deserialize(json)) == json, "Unit power snapshot changed on roundtrip");
                var whole = Parse("(10 kg)^2", domain).Document.Root;
                Require(whole.Type == "Power" && whole.Children[0].Type == "Quantity", "Explicit quantity power changed");
                return Task.CompletedTask;
            });
            foreach (string raw in new[] { "10 kg * 2", "10 m / t", "10 kg * (2+1)", "10 kg*(m+1)", "10 kg*(m*2)", "10 kg*(m*x)" })
                await Check("R07/arithmetic/" + domain + "/" + raw, async () =>
                {
                    var candidate = Parse(raw, domain); var root = candidate.Document.Root;
                    Require(root.Type == "Binary" && root.Children[0].Type == "Quantity", "Quantity arithmetic not preserved");
                    Require(root.Children[0].Children[0].Value == "10", "Quantity value changed");
                    using var session = new FormulaSession(new NativeAnalysisScheduler());
                    session.Configure(new(EnabledDomains: domain, MarkerProfiles: MarkerPreferences.Default)); session.UpdateSource(raw);
                    Require(await session.AnalyzeContentAsync(), "Session rejected analysis");
                    var physics = session.State.Content!.Regions.Single().Readings!.Candidates.First(c => c.Document.Domain == "physics");
                    session.Select(0, physics.Id);
                    Require(session.State.Content!.Regions.Single().Display!.Document.Root.Type == "Binary", "Session lost arithmetic region");
                    Require(session.State.Raw == raw, "Session changed raw text");
                });
        }
        await Check("R02/unit-variants", () =>
        {
            foreach (string raw in new[] { "10 kg*(m/s)^-2", "10 kg*((m/s))^2", "10 kg/(m*s)^2", "unit((m/s)^2)", "10 m/s^2" })
                Require(Parse(raw, DetectionDomains.Physics).Document.Domain == "physics", "Unit group rejected: " + raw);
            foreach (string raw in new[] { "10 kg*(m/s)^", "10 kg**2", "10 kg//2", "10 kg*(m/s)^x" })
                Require(AnalysisWire.Analyze(new(raw, 0, new(EnabledDomains: DetectionDomains.Physics))).Regions.Count == 0, "Malformed unit accepted: " + raw);
            const string unclosed = "10 kg*(m/s";
            var incomplete = AnalysisWire.Analyze(new(unclosed, 0, new(EnabledDomains: DetectionDomains.Physics)));
            Require(incomplete.ContentEligibility == "blocked" && incomplete.Regions.SelectMany(r => r.Candidates).All(c => c.Kind == "repair") && incomplete.Source.Raw == unclosed,
                "Missing group close became an eligible direct reading");
            return Task.CompletedTask;
        });
        await Check("R02/chained-unit-exponents", async () =>
        {
            foreach(string raw in new[] { "10 kg*(m/s)^2^3", "10 m^2^3", "10 m^2 ^3", "10 m²^3", "10 m^2³" })
            {
                var parsed=AnalysisWire.Analyze(new(raw,0,new(EnabledDomains:DetectionDomains.Physics)));
                Require(parsed.Regions.Count==0&&parsed.Diagnostics.Any(d=>d.Code=="UNSUPPORTED_CHAINED_UNIT_EXPONENT"),"Chained unit power escaped to outer quantity: "+raw);
                using var session=new FormulaSession(new NativeAnalysisScheduler());
                session.Configure(new(EnabledDomains:DetectionDomains.Physics,MarkerProfiles:MarkerPreferences.Default));session.UpdateSource(raw);await session.AnalyzeContentAsync();
                Require(session.State.Content!.Regions.All(r=>r.Display==null)&&session.State.Raw==raw,"Session displayed unsupported chained quantity power");
            }
            foreach(var domain in new[]{DetectionDomains.Physics,DetectionDomains.All})
            {
                var whole=Parse("(10 m^2)^3",domain).Document.Root;
                Require(whole.Type=="Power"&&whole.Children[0].Type=="Quantity"&&whole.Children[0].Children[1].Type=="Power"&&whole.Children[1].Value=="3","Explicit whole quantity exponent rejected");
                Require(Parse("10 kg*((m/s)^2)^3",domain).Document.Root.Type=="Quantity","Explicit nested unit exponent rejected");
            }
        });
        File.WriteAllText(Path.Combine(output, "physics-repair-verification.json"), JsonSerializer.Serialize(new { checks, passed = checks.Count - failed, failed }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"FIX1 physics: {checks.Count - failed}/{checks.Count}"); return failed;
    }
}
