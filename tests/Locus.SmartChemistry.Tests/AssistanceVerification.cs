using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Parsing;
using Locus.Core.Serialization;
using Locus.Application;

internal static class AssistanceVerification
{
    internal static async Task<int> Run(string output)
    {
        var checks=new List<object>();int failed=0;
        void Check(string id,Action action){try{action();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}}
        void Require(bool ok,string message="Contract failed"){if(!ok)throw new Exception(message);}
        void Reject(Action action){try{action();}catch(Exception e)when(e is ArgumentException or FormatException){return;}throw new Exception("Invalid data accepted");}
        CandidateSet Parse(string raw){var s=new SourceSnapshot(raw,9);return new ChemistryParser().Parse(s,new(0,raw.Length));}
        var settings=new FormulaSettings(InputMode.Markers,EnabledDomains:DetectionDomains.None,MarkerProfiles:MarkerPreferences.Default);
        var raw="👩‍🏫 nguồn hoa-[H2+O2->H2O] hết";var analysis=AnalysisWire.Analyze(new(raw,21,settings));var reading=analysis.Regions.Single();
        var region=new AssistanceRegion(analysis.Source,reading.ContentSpan,reading.ReplacementSpan,reading.Markers,reading.Intent);
        var result=ChemistryProjection.Create(Parse("2H2+O2->2H2O").Candidates[0].Document.Root);
        var context=AssistanceHistory.Context(settings);
        var proposal=new AssistanceProposal("balance",region,context,result,new(AssistanceVersions.Solver),reading);
        var before=new FormulaState(raw,21,settings,analysis,0,reading.Candidates[0].Id);
        var expected="👩‍🏫 nguồn hoa-[2H2+O2->2H2O] hết";
        Check("proposal/source-and-generated-spans-separated",()=>{
            Require(proposal.AfterRaw==expected&&proposal.Region.Source.Raw==raw);
            Require(proposal.Result.Source.Id!=proposal.Region.Source.Id&&proposal.Result.ContentSpan.Start==0);
            Require(proposal.Edit.Span.Equals(reading.ContentSpan));
            foreach(var node in Walk(result.Candidates[0].Document.Root))foreach(var span in node.SourceSpans)result.Source.Validate(span);
        });
        Check("projection/charges-groups-reversible",()=>{
            foreach(var text in new[]{"K4[Fe(CN)6]+H2O<->K4[Fe(CN)6]+H2O","Fe^2++Ce^4+->Fe^3++Ce^3+","2Ca(OH)2+4HCl->2CaCl2+4H2O"}){
                var original=Parse(text);var projected=ChemistryProjection.Create(original.Candidates[0].Document.Root);
                Require(projected.Source.Raw==text);Require(CandidateExporter.ToMathMl(original.Candidates[0])==CandidateExporter.ToMathMl(projected.Candidates[0]));
            }
        });
        string serialized=AssistanceSerializer.Serialize(proposal);
        Check("proposal/roundtrip-exact",()=>{
            var decoded=AssistanceSerializer.Deserialize(serialized);Require(decoded.Id==proposal.Id&&AssistanceSerializer.Serialize(decoded)==serialized);
            Require(decoded.AfterRaw==expected&&decoded.Context.Id==context.Id&&decoded.OriginalReading!.Candidates[0].Id==reading.Candidates[0].Id);
            File.WriteAllText(Path.Combine(output,"proposal-balance.json"),serialized);
        });
        Check("context/canonical-order-and-distinct-conditions",()=>{
            var a=AssistanceHistory.Context(settings,new[]{new ConditionFact("medium","aqueous"),new ConditionFact("energy","heat")});
            var b=AssistanceHistory.Context(settings,new[]{new ConditionFact("energy","heat"),new ConditionFact("medium","aqueous")});Require(a.Id==b.Id);
            Require(a.Id!=AssistanceHistory.Context(settings,new[]{new ConditionFact("medium","aqueous")}).Id);
            Reject(()=>AssistanceHistory.Context(settings,new[]{new ConditionFact("medium","a"),new ConditionFact("medium","b")}));
        });
        Check("proposal/reject-false-original-reading-and-kind",()=>{
            Reject(()=>new AssistanceProposal("direct",region,context,result,new(AssistanceVersions.Solver),reading));
            Reject(()=>new AssistanceProposal("balance",region,context,result,new(AssistanceVersions.Solver),Parse("H2O")));
            Reject(()=>new AssistanceProposal("complete-reaction",region,context,result,new(AssistanceVersions.Solver)));
            Reject(()=>new AssistanceProposal("balance",region,context,result,new(AssistanceVersions.Solver)));
        });
        Check("proposal/reject-text-tree-mismatch",()=>{
            var s=new SourceSnapshot("2H2+N2->2H2O");var span=new TextSpan(0,s.Raw.Length);var candidate=new Candidate("direct",result.Candidates[0].Document,s,span,span);
            Reject(()=>new AssistanceProposal("balance",region,context,new(s,span,span,new[]{candidate}),new(AssistanceVersions.Solver),reading));
        });
        foreach(var (id,change) in new (string,Action<JsonObject>)[]{
            ("id",p=>p["Id"]="bad"),("source-revision",p=>p["Region"]!["Revision"]=22),
            ("span",p=>p["Region"]!["Start"]=0),("intent",p=>p["Region"]!["Domain"]="math"),
            ("routing-version",p=>p["Region"]!["RoutingVersion"]="future"),
            ("solver-version",p=>p["Solver"]="future"),("condition",p=>p["Conditions"]=new JsonArray(JsonSerializer.SerializeToNode(new{Key="medium",Value="aqueous"}))),
            ("missing-result",p=>p.Remove("Result"))})
            Check("proposal/tamper-"+id,()=>Reject(()=>AssistanceSerializer.Deserialize(EditPayload(serialized,change))));
        Check("proposal/reject-version-checksum-duplicates-budget",()=>{
            var e=JsonNode.Parse(serialized)!;e["Version"]="future";Reject(()=>AssistanceSerializer.Deserialize(e.ToJsonString()));
            e=JsonNode.Parse(serialized)!;e["Sha256"]="bad";Reject(()=>AssistanceSerializer.Deserialize(e.ToJsonString()));
            Reject(()=>AssistanceSerializer.Deserialize(serialized.Replace("\"Version\":","\"Version\":\"duplicate\",\"Version\":")));
            Reject(()=>AssistanceSerializer.Deserialize(new string('x',AssistanceSerializer.MaxCharacters+1)));
        });
        ReactionDraft Draft(bool closed)
        {
            var text="hoa-[H2+O2="+(closed?"]":"");var source=new SourceSnapshot(text,31);int end=text.Length-(closed?1:0);
            var area=new AssistanceRegion(source,new(5,end),new(0,text.Length),new("hoa-[","]"),new("chemistry","chemistry"),closed);
            return new(area,new ChemistryParser().Parse(source,new(5,end-1)),new(end-1,end),"arrow");
        }
        Check("draft/open-and-closed-roundtrip",()=>{
            foreach(bool closed in new[]{false,true}){var draft=Draft(closed);var json=AssistanceSerializer.SerializeDraft(draft);var decoded=AssistanceSerializer.DeserializeDraft(json);Require(decoded.Id==draft.Id&&decoded.Region.IsClosed==closed&&decoded.Reactants.Candidates[0].Document.Root.Type!="ChemReaction");File.WriteAllText(Path.Combine(output,closed?"draft-closed.json":"draft-open.json"),json);}
        });
        Check("draft/reject-hidden-prefix-and-wrong-separator",()=>{
            var draft=Draft(false);Reject(()=>new ReactionDraft(draft.Region,draft.Reactants,draft.SeparatorSpan,"reversible"));
            var source=new SourceSnapshot("bad H2+O2=");var area=new AssistanceRegion(source,new(0,source.Raw.Length),new(0,source.Raw.Length));var left=new ChemistryParser().Parse(source,new(4,source.Raw.Length-1));
            Reject(()=>new ReactionDraft(area,left,new(source.Raw.Length-1,source.Raw.Length),"arrow"));
        });
        Check("history/file-v4-keeps-before-result-and-conditions",()=>{
            var afterSource=new SourceSnapshot(expected,22);int caret=expected.IndexOf(']')+1;
            var accepted=new AcceptedTransformation(proposal,before,afterSource,new(reading.ContentSpan.End,reading.ContentSpan.End),new(caret,caret));
            var afterAnalysis=AnalysisWire.Analyze(new(expected,22,settings));var after=new FormulaState(expected,22,settings,afterAnalysis,0,afterAnalysis.Regions[0].Candidates[0].Id,new(new[]{accepted}));
            var file=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),2,after,new()));Require(JsonNode.Parse(file)!["version"]!.GetValue<int>()==4);
            var reopened=(FormulaDocument)DocumentCodec.Open(file).Document!;Require(reopened.State.Transformations!.Entries.Single().Proposal.Id==proposal.Id);
            Require(reopened.State.Transformations.Entries[0].Before.CandidateId==before.CandidateId&&reopened.State.Transformations.Entries[0].Before.Raw==raw);
            File.WriteAllText(Path.Combine(output,"accepted-balance.locus"),file);
            using var session=new FormulaSession(new NativeAnalysisScheduler());session.Load(reopened.State);session.UpdateSource("x^2");Require(session.State.Transformations!.Entries[0].Proposal.Id==proposal.Id);Require(session.Undo()&&session.State.Raw==expected&&session.CanExport);
            var downgraded=JsonNode.Parse(file)!;downgraded["version"]=3;Require(!DocumentCodec.Open(downgraded.ToJsonString()).IsSupported);
            foreach(var edit in new Action<JsonObject>[]{p=>p["transformations"]![0]!["afterRaw"]="wrong",p=>p["transformations"]![0]!["selectionAfter"]!["start"]=0,p=>p["transformations"]![0]!["before"]!["raw"]="different"})
                Require(!DocumentCodec.Open(EditPayload(file,edit,true)).IsSupported);
        });
        Check("history/open-wrapper-never-invents-closer-or-complete-region",()=>{
            var draft=Draft(false);var p=new AssistanceProposal("complete-reaction",draft.Region,AssistanceHistory.Context(settings),result,new(AssistanceVersions.Solver,AssistanceVersions.Catalog,"test-fixture",new[]{"https://example.test/fixture"}));
            var prior=new FormulaState(draft.Region.Source.Raw,31,settings);var next=new SourceSnapshot(p.AfterRaw,32);var caret=next.Raw.Length;
            var record=new AcceptedTransformation(p,prior,next,new(prior.Raw.Length,prior.Raw.Length),new(caret,caret));
            Require(!next.Raw.EndsWith(']'));var analysisAfter=AnalysisWire.Analyze(new(next.Raw,32,settings));Require(analysisAfter.IsIncomplete&&analysisAfter.Regions.Count==0);
            var document=new FormulaDocument(Guid.NewGuid(),1,new(next.Raw,32,settings,analysisAfter,Transformations:new(new[]{record})),new());
            Require(DocumentCodec.Open(DocumentCodec.Serialize(document)).IsSupported);
        });
        Check("history/space-and-bounds",()=>{
            int caret=expected.IndexOf(']')+1;var after=new SourceSnapshot(expected.Insert(caret," "),22);
            _=new AcceptedTransformation(proposal,before,after,new(reading.ContentSpan.End,reading.ContentSpan.End),new(caret+1,caret+1),true);
            Reject(()=>new AcceptedTransformation(proposal,before,after,new(1,1),new(caret+1,caret+1),true)); // Inside the leading emoji surrogate pair.
            Reject(()=>new AssistanceHistory(Array.Empty<AcceptedTransformation>()));
        });
        await File.WriteAllTextAsync(Path.Combine(output,"assistance-contracts.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,scope="SC1-03 data models, projection, provenance and file history; no live assistance acceptance, balancing or prediction",summary=new{checks=checks.Count,passed=checks.Count-failed,failed},checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SC1 assistance contracts: {checks.Count-failed}/{checks.Count}");return failed;
    }
    private static IEnumerable<MathNode> Walk(MathNode node){yield return node;foreach(var child in node.Children)foreach(var n in Walk(child))yield return n;}
    private static string EditPayload(string json,Action<JsonObject> edit,bool file=false)
    {
        string payloadKey=file?"payload":"Payload",hashKey=file?"sha256":"Sha256";
        var envelope=JsonNode.Parse(json)!.AsObject();var payload=JsonNode.Parse(envelope[payloadKey]!.GetValue<string>())!.AsObject();edit(payload);
        string text=payload.ToJsonString();envelope[payloadKey]=text;envelope[hashKey]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));return envelope.ToJsonString();
    }
}
