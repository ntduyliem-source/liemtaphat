using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class ContentVerification
{
    public static async Task<int> Run(string output)
    {
        Directory.CreateDirectory(output); var results=new List<object>(); int failed=0;
        void Assert(bool condition,string message="Content contract failed"){if(!condition)throw new Exception(message);}
        async Task Check(string name,Func<Task> test){try{await test();results.Add(new{name,passed=true});}catch(Exception e){failed++;results.Add(new{name,passed=false,error=e.ToString()});Console.Error.WriteLine(name+": "+e.Message);}}
        var settings=new FormulaSettings(EnabledDomains:DetectionDomains.All,MarkerProfiles:MarkerPreferences.Default);
        var scheduler=new NativeAnalysisScheduler();
        async Task<FormulaSession> Session(string raw){var s=new FormulaSession(scheduler);s.Configure(settings);s.UpdateSource(raw);if(!await s.AnalyzeContentAsync()){await ContentAnalyzer.AnalyzeAsync(new(raw,s.State.SourceRevision,settings),scheduler,null,CancellationToken.None);Assert(false,s.Error);}return s;}
        const string paragraph="👩‍🏫 e\u0301 Bài tập\r\nCho lc[x mũ 2 + 1]. Vận tốc ly-[v=10 m/s].\r\nXét hoa-[H2+O2=H2O].\nhttps://example.org/x^2 hello@example.org D:\\toan\\x^2\nGiữ nguyên câu cuối.";
        await Check("numbered-prose-stays-text-with-all-detectors",async()=>{
            string raw=string.Join("\r\n",Enumerable.Range(1,100).Select(i=>$"Dòng {i}: hoa-[H2+O2=H2O]. Giữ nguyên chữ Việt."));
            using var s=await Session(raw);var content=s.State.Content!;
            Assert(content.Raw==raw&&content.Regions.Count==100,"Expected 100 equations, without row numbers");
            Assert(content.Regions.All(r=>raw[r.Start..r.End]=="hoa-[H2+O2=H2O]"),"Prose was converted");
        });
        await Check("paragraph-domains-prose-url-nfd-crlf",async()=>{
            using var s=await Session(paragraph);var d=s.State.Content!;
            Assert(d.Regions.Count==3,"Expected three regions, got "+d.Regions.Count);
            Assert(string.Concat(d.Blocks().Select(b=>d.Raw[b.Start..b.End]))==paragraph);
            Assert(d.Regions.Select(r=>r.Display?.Document.Domain).SequenceEqual(new[]{"math","physics","chemistry"}));
            Assert(d.Regions.All(r=>r.Raw==paragraph[r.Start..r.End]));
            var copied=d.ResultText(0,d.Raw.Length);Assert(copied.Contains("https://example.org/x^2")&&copied.Contains("\r\n")&&copied.Contains("👩‍🏫 e\u0301")&&copied.EndsWith("Giữ nguyên câu cuối."));
        });
        await Check("date-in-prose-keeps-raw-while-explicit-fraction-converts",async()=>{
            const string raw="Bài thử máy 28/09: toan-[x mũ 2]; ly-[v=10 m/s]; hoa-[H2+O2=H2O].\nNgày 1/2: lc[28/09].";
            using var s=await Session(raw);var content=s.State.Content!;
            Assert(content.Regions.Count==4,"Dates were detected as formulas");
            Assert(content.Regions.All(r=>r.Raw.StartsWith("toan-[")||r.Raw.StartsWith("ly-[")||r.Raw.StartsWith("hoa-[")||r.Raw.StartsWith("lc[")),"Prose region was converted");
            var text=content.ResultText(0,raw.Length);Assert(text.Contains("Bài thử máy 28/09:")&&text.Contains("\nNgày 1/2:"),"Date text changed");
            using var single=await Session("28/09");Assert(single.State.Content!.Regions.Single().Display!=null,"Explicit fraction was blocked");
        });
        await Check("natural-text-needs-no-input-mode",async()=>{using var s=await Session("Cho x mũ 2 + 1. Tính v=10 m/s. Chất H2SO4.");Assert(s.State.Content!.Regions.Count>=2);Assert(s.State.Raw.StartsWith("Cho "));});
        await Check("reverse-partial-and-empty-selection",async()=>{using var s=await Session(paragraph);var d=s.State.Content!;var all=d.Select(d.Raw.Length,0);Assert(all.Whole.Count==3&&all.Partial.Count==0);var r=d.Regions[0];Assert(d.Select(r.Start+1,r.End).Partial.Single()==r.Id);Assert(d.Select(r.Start,r.Start).Whole.Count==0);});
        await Check("keep-text-survives-neighbor-edit-and-undo",async()=>{using var s=await Session(paragraph);var region=s.State.Content!.Regions[1];s.KeepContentText(region.Id,true);var before=s.State;s.UpdateSource("Lời mở đầu. "+paragraph);Assert(await s.AnalyzeContentAsync());var kept=s.State.Content!.Regions.Single(r=>r.Id==region.Id);Assert(kept.KeepText&&kept.Display==null);Assert(s.Undo()&&s.State==before);});
        await Check("actual-formula-edit-does-not-inherit-keep",async()=>{using var s=await Session("lc[x^2] và lc[x^2]");var first=s.State.Content!.Regions[0];s.KeepContentText(first.Id,true);s.UpdateSource("lc[x^3] và lc[x^2]");await s.AnalyzeContentAsync();Assert(s.State.Content!.Regions.All(r=>!r.KeepText)&&s.State.Content.Regions[0].Id!=first.Id);});
        await Check("identical-formulas-do-not-share-decisions",async()=>{using var s=await Session("lc[x^2] và lc[x^2]");var regions=s.State.Content!.Regions;Assert(regions[0].Id!=regions[1].Id);s.KeepContentText(regions[1].Id,true);s.UpdateSource("Đề bài: lc[x^2] và lc[x^2]");await s.AnalyzeContentAsync();Assert(!s.State.Content!.Regions[0].KeepText&&s.State.Content.Regions[1].KeepText);});
        await Check("invalid-marked-text-has-fx-without-invented-ast",async()=>{using var s=await Session("Giữ lc[abc@@] ở đây và lc[x^2].");Assert(s.State.Content!.Regions.Count==2);Assert(s.State.Content.Regions[0].Readings==null&&s.State.Content.Regions[0].Display==null);Assert(s.State.Content.Regions[1].Display!=null);});
        await Check("ambiguous-kept-as-text-and-repair-needs-selection",async()=>{using var s=await Session("x+1/2");Assert(s.State.Content!.Regions.Count>0,"No region for fraction");Assert(s.State.Content.Regions[0].Display?.Kind=="direct");using var incomplete=new FormulaSession(scheduler);incomplete.UpdateSource("can(x+1");Assert(await incomplete.AnalyzeContentAsync());Assert(incomplete.State.Content!.Regions.Count>0,"No repair region");var region=incomplete.State.Content!.Regions[0];Assert(region.Display==null);var repair=region.Readings!.Candidates.First(c=>c.Kind=="repair");Assert(incomplete.Select(0,repair.Id));Assert(incomplete.State.Content!.Regions[0].Display!.Kind=="repair"&&incomplete.State.Raw=="can(x+1");Assert(incomplete.Undo()&&incomplete.State.Content!.Regions[0].Display==null);});
        await Check("multiline-marker-not-split-or-salvaged",async()=>{using var s=await Session("Đầu lc[x+\n1] cuối.");Assert(s.State.Content!.Regions.Count<=1);Assert(string.Concat(s.State.Content.Blocks().Select(b=>s.State.Raw[b.Start..b.End]))==s.State.Raw);});
        await Check("long-document-bounded-windows",async()=>{string raw=string.Concat(Enumerable.Repeat("Văn bản giữ nguyên, không phải công thức.\n",220))+"lc[x^2]";using var s=await Session(raw);Assert(raw.Length>4096&&s.State.Content!.Regions.Count==1&&s.State.Content.Regions[0].Display!=null);});
        await Check("limits-preserve-all-raw",async()=>{using var s=await Session(new string('a',100001));Assert(s.State.Content!.Regions.Count==0&&s.State.Content.Notices.Count>0&&s.State.Raw.Length==100001);string json=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));Assert(((FormulaDocument)DocumentCodec.Open(json).Document!).State.Raw.Length==100001);});
        await Check("v6-roundtrip-keeps-ids-snapshots-decisions-and-commands",async()=>{using var s=await Session(paragraph);s.KeepContentText(s.State.Content!.Regions[1].Id,true);var doc=new FormulaDocument(Guid.NewGuid(),4,s.State,new());string json=DocumentCodec.Serialize(doc);Assert(JsonNode.Parse(json)!["version"]!.GetValue<int>()==6);var opened=(FormulaDocument)DocumentCodec.Open(json).Document!;Assert(DocumentCodec.Serialize(opened)==json);using var loaded=new FormulaSession(new NeverScheduler());loaded.Load(opened.State);Assert(loaded.State.Content!.Regions[1].KeepText&&loaded.State.Content.Commands.Single().Kind=="keep-text");await File.WriteAllTextAsync(Path.Combine(output,"mixed-document.locus"),json);});
        await Check("v5-rejects-stale-window-and-downlevel-envelope",async()=>{using var s=await Session(paragraph);string json=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),0,s.State,new()));var envelope=JsonNode.Parse(json)!;envelope["version"]=4;Assert(!DocumentCodec.Open(envelope.ToJsonString()).IsSupported);envelope["version"]=5;var payload=JsonNode.Parse(envelope["payload"]!.GetValue<string>())!;payload["content"]!["regions"]![0]!["window"]="stale";envelope["payload"]=payload.ToJsonString();envelope["sha256"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload.ToJsonString())));Assert(!DocumentCodec.Open(envelope.ToJsonString()).IsSupported);});
        await Check("old-snapshots-load-without-parser",()=>{var a=AnalysisWire.Analyze(new("x+1/2",7,new()));var state=new FormulaState("x+1/2",7,new(),a,0,a.Regions[0].Candidates[1].Id);var json=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,state,new()));using var s=new FormulaSession(new NeverScheduler());s.Load(((FormulaDocument)DocumentCodec.Open(json).Document!).State);s.PromoteSnapshot();Assert(s.State.Candidate!.Kind=="repair"&&s.State.Content!.Regions[0].Display!.Kind=="repair");return Task.CompletedTask;});
        await Check("cancel-and-supersede-never-commit-old-paragraph",async()=>{var slow=new GateScheduler();using var s=new FormulaSession(slow);s.Configure(settings);s.UpdateSource("x^2");var task=s.AnalyzeContentAsync();Assert(s.IsBusy);s.UpdateSource("1/2");slow.Complete();Assert(!await task&&s.State.Raw=="1/2"&&s.State.Content==null);var task2=s.AnalyzeContentAsync();s.CancelContentAnalysis();slow.Complete();Assert(!await task2&&!s.IsBusy&&s.State.Content==null);});
        await Check("wrong-window-result-refused",async()=>{using var s=new FormulaSession(new WrongScheduler());s.Configure(settings);s.UpdateSource("x^2");Assert(!await s.AnalyzeContentAsync()&&s.State.Raw=="x^2"&&s.State.Content==null);});
        await Check("partial-kept-text-copy-converts-only-whole-other-regions",async()=>{
            using var s=await Session("lc[x^2] và lc[x^3]");var first=s.State.Content!.Regions[0];s.KeepContentText(first.Id,true);
            var content=s.State.Content!;var copied=content.ResultText(first.Start+3,content.Raw.Length);
            Assert(copied=="x^2] và {x}^{3}",copied);
        });
        await Check("single-sc1-assistance-v5-history-restores-snapshot",async()=>{
            using var s=await Session("hoa-[H2+O2=H2O]");var before=s.State;
            Assert(await s.RequestAssistanceAsync(0));var proposal=s.AssistanceProposals.Single();
            Assert(s.AcceptAssistance(s.LeaseAssistance(proposal.Id),new(s.State.Raw,s.State.Raw.Length,s.State.Raw.Length,false,false,true),out _));
            s.PromoteSnapshot();string json=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));
            var opened=DocumentCodec.Open(json);Assert(opened.Document is FormulaDocument,opened.UnavailableReason??"Missing file");
            using var loaded=new FormulaSession(new NeverScheduler());loaded.Load(((FormulaDocument)opened.Document!).State);
            Assert(loaded.State.Content!.Regions[0].Display!=null&&loaded.State.Transformations!.Entries.Count==1);
            Assert(loaded.RestoreBeforeAssistance(out _)&&loaded.State.Raw==before.Raw&&loaded.State.Content!.Regions[0].Id==before.Content!.Regions[0].Id);
            Assert(loaded.Undo()&&loaded.State.Raw==s.State.Raw);
        });
        await Check("separate-result-override-and-anchored-command-roundtrip",async()=>{
            using var s=await Session("hoa-[H2+O2=H2O]");var content=s.State.Content!;var original=content.Regions.Single();
            var result=AnalysisWire.Analyze(new("hoa-[2H2+O2=2H2O]",0,settings)).Regions.Single();
            var after=original with{ResultOverride=new(original.SelectedId!,result,result.Candidates[0].Id,"balance/fixture")};
            var changed=content.Change(after,"balance");var state=s.State with{Content=changed,CandidateId=after.Display!.Id};
            string json=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),0,state,new()));
            var opened=(FormulaDocument)DocumentCodec.Open(json).Document!;Assert(opened.State.Raw==content.Raw&&opened.State.Candidate!.Id==after.Display.Id);
            var saved=opened.State.Content!;Assert(saved.Commands.Single().Changes.Single().Before.ResultOverride==null&&saved.Regions[0].ResultOverride!=null&&saved.ResultText(0,saved.Raw.Length)!=content.ResultText(0,content.Raw.Length));
            var lower=JsonNode.Parse(json)!;lower["version"]=5;Assert(!DocumentCodec.Open(lower.ToJsonString()).IsSupported);
            using var loaded=new FormulaSession(new NeverScheduler());loaded.Load(opened.State);Assert(loaded.Require(loaded.Lease()).Id==after.Display.Id);
            await File.WriteAllTextAsync(Path.Combine(output,"result-history-fixture.locus"),json);
            var edited=await ContentAnalyzer.AnalyzeAsync(new("Ghi chú. "+content.Raw,2,settings),scheduler,changed,CancellationToken.None);
            Assert(edited.Regions.Single().Id==original.Id&&edited.Regions.Single().ResultOverride!=null);
            var formulaEdit=await ContentAnalyzer.AnalyzeAsync(new("hoa-[H2+Cl2=HCl]",3,settings),scheduler,changed,CancellationToken.None);
            Assert(formulaEdit.Regions.Single().Id!=original.Id&&formulaEdit.Regions.Single().ResultOverride==null);
        });
        await Check("history-rejects-source-anchor-or-reading-mismatch",async()=>{
            using var s=await Session("lc[x^2]");var region=s.State.Content!.Regions.Single();
            bool rejected=false;try{_ = new ContentCommand(Guid.NewGuid(),"keep-text",[new(region,region with{Id=Guid.NewGuid(),KeepText=true})]);}catch(FormatException){rejected=true;}Assert(rejected);
            var result=AnalysisWire.Analyze(new("1/2",0,new())).Regions.Single();
            rejected=false;try{_ = s.State.Content.Replace(region with{ResultOverride=new("other-reading",result,result.Candidates[0].Id,"fixture")});}catch(FormatException){rejected=true;}Assert(rejected);
            s.KeepContentText(region.Id,true);Assert(s.State.Content!.Commands.Count==1);Assert(s.Undo()&&s.State.Content!.Commands.Count==0);Assert(s.Redo()&&s.State.Content!.Commands.Count==1);
        });
        var report=new{status=failed==0?"PASSED":"FAILED",total=results.Count,failed,results};await File.WriteAllTextAsync(Path.Combine(output,"content-tests.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{report.status,report.total,report.failed}));return failed==0?0:1;
    }
    private sealed class NeverScheduler:IAnalysisScheduler{public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest r,CancellationToken t)=>throw new Exception("Unexpected reparse");}
    private sealed class WrongScheduler:IAnalysisScheduler{public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest r,CancellationToken t)=>Task.FromResult(AnalysisWire.Analyze(r with{Raw="1/2"}));}
    private sealed class GateScheduler:IAnalysisScheduler
    {
        private AnalysisRequest? request;private TaskCompletionSource<AnalysisResult>? gate;
        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest r,CancellationToken t){request=r;gate=new();return gate.Task;}
        public void Complete()=>gate!.SetResult(AnalysisWire.Analyze(request!));
    }
}
