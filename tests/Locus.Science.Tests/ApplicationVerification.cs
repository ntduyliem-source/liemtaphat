using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Serialization;

internal static class ApplicationVerification
{
    internal static async Task<int> Run(string root,string output)
    {
        var checks=new List<object>();int failed=0;
        async Task Check(string id,Func<Task> test){try{await test();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine(id+": "+e.Message);}}
        void Assert(bool value){if(!value)throw new Exception("Contract failed.");}
        Task Sync(Action test){test();return Task.CompletedTask;}
        string Snapshot(FormulaSession s)=>CandidateSetSerializer.Serialize(s.State.Region!);
        var scheduler=new NativeAnalysisScheduler();
        for(int mask=0;mask<8;mask++)
        {
            var flags=(DetectionDomains)mask;
            await Check("preferences-and-worker-wire/"+mask,()=>Sync(()=>{
                var settings=new FormulaSettings(EnabledDomains:flags);var prefs=new EditorPreferences(2,settings,new(),true);
                Assert(EditorPreferences.Read(prefs.Serialize())==prefs);
                var request=new AnalysisRequest("H2SO4",9007199254740993,settings);
                var response=AnalysisWire.Deserialize(AnalysisWire.Run(AnalysisWire.Request(request)));
                Assert(response.Source.Revision==request.SourceRevision && (response.Regions.Count>0)==flags.HasFlag(DetectionDomains.Chemistry));
            }));
        }
        await Check("legacy/preferences-without-domain",()=>Sync(()=>{
            const string json="{\"version\":1,\"settings\":{\"mode\":\"Explicit\",\"open\":\"lc[\",\"close\":\"]\"},\"view\":{\"fontSize\":40,\"pixelScale\":2,\"whiteBackground\":false},\"autoSave\":true}";
            Assert(EditorPreferences.Read(json)?.Settings.EnabledDomains==DetectionDomains.Math);
        }));
        foreach(int invalid in new[]{8,-1,255})
        {
            await Check("settings/reject-unknown-bits/"+invalid,()=>Sync(()=>{
                var bad=new FormulaSettings(EnabledDomains:(DetectionDomains)invalid);
                using var s=new FormulaSession(scheduler);bool rejected=false;
                try{s.Configure(bad);}catch(ArgumentException){rejected=true;}Assert(rejected&&s.State.Raw=="");
                Assert(EditorPreferences.Read(new EditorPreferences(2,new(),new(),true).Serialize().Replace("\"Math\"",invalid.ToString()))==null);
            }));
        }
        foreach(var (name,raw,flags) in new[]{("chemistry","👩‍🏫 lc[SO₄²⁻]",DetectionDomains.Chemistry),("physics","👩‍🏫 lc[v₀ = 10 m/s²]",DetectionDomains.Physics)})
        {
            await Check(name+"/disable-save-open-edit-undo",async()=>{
                using var s=new FormulaSession(scheduler);s.Configure(new(InputMode.Markers,EnabledDomains:flags));s.UpdateSource(raw);Assert(await s.AnalyzeAsync());
                var snapshot=Snapshot(s);var id=s.State.CandidateId;var lease=s.Lease();
                s.Configure(s.State.Settings with {EnabledDomains=DetectionDomains.None});
                Assert(!s.IsCurrent(lease)&&s.CanExport&&Snapshot(s)==snapshot&&s.State.Raw==raw);
                string saved=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));
                Assert(JsonNode.Parse(saved)!["version"]!.GetValue<int>()==2);
                File.WriteAllText(Path.Combine(output,name+"-off.locus"),saved);
                var loaded=(FormulaDocument)DocumentCodec.Open(saved).Document!;
                using var reopened=new FormulaSession(scheduler);reopened.Load(loaded.State);
                Assert(reopened.CanExport&&Snapshot(reopened)==snapshot&&reopened.State.CandidateId==id&&reopened.State.Settings.EnabledDomains==DetectionDomains.None);
                reopened.UpdateSource("lc[H2O]");Assert(!reopened.CanExport);await reopened.AnalyzeAsync();Assert(!reopened.CanExport);
                Assert(reopened.Undo()&&reopened.CanExport&&Snapshot(reopened)==snapshot);
                var downgraded=JsonNode.Parse(saved)!;downgraded["version"]=1;Assert(!DocumentCodec.Open(downgraded.ToJsonString()).IsSupported);
            });
        }
        await Check("snapshot/enabling-does-not-reinterpret-selection",async()=>{
            using var s=new FormulaSession(scheduler);s.UpdateSource("x+1/2");await s.AnalyzeAsync();var repair=s.State.Region!.Candidates.Last();s.Select(0,repair.Id);
            var before=Snapshot(s);s.Configure(s.State.Settings with {EnabledDomains=DetectionDomains.All});Assert(s.State.CandidateId==repair.Id&&Snapshot(s)==before);
            await s.AnalyzeAsync();Assert(s.State.Candidate!.Kind=="direct");
        });
        await Check("settings/cancel-late-domain-result",async()=>{
            var delayed=new Deferred();using var s=new FormulaSession(delayed);s.Configure(new(EnabledDomains:DetectionDomains.Chemistry));s.UpdateSource("H2O");
            var old=s.AnalyzeAsync();s.Configure(new(EnabledDomains:DetectionDomains.None));Assert(delayed.Calls[0].Token.IsCancellationRequested);
            delayed.Complete(0);Assert(!await old&&!s.CanExport&&!s.IsBusy&&s.State.Settings.EnabledDomains==DetectionDomains.None);
        });
        await Check("settings/composition-guard",()=>Sync(()=>{
            using var s=new FormulaSession(scheduler);s.UpdateSource("x mu",true);bool rejected=false;
            try{s.Configure(new(EnabledDomains:DetectionDomains.All));}catch(InvalidOperationException){rejected=true;}
            Assert(rejected&&s.IsComposing&&s.State.Settings.EnabledDomains==DetectionDomains.Math);
        }));
        await Check("legacy/frozen-web1-file",()=>Sync(()=>{
            string bytes=File.ReadAllText(Path.Combine(root,"artifacts/web1/runs/chromium/selected.locus"));
            var result=DocumentCodec.Open(bytes);Assert(result.Document is FormulaDocument);
            var doc=(FormulaDocument)result.Document!;Assert(doc.State.Settings.EnabledDomains==DetectionDomains.Math&&doc.State.Candidate!=null);
            var original=AnalysisWire.Serialize(doc.State.Analysis!);
            var next=(FormulaDocument)DocumentCodec.Open(DocumentCodec.Serialize(doc)).Document!;
            Assert(AnalysisWire.Serialize(next.State.Analysis!)==original);
        }));
        await Check("file/unknown-domain-bits-retains-original",async()=>{
            using var s=new FormulaSession(scheduler);s.UpdateSource("x^2");await s.AnalyzeAsync();
            var json=JsonNode.Parse(DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),0,s.State,new())))!;
            var payload=JsonNode.Parse(json["payload"]!.GetValue<string>())!;payload["settings"]!["enabledDomains"]=8;
            json["payload"]=payload.ToJsonString();json["sha256"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToJsonString())));
            var original=json.ToJsonString();var result=DocumentCodec.Open(original);Assert(!result.IsSupported&&result.OriginalJson==original&&s.CanExport);
        });
        File.WriteAllText(Path.Combine(output,"application-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,summary=new{checks=checks.Count,passed=checks.Count-failed,failed},results=checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"E3 application: {checks.Count-failed}/{checks.Count} passed");return failed;
    }
    private sealed class Deferred:IAnalysisScheduler
    {
        internal readonly List<(AnalysisRequest Request,CancellationToken Token,TaskCompletionSource<AnalysisResult> Completion)> Calls=[];
        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request,CancellationToken token){var completion=new TaskCompletionSource<AnalysisResult>(TaskCreationOptions.RunContinuationsAsynchronously);Calls.Add((request,token,completion));return completion.Task;}
        internal void Complete(int i)=>Calls[i].Completion.SetResult(AnalysisWire.Analyze(Calls[i].Request));
    }
}
