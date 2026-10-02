using System.Text.Json;
using Locus.Application;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;

internal static class CatalogVerification
{
    internal static async Task<int> Run(string root,string output)
    {
        int failed=0;var checks=new List<object>();var fixtures=new List<object>();
        var settings=new FormulaSettings(InputMode.Explicit,"lc[","]",DetectionDomains.None,MarkerPreferences.Default);
        void Require(bool ok,string message="Catalog contract failed"){if(!ok)throw new Exception(message);}
        async Task Check(string id,Func<Task> action){try{await action();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}}
        ChemistryAssistanceResponse Run(string raw,IEnumerable<AssistanceCondition>? conditions=null)
            =>ChemistryAssistanceWire.Analyze(new(new(raw,12,settings),0,conditions?.ToArray()??[]));
        foreach(var rule in ReactionCatalog.Rules)
        {
            await Check("manifest/"+rule.Id,()=>{
                var conditions=rule.Conditions.Select(c=>new AssistanceCondition(c.Key,c.Value)).ToArray();
                string raw="hoa-["+rule.PositiveExample+"]";var result=Run(raw,conditions);
                Require(result.Status==(rule.IsNoReaction?"no-reaction":"available"),rule.Id+": "+result.Status);
                if(!rule.IsNoReaction)
                {
                    var p=AssistanceSerializer.Deserialize(result.Proposals.Single());Require(p.Provenance.RuleId==rule.Id);
                    Require(ReactionBalancer.VerifyConservation(p.Result.Candidates.Single().Document.Root));
                    Require(AssistanceSerializer.Serialize(AssistanceSerializer.Deserialize(result.Proposals[0]))==result.Proposals[0]);
                    Require(p.Region.Source.Raw==raw&&p.Region.Source.Revision==12&&p.Kind=="complete-reaction");
                }
                foreach(var omitted in conditions)
                {
                    var missing=Run(raw,conditions.Where(c=>c.Key!=omitted.Key));
                    Require(missing.Status=="needs-conditions"&&missing.Proposals.Length==0,"Missing condition was inferred: "+omitted.Key);
                }
                Require(rule.References.Count>0&&rule.Scope.Length>0&&rule.Exclusions.Length>0&&rule.NegativeExample.Length>0&&rule.EvidenceKind.Length>0);
                var request=ChemistryAssistanceWire.Request(new(new(raw,12,settings),0,conditions));var wire=AnalysisWire.Run(request);
                Require(ChemistryAssistanceWire.Deserialize(wire).Status==result.Status);
                fixtures.Add(new{id="catalog/"+rule.Id,request,result=wire});return Task.CompletedTask;
            });
        }
        using var heldout=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"corpus/sc1/reactions-heldout.json")));
        foreach(var item in heldout.RootElement.EnumerateArray())
        {
            await Check("heldout/"+item.GetProperty("id").GetString(),()=>{
                string raw=item.GetProperty("raw").GetString()!;var conditions=item.GetProperty("conditions").EnumerateObject().Select(p=>new AssistanceCondition(p.Name,p.Value.GetString()!)).ToArray();
                var result=Run(raw,conditions);Require(result.Status==item.GetProperty("status").GetString(),"Expected "+item.GetProperty("status")+" got "+result.Status);
                if(item.TryGetProperty("result",out var expected))Require(AssistanceSerializer.Deserialize(result.Proposals.Single()).ReplacementText==expected.GetString(),"Wrong whole equation");
                else Require(result.Proposals.Length==0,"An uncertain state supplied a proposal");
                var request=ChemistryAssistanceWire.Request(new(new(raw,12,settings),0,conditions));var wire=AnalysisWire.Run(request);
                Require(ChemistryAssistanceWire.Deserialize(wire).Status==result.Status);
                fixtures.Add(new{id="catalog/heldout/"+item.GetProperty("id").GetString(),request,result=wire});return Task.CompletedTask;
            });
        }
        await Check("catalog/cancellation",()=>{using var stop=new CancellationTokenSource();stop.Cancel();try{ChemistryAssistanceWire.Analyze(new(new("hoa-[H2+O2=]",1,settings),0,[]),stop.Token);throw new Exception("Not canceled");}catch(OperationCanceledException){}return Task.CompletedTask;});
        await Check("catalog/metadata-tamper",()=>{
            string valid=JsonSerializer.Serialize(Run("hoa-[H2+O2=]",[new("activation","ignition")]));
            foreach(string bad in new[]{valid.Replace("s-hydrogen-oxygen","unknown-record"),valid.Replace("ignition","forged"),valid.Replace("openstax.org","example.org")})
            {try{ChemistryAssistanceWire.Deserialize(bad);throw new Exception("Metadata tamper accepted");}catch(FormatException){}}
            return Task.CompletedTask;
        });
        foreach(bool closed in new[]{false,true})await Check("catalog/accept-history-"+(closed?"closed":"open"),async()=>{
            using var session=new FormulaSession(new NativeAnalysisScheduler());session.Configure(settings);
            string raw="hoa-[h2+o2="+(closed?"]":"");session.UpdateSource(raw);await session.AnalyzeAsync();
            await session.RequestAssistanceAsync(0,[new("activation","ignition")]);var p=session.AssistanceProposals.Single();
            var original=session.State;Require(session.AcceptAssistance(session.LeaseAssistance(p.Id),new(raw,raw.Length,raw.Length,false,false,true),out var caret));
            Require(session.State.Raw=="hoa-[2H2+O2->2H2O"+(closed?"]":""));Require(caret.Start==session.State.Raw.Length);
            Require(session.State.Transformations!.Entries.Single().Proposal.Provenance.RuleId=="s-hydrogen-oxygen");
            var json=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,session.State,new()));var loaded=DocumentCodec.Open(json).Document as FormulaDocument;
            Require(loaded?.State.Transformations!.Entries.Single().Proposal.Context.Conditions.Single().Value=="ignition");
            Require(session.Undo()&&session.State==original);Require(session.Redo()&&session.State.Raw==loaded!.State.Raw);
            File.WriteAllText(Path.Combine(output,"accepted-products-"+(closed?"closed":"open")+".locus"),json);
        });
        File.WriteAllText(Path.Combine(output,"catalog-manifest.json"),JsonSerializer.Serialize(new{version=AssistanceVersions.Catalog,records=ReactionCatalog.Rules},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"catalog-fixtures.json"),JsonSerializer.Serialize(fixtures));
        File.WriteAllText(Path.Combine(output,"catalog-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,scope="45 authored whitelist records, held-apart input/condition variants and refusal cases; not accuracy on arbitrary chemistry",summary=new{checks=checks.Count,passed=checks.Count-failed,failed},checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SC1 catalog: {checks.Count-failed}/{checks.Count}; native catalog fixtures: {fixtures.Count}");return failed;
    }
}
