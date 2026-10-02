using System.Text.Json;
using System.Xml.Linq;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Domains;
using Locus.Core.Export;
using Locus.Core.Serialization;
using Locus.Application;

var root=Path.GetFullPath(args.FirstOrDefault()??".");
var output=Path.GetFullPath(args.ElementAtOrDefault(1)??Path.Combine(root,"artifacts/e3"));Directory.CreateDirectory(output);
var checks=new List<object>();int failed=0;
var workerFixtures=new List<object>();
void Check(string id,Action action){try{action();checks.Add(new{id,status="PASS"});}catch(Exception ex){failed++;checks.Add(new{id,status="FAIL",error=ex.ToString()});Console.WriteLine(id+": "+ex.Message);}}
void Require(bool value,string message){if(!value)throw new Exception(message);}
AnalysisResult Analyze(string text,int flags=7,InputMode mode=InputMode.Explicit)=>new AnalysisEngine().Analyze(new SourceSnapshot(text),new AnalysisOptions(mode,enabledDomains:(DetectionDomains)flags));
bool Contains(MathNode n,string type)=>n.Type==type||n.Children.Any(c=>Contains(c,type));
using var corpus=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"corpus/e3/cases.json")));
foreach(var item in corpus.RootElement.EnumerateArray())
{
    string id=item.GetProperty("id").GetString()!,raw=item.GetProperty("raw").GetString()!;
    for(int mask=0;mask<8;mask++)
    {
        int flags=mask;
        Check(id+"/"+flags,()=>{
            var result=Analyze(raw,flags);
            var request=new AnalysisRequest(raw,0,new(EnabledDomains:(DetectionDomains)flags));
            workerFixtures.Add(new{id=id+"/"+flags,request=AnalysisWire.Request(request),result=AnalysisWire.Serialize(AnalysisWire.Analyze(request))});
            bool expected=(flags&1)!=0&&item.GetProperty("math").GetBoolean()||(flags&2)!=0&&item.GetProperty("physics").GetBoolean()||(flags&4)!=0&&item.GetProperty("chemistry").GetBoolean();
            Require((result.Regions.Count>0)==expected,$"Expected accepted={expected}; {string.Join(',',result.Diagnostics.Select(d=>d.Code))}");
            Require(result.Source.Raw==raw,"Raw changed");
            foreach(var set in result.Regions)
            {
                Require(set.Candidates.Count<=3,"Candidate limit");
                var restored=CandidateSetSerializer.Deserialize(CandidateSetSerializer.Serialize(set));
                Require(restored.OriginalReplacement==raw,"Snapshot source");
                Require(CandidateSetSerializer.Serialize(restored)==CandidateSetSerializer.Serialize(set),"Snapshot round trip");
                foreach(var candidate in set.Candidates)
                {
                    int bit=candidate.Document.Domain switch{"math"=>1,"physics"=>2,"chemistry"=>4,_=>0};Require((flags&bit)!=0,"Disabled domain");
                    var exported=CandidateExporter.Export(candidate);XDocument.Parse(exported.MathMl);XDocument.Parse(exported.Omml);
                    Require(exported.OriginalText==raw,"Export source changed");
                    Require(exported.MathMl==CandidateExporter.ToMathMl(restored.Candidates.First(c=>c.Id==candidate.Id)),"Restored export drift");
                }
                if(item.TryGetProperty("node",out var node)&&((flags&2)!=0&&item.GetProperty("physics").GetBoolean()||(flags&4)!=0&&item.GetProperty("chemistry").GetBoolean()))
                    Require(set.Candidates.Any(c=>Contains(c.Document.Root,node.GetString()!)),"Missing semantic node "+node);
            }
        });
    }
}
Check("passive/chemistry-source-spans",()=>{const string raw="Thêm H2SO4 vào Ca(OH)2.";var result=Analyze(raw,4,InputMode.Passive);Require(result.Regions.Count==2,"Two chemistry regions");Require(result.Regions[0].OriginalContent=="H2SO4"&&result.Regions[1].OriginalContent=="Ca(OH)2","Corridor source");});
Check("passive/weak-word",()=>Require(Analyze("Có Co trong câu",4,InputMode.Passive).Regions.Count==0,"Weak chemical word in prose"));
for(int flags=1;flags<8;flags++) foreach(bool profiles in new[]{false,true})
{
    int mask=flags;bool useProfiles=profiles;
    Check($"passive/prose-evidence/{mask}/{useProfiles}",()=>{
        foreach(string raw in new[]{"Dòng 1: đã xong.","Có 21 câu.","Mức -2.","Chọn x.","Có Co trong câu."})
        {
            var settings=new FormulaSettings(InputMode.Passive,EnabledDomains:(DetectionDomains)mask,MarkerProfiles:useProfiles?MarkerPreferences.Default:null);
            var request=new AnalysisRequest(raw,0,settings);var result=AnalysisWire.Analyze(request);
            Require(result.Regions.Count==0,"Ordinary prose detected: "+raw+" => "+string.Join(" | ",result.Regions.Select(r=>r.OriginalContent)));
            workerFixtures.Add(new{id=$"prose/{mask}/{useProfiles}/{raw}",request=AnalysisWire.Request(request),result=AnalysisWire.Serialize(result)});
        }
        if((mask&3)!=0)Require(AnalysisWire.Analyze(new("1",0,new(EnabledDomains:(DetectionDomains)mask))).Regions.Count==1,"Explicit number no longer accepted");
    });
}
Check("passive/prose-between-named-wrappers",()=>{
    const string raw="Dòng 1: toan-[x^2]. Dòng 2: hoa-[H2O].";
    var request=new AnalysisRequest(raw,0,new(InputMode.Passive,EnabledDomains:DetectionDomains.All,MarkerProfiles:MarkerPreferences.Default));
    var result=AnalysisWire.Analyze(request);
    Require(result.Regions.Select(r=>r.OriginalReplacement).SequenceEqual(new[]{"toan-[x^2]","hoa-[H2O]"}),"Numbered prose or wrapper changed");
    workerFixtures.Add(new{id="prose/mixed-wrappers",request=AnalysisWire.Request(request),result=AnalysisWire.Serialize(result)});
    foreach(string wrapper in new[]{"toan-[1]","ly-[1]"})Require(AnalysisWire.Analyze(new(wrapper,0,request.Settings)).Regions.Count==1,"Explicit wrapped number rejected");
});
Check("passive/physics-source-spans",()=>{var result=Analyze("Biết v = 10 m/s và x mũ 2.",7,InputMode.Passive);Require(result.Regions.Count==2,"Two regions");Require(result.Regions[0].OriginalContent=="v = 10 m/s","Quantity boundary");});
Check("passive/physics-prefixed-unit",()=>{var result=Analyze("Vật có m = 10 kg và v = 15 km/h.",2,InputMode.Passive);Require(result.Regions.Count==2&&result.Regions[0].OriginalContent=="m = 10 kg"&&result.Regions[1].OriginalContent=="v = 15 km/h","Prefixed unit corridor: "+string.Join(" | ",result.Regions.Select(r=>r.OriginalContent))+"; "+string.Join(",",result.Diagnostics.Select(d=>d.Code)));});
Check("passive/protected",()=>Require(Analyze("https://example.com/H2SO4",7,InputMode.Passive).Regions.Count==0,"Protected URL"));
Check("physics/relative-unit-context",()=>{Require(Analyze("15 km/h",2).Regions.Count==1,"Quantity mistaken for path");Require(Analyze("kg/m",2).Regions.Count==0,"Bare path reinterpreted");Require(Analyze("15 https://m/s",2).Regions.Count==0,"URL reinterpreted");Require(Analyze("lc[15 kg/m^3]",2,InputMode.Markers).Regions.Count==1,"Marker unit mistaken for path");});
Check("markers/mixed",()=>{var result=Analyze("A lc[H2SO4] B lc[v_0] C lc[x^2]",7,InputMode.Markers);Require(result.Regions.Count==3,"Three markers");Require(result.Regions[0].OriginalReplacement=="lc[H2SO4]","Marker preserved");});
Check("markers/custom",()=>{var result=new AnalysisEngine().Analyze(new SourceSnapshot("A {{H₂SO₄}} B"),new AnalysisOptions(InputMode.Markers,markers:new("{{","}}"),enabledDomains:DetectionDomains.Chemistry));Require(result.Regions.Single().OriginalReplacement=="{{H₂SO₄}}","Custom wrapper");});
Check("chemistry/no-balance",()=>{var result=Analyze("H2 + O2 -> H2O",4);Require(result.Regions.Single().Candidates.Single().Edits.Count==0,"No balancing edits");});
Check("physics/unit-roman",()=>{var ml=CandidateExporter.ToMathMl(Analyze("v = 10 m/s",2).Regions.Single().Candidates.First());Require(ml.Contains("mathvariant=\"normal\">m")&&ml.Contains("<mi>v</mi>"),"Units upright / variable italic");});
Check("physics/domain-ambiguity",()=>{var r=Analyze("v = 10 m/s",7);Require(r.Regions.Single().Candidates.Count==2,"Quantity vs algebra interpretations");Require(r.ContentEligibility=="blocked","Ambiguity must block auto");});
Check("legacy/snapshot-unchanged-with-common-physics",()=>{var old=Analyze("1/2x",1).Regions.Single();var current=Analyze("1/2x",3).Regions.Single();Require(CandidateSetSerializer.Serialize(old)==CandidateSetSerializer.Serialize(current),"Legacy candidates changed");});
Check("document/domain-isolation",()=>{var node=Analyze("H2O",4).Regions.Single().Candidates.Single().Document.Root;bool rejected=false;try{_ = new MathDocument(node);}catch(ArgumentException){rejected=true;}Require(rejected,"Chemistry nodes in MathDocument");});
Check("input/cancellation",()=>{using var c=new CancellationTokenSource();c.Cancel();bool cancelled=false;try{_ = new AnalysisEngine().Analyze(new SourceSnapshot("H2SO4"),new AnalysisOptions(enabledDomains:DetectionDomains.All),c.Token);}catch(OperationCanceledException){cancelled=true;}Require(cancelled,"Cancellation ignored");});
Check("input/depth",()=>Require(Analyze(new string('(',100)+"H2"+new string(')',100),4).Regions.Count==0,"Depth not bounded"));
var legacyJson=await CoreVerification.RunInMemory(File.ReadAllText(Path.Combine(root,"corpus/m0/cases.json")));
File.WriteAllText(Path.Combine(output,"math-regression.json"),legacyJson);
using(var legacy=JsonDocument.Parse(legacyJson))Check("legacy/238-contracts",()=>Require(legacy.RootElement.GetProperty("summary").GetProperty("failed").GetInt32()==0,"Legacy contract failures"));
File.WriteAllText(Path.Combine(output,"core-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,summary=new{checks=checks.Count,passed=checks.Count-failed,failed},results=checks},new JsonSerializerOptions{WriteIndented=true}));
File.WriteAllText(Path.Combine(output,"worker-fixtures.json"),JsonSerializer.Serialize(workerFixtures));
Console.WriteLine($"E3 core: {checks.Count-failed}/{checks.Count} passed; reports in {output}");
failed+=await ApplicationVerification.Run(root,output);
return failed==0?0:1;
