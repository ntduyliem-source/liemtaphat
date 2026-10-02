using System.Text.Json;
using Locus.Application;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Export;

internal static class AssistanceSessionVerification
{
    internal static async Task<int> Run(string output)
    {
        var checks=new List<object>();int failed=0;
        void Require(bool ok,string message="Contract failed"){if(!ok)throw new Exception(message);}
        async Task Check(string id,Func<Task> action){try{await action();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}}
        FormulaSettings Settings()=>new(InputMode.Markers,EnabledDomains:DetectionDomains.None,MarkerProfiles:MarkerPreferences.Default);
        async Task<FormulaSession> Ready(string raw="hoa-[h2+o2=h2o]")
        {var s=new FormulaSession(new NativeAnalysisScheduler());s.Configure(Settings());s.UpdateSource(raw);Require(await s.AnalyzeAsync());Require(await s.RequestAssistanceAsync(raw.IndexOf("hoa-[",StringComparison.Ordinal)));return s;}
        EditorInputStamp Stamp(FormulaSession s)=>new(s.State.Raw,s.State.Raw.Length,s.State.Raw.Length,false,false,true);
        await Check("accept-one-undo-same-preview-and-v4",async()=>{
            using var s=await Ready();var before=s.State;var p=s.AssistanceProposals.Single();var preview=CandidateExporter.ToMathMl(p.Result.Candidates[0]);var lease=s.LeaseAssistance(p.Id);
            Require(s.AcceptAssistance(lease,Stamp(s),out var caret));Require(s.State.Raw=="hoa-[2H2+O2->2H2O]"&&caret.Start==s.State.Raw.Length&&caret.End==caret.Start);
            Require(CandidateExporter.ToMathMl(s.State.Candidate!)==preview&&s.State.Transformations!.Entries.Count==1);
            var accepted=s.State;Require(!s.AcceptAssistance(lease,Stamp(s),out _));Require(s.Undo()&&ReferenceEquals(s.State,before)&&s.HistorySelection?.Start==before.Raw.Length);Require(s.Redo()&&ReferenceEquals(s.State,accepted)&&s.HistorySelection==caret);
            var file=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));Require(DocumentCodec.Open(file).Document is FormulaDocument doc&&doc.State.Transformations!.Entries[0].Proposal.Id==p.Id);File.WriteAllText(Path.Combine(output,"session-accepted-balance.locus"),file);
        });
        await Check("multiple-regions-preserve-other-choices-and-spans",async()=>{
            using var s=await Ready("👩‍🏫 toan-[x+1/2] trước hoa-[h2+o2=h2o] sau ly-[v_0]");var before=s.State;
            var untouched=before.Analysis!.Regions.Where(r=>r.Intent!.Domain!="chemistry").Select(r=>r.Candidates.Select(CandidateExporter.ToMathMl).ToArray()).ToArray();
            var p=s.AssistanceProposals.Single();Require(s.AcceptAssistance(s.LeaseAssistance(p.Id),Stamp(s),out _));Require(s.State.Analysis!.Regions.Count==3&&s.State.Region!.Intent!.Domain=="chemistry");
            int i=0;foreach(var r in s.State.Analysis.Regions.Where(r=>r.Intent!.Domain!="chemistry"))Require(r.Candidates.Select(CandidateExporter.ToMathMl).SequenceEqual(untouched[i++]));
            Require(s.State.Raw=="👩‍🏫 toan-[x+1/2] trước hoa-[2H2+O2->2H2O] sau ly-[v_0]");Require(s.Undo()&&ReferenceEquals(s.State,before));
        });
        await Check("pending-preview-does-not-enter-source-or-file",async()=>{
            using var s=await Ready();var raw=s.State.Raw;var file=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));
            Require(!file.Contains("transformations",StringComparison.Ordinal)&&s.State.Transformations==null&&s.State.Raw==raw&&s.State.Candidate!.Source.Raw==raw);
            s.DismissAssistance();Require(s.Assistance==null&&s.State.Raw==raw&&s.CanExport);
        });
        await Check("reject-stale-input-configuration-ime-focus",async()=>{
            foreach(string mutation in new[]{"raw","pending","composing","focus","settings","source","dismiss"})
            {
                using var s=await Ready();var p=s.AssistanceProposals.Single();var lease=s.LeaseAssistance(p.Id);var stamp=Stamp(s);
                if(mutation=="raw")stamp=stamp with{Raw="changed"};if(mutation=="pending")stamp=stamp with{Pending=true};if(mutation=="composing")stamp=stamp with{Composing=true};if(mutation=="focus")stamp=stamp with{Focused=false};
                if(mutation=="settings")s.Configure(s.State.Settings with{EnabledDomains=DetectionDomains.All});if(mutation=="source")s.UpdateSource("toan-[x^2]");if(mutation=="dismiss")s.DismissAssistance();
                var before=s.State;Require(!s.AcceptAssistance(lease,stamp,out _)&&ReferenceEquals(before,s.State),mutation);
            }
        });
        await Check("restore-source-is-undoable-after-edit",async()=>{
            using var s=await Ready();var before=s.State;var p=s.AssistanceProposals.Single();Require(s.AcceptAssistance(s.LeaseAssistance(p.Id),Stamp(s),out _));
            s.UpdateSource(s.State.Raw+" ghi chú");var edited=s.State;
            Require(s.RestoreBeforeAssistance(out var selection)&&s.State.Raw==before.Raw&&s.State.CandidateId==before.CandidateId&&s.State.Transformations==null&&selection.Start==before.Raw.Length);
            Require(s.Undo()&&ReferenceEquals(s.State,edited));
        });
        await Check("space-transaction-keeps-exactly-one-added-space",async()=>{
            using var s=await Ready();var before=s.State;var p=s.AssistanceProposals.Single();Require(s.AcceptAssistance(s.LeaseAssistance(p.Id),Stamp(s),out var caret,true));
            Require(s.State.Raw=="hoa-[2H2+O2->2H2O] "&&caret.Start==s.State.Raw.Length);Require(s.Undo()&&ReferenceEquals(s.State,before));
        });
        await Check("late-response-after-source-and-condition-change",async()=>{
            var held=new HeldAssistanceScheduler();using var s=new FormulaSession(held);s.Configure(Settings());s.UpdateSource("hoa-[H2+O2->H2O]");await s.AnalyzeAsync();
            var first=s.RequestAssistanceAsync(0);var second=s.RequestAssistanceAsync(0,new[]{new AssistanceCondition("medium","aqueous")});
            held.Complete(0);Require(!await first&&s.IsAssisting&&s.Assistance==null);held.Complete(1);Require(await second&&s.AssistanceProposals.Single().Context.Conditions.Single().Value=="aqueous");
            var third=s.RequestAssistanceAsync(0);s.UpdateSource("hoa-[H2O]");held.Complete(2);Require(!await third&&s.Assistance==null&&s.State.Raw=="hoa-[H2O]");
        });
        await File.WriteAllTextAsync(Path.Combine(output,"assistance-session-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,scope="Session transactions and manual assistance; keyboard ghost/OS IME not asserted",summary=new{checks=checks.Count,passed=checks.Count-failed,failed},checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SC1 assistance sessions: {checks.Count-failed}/{checks.Count}");return failed;
    }
    private sealed class HeldAssistanceScheduler : IAnalysisScheduler,IChemistryAssistanceScheduler
    {
        private readonly List<(ChemistryAssistanceRequest request,TaskCompletionSource<ChemistryAssistanceResponse> response)> pending=[];
        public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request,CancellationToken token)=>Task.FromResult(AnalysisWire.Analyze(request,token));
        public Task<ChemistryAssistanceResponse> AssistAsync(ChemistryAssistanceRequest request,CancellationToken token){var response=new TaskCompletionSource<ChemistryAssistanceResponse>();pending.Add((request,response));return response.Task;}
        public void Complete(int index)=>pending[index].response.SetResult(ChemistryAssistanceWire.Analyze(pending[index].request));
    }
}
