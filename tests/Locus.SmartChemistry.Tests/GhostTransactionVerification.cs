using System.Text.Json;
using Locus.Application;
using Locus.Core.Assistance;
using Locus.Core.Detection;

internal static class GhostTransactionVerification
{
    internal static async Task<int> Run(string output)
    {
        var checks=new List<object>();int failed=0;
        void Require(bool value,string reason="Ghost transaction contract failed"){if(!value)throw new Exception(reason);}
        async Task Check(string id,Func<Task> test){try{await test();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}}
        async Task<FormulaSession> Ready(){var s=new FormulaSession(new NativeAnalysisScheduler());s.Configure(new(InputMode.Explicit,EnabledDomains:DetectionDomains.None,MarkerProfiles:MarkerPreferences.Default));s.UpdateSource("hoa-[h2+o2=");await s.AnalyzeAsync();await s.RequestAssistanceAsync(0,[new("activation","ignition")]);return s;}
        EditorInputStamp Stamp(FormulaSession s)=>new(s.State.Raw,s.State.Raw.Length,s.State.Raw.Length,false,false,true);
        await Check("prepare-is-pure-and-commit-exactly-once",async()=>{
            using var s=await Ready();var before=s.State;var version=s.Version;var p=s.AssistanceProposals.Single();
            Require(s.TryPrepareAssistance(s.LeaseAssistance(p.Id),Stamp(s),out var plan));
            Require(ReferenceEquals(s.State,before)&&s.Version==version&&s.State.Transformations==null&&s.AssistanceProposals.Single().Id==p.Id);
            Require(plan!.After.Raw=="hoa-[2H2+O2->2H2O"&&plan.Caret.Start==plan.After.Raw.Length);
            Require(s.CommitAssistance(plan));var after=s.State;Require(!s.CommitAssistance(plan)&&ReferenceEquals(s.State,after));
            Require(s.Undo()&&ReferenceEquals(s.State,before)&&s.Redo()&&ReferenceEquals(s.State,after));
        });
        foreach(string mutation in new[]{"source","settings","conditions","composition","dismiss","other-session"})await Check("prepare-stale/"+mutation,async()=>{
            using var s=await Ready();var p=s.AssistanceProposals.Single();Require(s.TryPrepareAssistance(s.LeaseAssistance(p.Id),Stamp(s),out var plan));
            if(mutation=="source")s.UpdateSource("hoa-[HCl+NaOH=");
            if(mutation=="settings")s.Configure(s.State.Settings with{EnabledDomains=DetectionDomains.All});
            if(mutation=="conditions")await s.RequestAssistanceAsync(0,[new("activation","none")]);
            if(mutation=="composition")s.UpdateSource(s.State.Raw,true);
            if(mutation=="dismiss")s.DismissAssistance();
            if(mutation=="other-session"){using var other=await Ready();var saved=other.State;Require(!other.CommitAssistance(plan!)&&ReferenceEquals(saved,other.State));return;}
            var before=s.State;Require(!s.CommitAssistance(plan!)&&ReferenceEquals(s.State,before)&&s.State.Transformations==null);
        });
        await Check("space-one-transaction-and-preferences-migration",async()=>{
            using var s=await Ready();var before=s.State;Require(s.TryPrepareAssistance(s.LeaseAssistance(s.AssistanceProposals.Single().Id),Stamp(s),out var plan,true));
            Require(plan!.After.Raw=="hoa-[2H2+O2->2H2O ");Require(s.CommitAssistance(plan)&&s.Undo()&&ReferenceEquals(before,s.State));
            foreach(int version in new[]{1,2,3})
            {
                var old=new EditorPreferences(version,new(),new(),true);Require(EditorPreferences.Read(old.Serialize())?.AcceptChemistrySpace==null);
            }
            var fresh=new EditorPreferences(4,s.State.Settings,new(),true,true);Require(EditorPreferences.Read(fresh.Serialize())?.AcceptChemistrySpace==true);
            Require(EditorPreferences.Read(fresh.Serialize().Replace("\"version\":4","\"version\":3"))==null);
            Require(EditorPreferences.Read(fresh.Serialize().Replace("\"version\":4","\"version\":99"))==null);
        });
        File.WriteAllText(Path.Combine(output,"ghost-transaction-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,scope="Two-phase application acceptance and preferences; UI keys and OS IME tested separately",summary=new{checks=checks.Count,passed=checks.Count-failed,failed},checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SC1 ghost transactions: {checks.Count-failed}/{checks.Count}");return failed;
    }
}
