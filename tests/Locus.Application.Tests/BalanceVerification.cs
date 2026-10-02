using Locus.Application;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Export;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class BalanceVerification
{
    public static async Task<int> Run(string output)
    {
        Directory.CreateDirectory(output);var checks=new List<object>();int failed=0;
        void Assert(bool value,string reason="Balance contract failed"){if(!value)throw new Exception(reason);}
        async Task Check(string id,Func<Task> action){try{await action();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}}
        var settings=new FormulaSettings(EnabledDomains:DetectionDomains.All,MarkerProfiles:MarkerPreferences.Default);
        async Task<FormulaSession> Ready(string raw,bool auto=false,IAnalysisScheduler? scheduler=null)
        {var s=new FormulaSession(scheduler??new NativeAnalysisScheduler());s.Configure(settings);s.SetAutoBalance(auto);s.UpdateSource(raw);Assert(await s.AnalyzeContentAsync(),s.Error);return s;}
        async Task Select(FormulaSession s,int start=0,int? end=null){s.SetBalanceSelection(s.State.Content!.Select(start,end??s.State.Raw.Length));Assert(await s.RefreshBalanceAsync(),s.BalanceMessage);}
        string Text(FormulaSession s)=>s.State.Content!.ResultText(0,s.State.Raw.Length);
        bool Managed(ContentRegion r)=>r.ResultOverride?.ManagedBalance==true;
        const string mixture="A hoa-[3H2+O2=H2O].\nB hoa-[N2+H2=NH3].\nC hoa-[C+O2=CO2].\nD hoa-[H2=CO2].";
        await Check("BAL-UX-01-domain-species-empty-and-clipped",async()=>{
            foreach(var raw in new[]{"lc[x^2]","hoa-[H2SO4]","hoa-[H2+O2=]"}){using var s=await Ready(raw);await Select(s);Assert(!s.CanBalanceStep);}
            using var valid=await Ready("hoa-[H2+O2=H2O]");await valid.RefreshBalanceAsync();Assert(!valid.CanBalanceStep);valid.SetBalanceSelection(valid.State.Content!.Select(1,valid.State.Raw.Length));await valid.RefreshBalanceAsync();Assert(!valid.CanBalanceStep);
            await Select(valid);Assert(valid.CanBalanceStep&&valid.BalanceAssessments.Single().Status=="Balanced");
        });
        await Check("BAL-UX-02-exact-coefficients-raw-export-undo",async()=>{
            using var s=await Ready("Trước hoa-[3H2+O2=H2O] sau.");string raw=s.State.Raw,before=Text(s);await Select(s);var lease=s.Lease();
            Assert(await s.BalanceStepAsync());Assert(Text(s)!=before&&s.State.Raw==raw&&!s.IsCurrent(lease));var balanced=s.State;
            Assert(s.BalanceStepLabel.StartsWith("Hủy"));Assert(await s.BalanceStepAsync());Assert(Text(s)==before&&s.State.Content!.Regions.Single().KeepFromAuto);
            Assert(s.Undo()&&ReferenceEquals(s.State,balanced));Assert(s.Redo()&&Text(s)==before);
        });
        await Check("BAL-UX-03-reverse-cycle-one-at-a-time",async()=>{
            using var s=await Ready("hoa-[H2+O2=H2O]\nhoa-[N2+H2=NH3]\nhoa-[Fe+O2=Fe2O3]");s.SetBalanceSelection(s.State.Content!.Select(s.State.Raw.Length,0));await s.RefreshBalanceAsync();var ids=s.State.Content.Regions.Select(r=>r.Id).ToArray();
            for(int i=0;i<3;i++){Assert(s.NextBalanceRegion==ids[i],"Wrong balance order "+s.BalanceStepLabel);Assert(await s.BalanceStepAsync());Assert(s.State.Content!.Regions.Count(Managed)==i+1);}
            Assert(s.BalanceStepLabel.Contains("Hủy")&&s.NextBalanceRegion==ids[0]);
            for(int i=0;i<3;i++){Assert(s.NextBalanceRegion==ids[i],"Wrong cancel order");Assert(await s.BalanceStepAsync());Assert(s.State.Content!.Regions.Count(Managed)==2-i);}
            Assert(s.BalanceStepLabel.StartsWith("Cân bằng")&&s.NextBalanceRegion==ids[0]);
        });
        await Check("BAL-UX-04-user-balanced-and-no-solution-remain-exportable",async()=>{
            using var s=await Ready("hoa-[4H2+2O2=4H2O]");string before=Text(s);await Select(s);Assert(!s.CanBalanceStep&&s.BalanceStepLabel=="Đã cân bằng"&&s.BalanceAssessments.Single().Status=="AlreadyBalanced"&&s.CanExport&&Text(s)==before);
            using var invalid=await Ready("hoa-[H2=CO2]");await Select(invalid);Assert(!invalid.CanBalanceStep&&invalid.BalanceAssessments.Single().Status=="NoSolution"&&invalid.CanExport);
        });
        await Check("BAL-UX-05-balance-batch-double-click-restores-mixed",async()=>{
            using var s=await Ready(mixture);var first=s.State.Content!.Regions.First(r=>r.Display?.Document.Domain=="chemistry");await Select(s,first.Start,first.End);Assert(await s.BalanceStepAsync(),"initial step failed: "+s.BalanceMessage);Assert(s.State.Content!.Regions.Count(Managed)==1,"initial step count");await Select(s);string before=Text(s);var old=s.State;
            Assert(await s.BalanceBatchAsync(false));Assert(s.State.Content!.Regions.Count(Managed)==2&&s.CanQuickUndoBatch(false));
            var all=s.State;Assert(await s.BalanceBatchAsync(false),"quick undo failed");Assert(Text(s)==before&&s.State.Content!.Regions.Count(Managed)==1,"mixed restore mismatch: "+Text(s));Assert(s.Undo()&&ReferenceEquals(s.State,all),"first undo mismatch");Assert(s.Undo()&&ReferenceEquals(s.State,old),"second undo mismatch");
        });
        await Check("BAL-UX-06-cancel-batch-double-click-restores-flags",async()=>{
            using var s=await Ready(mixture);await Select(s);await s.BalanceBatchAsync(false);var balanced=s.State;
            Assert(await s.BalanceBatchAsync(true));Assert(s.State.Content!.Regions.Count(Managed)==0&&s.State.Content.Regions.Count(r=>r.KeepFromAuto)==2);
            Assert(s.CanQuickUndoBatch(true)&&!s.CanQuickUndoBatch(false));Assert(await s.BalanceBatchAsync(true));Assert(Text(s)==balanced.Content!.ResultText(0,balanced.Raw.Length)&&s.State.Content!.Regions.All(r=>!r.KeepFromAuto));
        });
        await Check("BAL-UX-07-quick-undo-invalidated-by-selection-source-and-undo",async()=>{
            using var s=await Ready(mixture);await Select(s);await s.BalanceBatchAsync(false);Assert(s.CanQuickUndoBatch(false));s.SetBalanceSelection(null);Assert(!s.CanQuickUndoBatch(false));
            await Select(s);await s.BalanceBatchAsync(true);Assert(s.CanQuickUndoBatch(true));s.Undo();Assert(!s.CanQuickUndoBatch(true));
            s.UpdateSource(s.State.Raw+"\nSửa");Assert(!s.CanQuickUndoBatch(false)&&!s.CanQuickUndoBatch(true));
        });
        await Check("BAL-UX-08-cancel-batch-atomic",async()=>{
            using var s=await Ready(mixture);await Select(s);var before=s.State;var pending=s.BalanceBatchAsync(false);s.CancelBalance();Assert(!await pending&&ReferenceEquals(s.State,before));
            await Select(s);pending=s.BalanceBatchAsync(false);s.UpdateSource("hoa-[C+O2=CO2]");Assert(!await pending&&s.State.Raw=="hoa-[C+O2=CO2]"&&s.State.Content==null);
        });
        await Check("BAL-UX-08-stale-worker-and-exception-no-commit",async()=>{
            var gate=new BalanceGate();using var s=await Ready("hoa-[H2+O2=H2O]",scheduler:gate);s.SetBalanceSelection(s.State.Content!.Select(0,s.State.Raw.Length));var work=s.RefreshBalanceAsync();var before=s.State;s.SetBalanceSelection(null);gate.Complete();Assert(!await work&&ReferenceEquals(before,s.State));
            s.SetBalanceSelection(s.State.Content!.Select(0,s.State.Raw.Length));work=s.RefreshBalanceAsync();gate.Fail();Assert(!await work&&!s.CanBalanceStep&&ReferenceEquals(before,s.State));
        });
        await Check("BAL-UX-09-auto-opt-in-paste-one-undo",async()=>{
            using var off=await Ready(mixture);Assert(!await off.ApplyAutoBalanceAsync()&&off.State.Content!.Regions.All(r=>!Managed(r)));off.SetAutoBalance(true);Assert(!await off.ApplyAutoBalanceAsync());
            using var on=await Ready(mixture,true);Assert(await on.ApplyAutoBalanceAsync());Assert(on.State.Content!.Regions.Count(Managed)==2&&on.State.Raw==mixture);Assert(on.Undo()&&on.State.Raw=="");Assert(on.Redo()&&on.State.Raw==mixture&&on.State.Content!.Regions.Count(Managed)==2);
        });
        await Check("BAL-UX-09-auto-disabled-while-waiting",async()=>{
            var gate=new BalanceGate();using var s=await Ready("hoa-[H2+O2=H2O]",true,gate);var before=s.State;var work=s.ApplyAutoBalanceAsync();s.SetAutoBalance(false);gate.Complete();Assert(!await work&&ReferenceEquals(before,s.State));
        });
        await Check("BAL-UX-09-reopen-history-and-detach-do-not-replay-pending-auto",async()=>{
            foreach(var action in new[]{"load","history","detach"})
            {
                using var s=await Ready("hoa-[H2+O2=H2O]",true);var snapshot=s.State;
                if(action=="load")s.Load(snapshot);
                else if(action=="history"){Assert(s.Undo());Assert(s.Redo());}
                else s.DetachView();
                Assert(!await s.ApplyAutoBalanceAsync()&&ReferenceEquals(s.State,snapshot),action);
            }
        });
        await Check("BAL-UX-10-cancel-ignore-reload-neighbor-edit",async()=>{
            using var s=await Ready("hoa-[H2+O2=H2O]",true);await s.ApplyAutoBalanceAsync();await Select(s);await s.BalanceStepAsync();string before=Text(s);var id=s.State.Content!.Regions[0].Id;
            s.UpdateSource("Lời dẫn. "+s.State.Raw);await s.AnalyzeContentAsync();Assert(!await s.ApplyAutoBalanceAsync()&&s.State.Content!.Regions.Single().Id==id&&s.State.Content.Regions.Single().KeepFromAuto);
            var file=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));using var loaded=new FormulaSession(new NativeAnalysisScheduler());loaded.SetAutoBalance(true);loaded.Load(((FormulaDocument)DocumentCodec.Open(file).Document!).State);Assert(!await loaded.ApplyAutoBalanceAsync()&&loaded.State.Content!.Regions[0].KeepFromAuto);
            await Select(loaded);await loaded.BalanceBatchAsync(false);Assert(!loaded.State.Content!.Regions[0].KeepFromAuto);await loaded.BalanceBatchAsync(false);Assert(loaded.State.Content!.Regions[0].KeepFromAuto);
        });
        await Check("BAL-UX-10-edit-formula-resets-ignore",async()=>{
            using var s=await Ready("hoa-[H2+O2=H2O]",true);await s.ApplyAutoBalanceAsync();await Select(s);await s.BalanceStepAsync();s.UpdateSource("hoa-[3H2+O2=H2O]");await s.AnalyzeContentAsync();Assert(await s.ApplyAutoBalanceAsync()&&s.State.Content!.Regions.Single().ResultOverride!.ManagedBalance);
        });
        await Check("BAL-UX-11-products-cancel-keeps-products-drop-restores-draft",async()=>{
            using var s=await Ready("hoa-[3H2+O2=]");Assert(await s.RequestAssistanceAsync(0,[new("activation","ignition")]));var p=s.AssistanceProposals.Single();var before=s.State;
            Assert(!s.TryPrepareContentAssistance(s.LeaseAssistance(p.Id),new(s.State.Raw,-1,s.State.Raw.Length,false,false,true),out _)&&ReferenceEquals(before,s.State));
            Assert(s.TryPrepareContentAssistance(s.LeaseAssistance(p.Id),new(s.State.Raw,s.State.Raw.Length,s.State.Raw.Length,false,false,true),out var plan));Assert(s.CommitAssistance(plan!));
            Assert(s.State.Raw==before.Raw&&s.State.Content!.Regions.Single().ResultOverride!.ManagedBalance);await Select(s);Assert(await s.BalanceStepAsync());
            var r=s.State.Content!.Regions.Single();Assert(r.ResultOverride!.ProductProposal!=null&&!r.ResultOverride.ManagedBalance&&r.Display!.Source.Raw=="3H2+O2->H2O",r.Display?.Source.Raw??"null");
            var file=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));Assert(JsonNode.Parse(file)!["version"]!.GetValue<int>()==7);var opened=(FormulaDocument)DocumentCodec.Open(file).Document!;s.Load(opened.State);
            Assert(s.DropContentProducts(r.Id)&&s.State.Raw==before.Raw&&s.State.Content!.Regions.Single().Display==null);Assert(s.Undo()&&s.State.Content!.Regions.Single().ResultOverride!.ProductProposal!=null);
            await File.WriteAllTextAsync(Path.Combine(output,"products-history.locus"),file);
        });
        await Check("BAL-UX-11-ghost-space-source-and-undo",async()=>{
            using var s=await Ready("hoa-[H2+O2=]");await s.RequestAssistanceAsync(0,[new("activation","ignition")]);var p=s.AssistanceProposals.Single();var before=s.State;
            Assert(s.TryPrepareContentAssistance(s.LeaseAssistance(p.Id),new(s.State.Raw,s.State.Raw.Length,s.State.Raw.Length,false,false,true),out var plan,true));Assert(s.CommitAssistance(plan!));
            Assert(s.State.Raw==before.Raw+" "&&s.State.Content!.Regions.Single().Display!=null);Assert(DocumentCodec.Open(DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()))).IsSupported);
            Assert(s.Undo()&&ReferenceEquals(s.State,before));
        });
        await Check("BAL-UX-12-v7-balance-persistence-and-downgrade",async()=>{
            using var s=await Ready(mixture);await Select(s);await s.BalanceBatchAsync(false);string json=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));Assert(JsonNode.Parse(json)!["version"]!.GetValue<int>()==7);
            var opened=(FormulaDocument)DocumentCodec.Open(json).Document!;Assert(DocumentCodec.Serialize(opened)==json);using var loaded=new FormulaSession(new NativeAnalysisScheduler());loaded.Load(opened.State);await Select(loaded);Assert(loaded.BalanceAssessments.Count(a=>a.Status=="cancel")==2);Assert(await loaded.BalanceBatchAsync(true));
            var lower=JsonNode.Parse(json)!;lower["version"]=6;Assert(!DocumentCodec.Open(lower.ToJsonString()).IsSupported);await File.WriteAllTextAsync(Path.Combine(output,"mixed-balanced.locus"),json);
        });
        await Check("wire-native-worker-operation-parity",async()=>{
            using var s=await Ready("hoa-[H2+O2=H2O]");var r=s.State.Content!.Regions[0];var input=new ContentBalanceRequest(Locus.Core.Serialization.CandidateSetSerializer.Serialize(r.Readings!),r.Display!.Id);var native=ContentBalanceWire.Analyze(input);var worker=ContentBalanceWire.Deserialize(AnalysisWire.Run(ContentBalanceWire.Request(input)));Assert(native==worker);
        });
        await Check("wire-rejects-undefined-and-numeric-status",()=>{
            foreach(var status in new[]{"999","0","balanced","cancel"})
            {bool rejected=false;try{ContentBalanceWire.Deserialize(JsonSerializer.Serialize(new ContentBalanceResponse("candidate",status,"message",null)));}catch(FormatException){rejected=true;}Assert(rejected,status);}
            return Task.CompletedTask;
        });
        await Check("preferences-default-off-and-version-refusal",()=>{
            Assert(EditorPreferences.Read(new EditorPreferences(4,settings,new(),true,false).Serialize())!.AutoBalance==null);
            var p=new EditorPreferences(5,settings,new(),true,false,true);Assert(EditorPreferences.Read(p.Serialize())!.AutoBalance==true);bool rejected=false;try{(p with{Version=4}).Serialize();}catch(FormatException){rejected=true;}Assert(rejected);return Task.CompletedTask;
        });
        var report=new{status=failed==0?"PASSED":"FAILED",total=checks.Count,failed,checks};await File.WriteAllTextAsync(Path.Combine(output,"balance-tests.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{report.status,report.total,report.failed}));return failed==0?0:1;
    }
    private sealed class BalanceGate:IAnalysisScheduler,IContentBalanceScheduler
    {
        private ContentBalanceRequest? request;private TaskCompletionSource<ContentBalanceResponse>? completion;
        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request,CancellationToken token)=>Task.FromResult(AnalysisWire.Analyze(request));
        public Task<ContentBalanceResponse> BalanceAsync(ContentBalanceRequest input,CancellationToken token){request=input;completion=new();return completion.Task;}
        public void Complete()=>completion!.SetResult(ContentBalanceWire.Analyze(request!));
        public void Fail()=>completion!.SetException(new IOException("worker failed"));
    }
}
