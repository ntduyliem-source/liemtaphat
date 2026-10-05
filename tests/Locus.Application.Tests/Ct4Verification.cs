using System.Text.Json;
using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Parsing;
using Locus.Core.Serialization;

public static class Ct4Verification
{
    public static async Task<int> Run(string output)
    {
        Directory.CreateDirectory(output);var checks=new List<object>();int failed=0;
        void Assert(bool value,string label="CT4 contract"){if(!value)throw new Exception(label);}
        async Task Check(string name,Func<Task> action){try{await action();checks.Add(new{name,passed=true});}catch(Exception e){failed++;checks.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine($"FAIL {name}: {e.Message}");}}
        Task Sync(Action a){a();return Task.CompletedTask;}
        FormulaSettings Settings(DetectionDomains domains=DetectionDomains.All,InputMode mode=InputMode.Explicit)=>new(mode,EnabledDomains:domains,MarkerProfiles:MarkerPreferences.Default);
        async Task<FormulaSession> Ready(string raw){var s=new FormulaSession(new NativeAnalysisScheduler());s.Configure(Settings());s.UpdateSource(raw);Assert(await s.AnalyzeContentAsync());return s;}
        CandidateSet Parse(string raw)=>new FormulaParser().Parse(new(raw),new(0,raw.Length));
        foreach(var (raw,expected) in new[]{("x^2+1/2","(x^2+1)/2"),("x*y+1/2","(x*y+1)/2"),("x-1/2","(x-1)/2"),("x+1/y","(x+1)/y"),("can x+1","can(x+1)"),("can x-1","can(x-1)")})
            await Check("scope-"+raw,()=>Sync(()=>{
                var set=Parse(raw);Assert(set.Candidates[0].Kind=="direct");var repair=set.Candidates.Single(c=>c.Kind=="repair");
                Assert(CandidateExporter.ToLatex(repair)==CandidateExporter.ToLatex(Parse(expected).Candidates[0]),"wrong proposed AST");
                string edited=SourceEdit.Apply(set.Source,repair.Edits);
                Assert(CandidateExporter.ToLatex(Parse(edited).Candidates[0])==CandidateExporter.ToLatex(repair),"edit and preview disagree");
                var routed=AnalysisWire.Analyze(new(raw,1,Settings(DetectionDomains.Math)));Assert(routed.Regions.SelectMany(r=>r.Candidates).Any(c=>c.Kind=="repair"));
            }));
        await Check("group-boundaries-missing-close-and-budget",()=>Sync(()=>{
            foreach(var raw in new[]{"(x^2+1)/2","x+(1/2)","can(x)+1"})Assert(Parse(raw).Candidates.All(c=>c.Kind!="repair"),raw);
            foreach(var raw in new[]{"(x+1","can(x+1"}){var set=Parse(raw);Assert(set.Candidates.Count==1&&set.Candidates[0].Kind=="repair"&&set.Candidates[0].Edits.Single().Text==")");}
            Assert(Parse("((x+1").Candidates.Count==0);Assert(Parse("x+1/2+y+1/3").Candidates.Count<=3);
        }));
        await Check("protected-prose-paths-and-math-markers",async()=>{
            using var s=await Ready("URL https://example.org/x-1/2 và a@b.com; data-set/1, C:\\Users\\A và 05/10/2026. Xét lc[x-1/2].");
            Assert(s.State.Content!.Regions.Count==1,s.State.Content.Regions.Count.ToString());Assert(s.State.Content.Regions[0].Raw=="lc[x-1/2]");
        });
        await Check("historical-snapshots-open-with-exact-id-and-export",()=>Sync(()=>{
            foreach(var path in Directory.GetFiles("tests/Locus.Core.Tests/Fixtures/ct4","*.json").Where(p=>!p.EndsWith(".expected.json"))){
                var set=CandidateSetSerializer.Deserialize(File.ReadAllText(path));
                using var expected=JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(path,"expected.json")));
                Assert(set.Candidates.Count==expected.RootElement.GetArrayLength());
                for(int i=0;i<set.Candidates.Count;i++){var c=set.Candidates[i];var e=expected.RootElement[i];Assert(c.Id==e.GetProperty("Id").GetString()&&CandidateExporter.ToMathMl(c)==e.GetProperty("MathMl").GetString()&&CandidateExporter.ToOmml(c)==e.GetProperty("Omml").GetString());}
            }
        }));
        await Check("inline-drafts-retain-exact-source-boundaries",async()=>{
            using var s=await Ready("🧪 Xét H2+O2= trong bài. Sau đó HCl+NaOH=, kết thúc.");
            Assert(s.State.Content!.Regions.Count==2,string.Join(" | ",s.State.Content.Regions.Select(r=>r.Raw)));
            Assert(s.State.Content.Regions[0].Raw=="H2+O2="&&s.State.Content.Regions[1].Raw=="HCl+NaOH=");
            Assert(await s.StudioBalanceAsync());Assert(s.State.Content.ResultText(0,s.State.Raw.Length).StartsWith("🧪 Xét "));
            Assert(s.State.Content.Regions.Count(FormulaSession.HasManagedCoefficientChange)==1);
        });
        await Check("balanced-input-does-not-create-false-undo",async()=>{
            using var s=await Ready("2H2+O2=2H2O");var before=s.State;
            Assert(!await s.StudioBalanceAsync()&&ReferenceEquals(s.State,before)&&!s.HasStudioBalance());
            Assert(s.StudioChemistryOutcomes.Single().Status=="already-balanced");
        });
        await Check("products-only-can-be-dropped-directly",async()=>{
            using var s=await Ready("HCl+NaOH=");Assert(await s.StudioBalanceAsync());var filled=s.State;var r=filled.Content!.Regions.Single();
            Assert(r.ResultOverride?.ProductProposal!=null&&!s.HasStudioBalance());
            Assert(await s.RunStudioChemistryAsync(s.StudioCommand(StudioChemistryAction.DropProducts,[r.Id])));
            Assert(s.State.Content!.Regions.Single().Display==null&&s.State.Raw=="HCl+NaOH=");Assert(s.Undo()&&ReferenceEquals(s.State,filled));
        });
        await Check("fill-cancel-drop-preserves-typed-coefficients-and-undo",async()=>{
            using var s=await Ready("3H2+O2=");var raw=s.State.Raw;Assert(await s.StudioBalanceAsync());var r=s.State.Content!.Regions.Single();
            Assert(await s.RunStudioChemistryAsync(s.StudioCommand(StudioChemistryAction.CancelBalance,[r.Id])));
            Assert(s.State.Content!.Regions.Single().Display?.Source.Raw=="3H2+O2->H2O");
            Assert(await s.RunStudioChemistryAsync(s.StudioCommand(StudioChemistryAction.DropProducts,[r.Id]))&&s.State.Raw==raw);
            Assert(s.State.Content!.Regions.Single().Display==null);Assert(s.Undo()&&s.State.Content!.Regions.Single().ResultOverride?.ProductProposal!=null);
        });
        await Check("command-targets-never-expand-outside-selection",async()=>{
            using var s=await Ready("lc[x^2]\nhoa-[H2+O2=]\nhoa-[N2+H2=NH3]");var c=s.State.Content!;
            Assert(!await s.RunStudioChemistryAsync(s.StudioCommand(StudioChemistryAction.Balance,[c.Regions[0].Id])));
            Assert(!await s.RunStudioChemistryAsync(s.StudioCommand(StudioChemistryAction.Balance,[])));
            Assert(await s.RunStudioChemistryAsync(s.StudioCommand(StudioChemistryAction.Balance,[c.Regions[1].Id])));
            Assert(s.State.Content!.Regions.Count(FormulaSession.HasManagedCoefficientChange)==1&&s.State.Content.Regions[2].ResultOverride==null);
            Assert(!await s.RunStudioChemistryAsync(s.StudioCommand(StudioChemistryAction.DropProducts,[c.Regions[2].Id])));
        });
        await Check("chemistry-mode-guard-and-one-step-mode-undo",async()=>{
            using var s=await Ready("H2SO4");var before=s.State;
            Assert(await s.ReanalyzeWithSettingsAsync(Settings(DetectionDomains.Math)));Assert(s.State.Content!.Regions.Count==0&&!s.CanRunStudioBalance);
            Assert(s.Undo()&&ReferenceEquals(s.State,before));
            foreach(var mode in new[]{Settings(DetectionDomains.Math),Settings(DetectionDomains.Physics),Settings(mode:InputMode.Markers)}){
                s.UpdateSource("hoa-[H2+O2=H2O]");await s.ReanalyzeWithSettingsAsync(mode);Assert(!await s.StudioBalanceAsync()&&!s.HasStudioBalance());
            }
        });
        await Check("mode-reanalysis-preserves-compatible-choice-only",async()=>{
            using var s=await Ready("lc[x^2+1/2]\nH2+O2=");var r=s.State.Content!.Regions[0];s.Select(0,r.Readings!.Candidates.Single(c=>c.Kind=="repair").Id);await s.StudioBalanceAsync();
            Assert(await s.ReanalyzeWithSettingsAsync(Settings(DetectionDomains.Math)));Assert(s.State.Content!.Regions.All(v=>v.ResultOverride==null));
            Assert(s.State.Content.Regions[0].Display?.Kind=="repair");
            Assert(DocumentCodec.Open(DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()))).IsSupported);
        });
        await Check("result-projection-whole-region-text-and-partial",async()=>{
            using var s=await Ready("Chữ Việt 🧪\nCho lc[x^2+1/2].\nKết thúc.");var c=s.State.Content!;var r=c.Regions.Single();
            var all=ContentExport.Capture(s.State);Assert(all.Parts[0].Text=="Chữ Việt 🧪\nCho "&&all.FormulaCount==1);
            Assert(ContentExport.Capture(s.State,c.Select(r.Start,r.End)).Parts.Single().Formula?.Id==r.Display?.Id);
            Assert(ContentExport.Capture(s.State,c.Select(0,r.Start)).FormulaCount==0);
            bool blocked=false;try{ContentExport.Capture(s.State,c.Select(r.Start+1,r.End));}catch(InvalidOperationException){blocked=true;}Assert(blocked);
        });
        await Check("repair-history-does-not-replace-source",async()=>{
            using var s=await Ready("x^2+1/2");var before=s.State;var r=before.Content!.Regions.Single();
            Assert(s.Select(0,r.Readings!.Candidates.Single(c=>c.Kind=="repair").Id)&&s.State.Raw==before.Raw);
            Assert(s.Undo()&&ReferenceEquals(s.State,before));
        });
        await Check("rapid-mode-return-cancels-older-reanalysis",async()=>{
            using var initial=await Ready("H2SO4");var gate=new Deferred();using var s=new FormulaSession(gate);s.Load(initial.State);
            var old=s.ReanalyzeWithSettingsAsync(Settings(DetectionDomains.Math));
            var latest=s.ReanalyzeWithSettingsAsync(Settings());
            Assert(gate.Calls.Count==2&&gate.Calls[0].Token.IsCancellationRequested);
            gate.Complete(1);Assert(await latest);var expected=s.State;gate.Complete(0);Assert(!await old&&ReferenceEquals(expected,s.State));
            Assert(s.State.Content!.Regions.Single().Display?.Document.Domain=="chemistry");
        });
        var report=new{status=failed==0?"PASSED":"FAILED",total=checks.Count,failed,checks};
        File.WriteAllText(Path.Combine(output,"ct4-contracts.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(new{report.status,report.total,report.failed}));return failed==0?0:1;
    }
}
