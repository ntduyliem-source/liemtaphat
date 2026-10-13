using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Locus.Application;
using Locus.Core.Detection;

internal static class RepairCompatibilityVerification
{
    public static int Run(string output)
    {
        Directory.CreateDirectory(output);
        const string directory = "tests/Locus.Application.Tests/Fixtures/repair-baseline";
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"manifest.json")));
        var results = new List<object>(); var failed = 0;
        foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var name = fixture.GetProperty("file").GetString()!;
            try
            {
                var json = File.ReadAllText(Path.Combine(directory,name));
                Require(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))) == fixture.GetProperty("sha256").GetString(), "Historical fixture was modified");
                var opened = DocumentCodec.Open(json);
                Require(opened.IsSupported, opened.UnavailableReason ?? "Snapshot did not open");
                Require(DocumentCodec.Serialize(opened.Document!) == json, "Restoration changed historical content or identities");
                if (opened.Document is FormulaDocument formula)
                {
                    using var session = new FormulaSession(new NoAnalysis());
                    session.Load(formula.State);
                    Require(session.State.Raw == fixture.GetProperty("raw").GetString(), "Original source changed");
                    Require(session.State.CandidateId == formula.State.CandidateId, "Stored selection changed");
                    if (session.State.Content is {} restoredContent)
                        Require(restoredContent.ResultText(0,restoredContent.Raw.Length) == fixture.GetProperty("result").GetString(), "Stored result was reinterpreted");
                    if (name == "math-repair.locus") Require(session.State.Candidate?.Kind == "repair", "Repair choice lost");
                    if (name == "mixed-chemistry.locus")
                    {
                        var content = session.State.Content!;
                        Require(content.Regions.First().KeepText, "Keep-text decision lost");
                        Require(content.Regions.Count(r=>r.ResultOverride?.ManagedBalance == true) == 2, "Balance decisions lost");
                        Require(content.Regions.Any(r=>r.ResultOverride?.ProductProposal != null), "Product provenance lost");
                        Require(content.Commands.Count >= 2, "Content transactions lost");
                        session.UpdateSource("x^3");
                        Require(session.Undo(), "Cannot undo an edit after restoring");
                        Require(DocumentCodec.Serialize(formula with {State=session.State}) == json, "Undo did not restore loaded snapshot exactly");
                    }
                }
                results.Add(new {name,passed=true});
            }
            catch (Exception ex) { failed++; results.Add(new{name,passed=false,error=ex.ToString()}); Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); }
        }
        var report = new {status=failed==0?"PASSED":"FAILED",total=results.Count,failed,results};
        File.WriteAllText(Path.Combine(output,"compatibility.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new {report.status,report.total,report.failed}));
        return failed==0?0:1;
    }
    private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
    private sealed class NoAnalysis : IAnalysisScheduler
    {
        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request,CancellationToken token) => throw new Exception("Opening a historical snapshot must not invoke analysis");
    }
}
