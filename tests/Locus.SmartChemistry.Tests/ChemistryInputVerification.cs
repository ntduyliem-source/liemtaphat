using System.Text.Json;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Parsing;
using Locus.Core.Serialization;
using Locus.Application;

internal static class ChemistryInputVerification
{
    internal static int Run(string root,string output)
    {
        var checks=new List<object>();var fixtures=new List<object>();int failed=0;
        void Require(bool ok,string message="Contract failed"){if(!ok)throw new Exception(message);}
        void Check(string id,Action action){try{action();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}}
        using var corpus=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"corpus/sc1/chemistry-input.json")));
        foreach(var item in corpus.RootElement.EnumerateArray())
        {
            string id=item.GetProperty("id").GetString()!,raw=item.GetProperty("raw").GetString()!;
            var settings=new FormulaSettings(item.TryGetProperty("mode",out var mode)?Enum.Parse<InputMode>(mode.GetString()!):InputMode.Explicit,
                EnabledDomains:(DetectionDomains)item.GetProperty("flags").GetInt32(),MarkerProfiles:MarkerPreferences.Default);
            Check(id,()=>{
                var request=new AnalysisRequest(raw,101,settings);var result=AnalysisWire.Analyze(request);
                if(item.TryGetProperty("error",out var error))Require(result.Regions.Count==0&&result.Diagnostics.Any(d=>d.Code==error.GetString()),"Expected "+error+": "+string.Join(',',result.Diagnostics.Select(d=>d.Code)));
                else
                {
                    Require(result.Regions.Count==1,"Expected one region");var set=result.Regions[0];var candidate=set.Candidates[0];
                    Require(candidate.Document.Domain==item.GetProperty("domain").GetString(),"Wrong domain");
                    if(item.TryGetProperty("linear",out var linear))Require(ChemistryProjection.Create(candidate.Document.Root).Source.Raw==linear.GetString(),"Wrong chemistry interpretation");
                    if(item.TryGetProperty("grammar",out var grammar))Require(candidate.GrammarVersion==grammar.GetString(),"Wrong grammar version");
                    if(item.TryGetProperty("warning",out var warning))Require(candidate.Diagnostics.Any(d=>d.Code==warning.GetString())&&set.ContentEligibility=="blocked","Missing warning gate");
                    var snapshot=CandidateSetSerializer.Serialize(set);Require(CandidateSetSerializer.Serialize(CandidateSetSerializer.Deserialize(snapshot))==snapshot,"Snapshot meaning changed");
                    var state=new FormulaState(raw,101,settings,result,0,candidate.Id);Require(DocumentCodec.Open(DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,state,new()))).IsSupported,"File roundtrip failed");
                }
                fixtures.Add(new{id,request=AnalysisWire.Request(request),result=AnalysisWire.Serialize(result)});
            });
        }
        foreach(var (body,expected) in new[]{("h2+o2=","H2+O2"),("h2+o2-> ","H2+O2"),("HCl+NaOH=","HCl+NaOH"),("h2+i2<->","H2+I2"),("h2+o2+=",(string?)null),("h2=o2=",null),("co+o2=",null),("=",null),("h2+o2=h2o",null)})
            Check("draft/"+body,()=>{
                var source=new SourceSnapshot("hoa-["+body,200);var region=new AssistanceRegion(source,new(5,source.Raw.Length),new(0,source.Raw.Length),new("hoa-[","]"),new("chemistry","chemistry"),false);
                var result=ReactionDraftParser.Parse(region);Require((result.Draft!=null)==(expected!=null),"Draft availability");
                if(result.Draft!=null){Require(ChemistryProjection.Create(result.Draft.Reactants.Candidates[0].Document.Root).Source.Raw==expected);var json=AssistanceSerializer.SerializeDraft(result.Draft);Require(AssistanceSerializer.DeserializeDraft(json).Id==result.Draft.Id);}
            });
        Check("case-budget-and-cancellation",()=>{
            var source=new SourceSnapshot(new string('c',65));var result=new ChemistryParser(true).Parse(source,new(0,65));Require(result.Diagnostics.Any(d=>d.Code=="CHEMISTRY_CASE_BUDGET"));
            using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{new ChemistryParser(true).Parse(new SourceSnapshot("h2o"),new(0,3),cancellationToken:cancel.Token);}catch(OperationCanceledException){cancelled=true;}Require(cancelled);
        });
        File.WriteAllText(Path.Combine(output,"chemistry-input-fixtures.json"),JsonSerializer.Serialize(fixtures));
        File.WriteAllText(Path.Combine(output,"chemistry-input-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,summary=new{checks=checks.Count,passed=checks.Count-failed,failed},checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SC1 chemistry input: {checks.Count-failed}/{checks.Count}");return failed;
    }
}
