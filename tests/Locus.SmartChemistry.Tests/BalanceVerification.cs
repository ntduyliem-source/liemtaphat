using System.Diagnostics;
using System.Text.Json;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Domains;
using Locus.Core.Parsing;
using Locus.Application;

internal static class BalanceVerification
{
    internal static int Run(string root,string output)
    {
        var checks=new List<object>();var fixtures=new List<object>();var elapsed=new List<double>();int failed=0;
        void Require(bool ok,string message="Contract failed"){if(!ok)throw new Exception(message);}
        void Check(string id,Action action){try{action();checks.Add(new{id,status="PASS"});}catch(Exception e){failed++;checks.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}}
        ScientificDocument Parse(string raw){var source=new SourceSnapshot(raw);return new ChemistryParser(true).Parse(source,new(0,raw.Length)).Candidates.Single().Document;}
        using var corpus=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"corpus/sc1/balance.json")));
        foreach(var item in corpus.RootElement.EnumerateArray())
        {
            string id=item.GetProperty("id").GetString()!,raw=item.GetProperty("raw").GetString()!;
            Check(id,()=>{
                var document=Parse(raw);var watch=Stopwatch.StartNew();var result=ReactionBalancer.Balance(document);elapsed.Add(watch.Elapsed.TotalMilliseconds);
                Require(result.Status.ToString()==item.GetProperty("status").GetString(),"Expected "+item.GetProperty("status")+" got "+result.Status);
                if(item.TryGetProperty("result",out var expected)){Require(result.Result?.Source.Raw==expected.GetString(),"Unexpected coefficients/source: "+result.Result?.Source.Raw);Require(ReactionBalancer.VerifyConservation(result.Result!.Candidates[0].Document.Root));}
                else Require(result.Result==null,"An unsuccessful/duplicate result produced a proposal");
                if(item.TryGetProperty("coefficients",out var numbers))Require(result.Coefficients.SequenceEqual(numbers.EnumerateArray().Select(n=>n.GetString()!)),"Incorrect minimum coefficients");
                if(result.Status==BalanceStatus.AlreadyBalanced)Require(ReactionBalancer.VerifyConservation(document.Root));
                var input=new AnalysisRequest("hoa-["+raw+"]",303,new(EnabledDomains:DetectionDomains.None,MarkerProfiles:MarkerPreferences.Default));
                var request=ChemistryAssistanceWire.Request(new(input,0,[]));var response=ChemistryAssistanceWire.Deserialize(AnalysisWire.Run(request));
                Require((response.Proposals.Length>0)==(result.Status==BalanceStatus.Balanced));
                if(response.Proposals.Length>0){var proposal=AssistanceSerializer.Deserialize(response.Proposals[0]);Require(proposal.ReplacementText==result.Result!.Source.Raw&&proposal.Kind=="balance"&&proposal.AfterRaw=="hoa-["+proposal.ReplacementText+"]");}
                fixtures.Add(new{id,request,result=AnalysisWire.Run(request)});
            });
        }
        Check("limits/cancellation-and-integer-budget",()=>{
            using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{ReactionBalancer.Balance(Parse("H2+O2->H2O"),cancel.Token);}catch(OperationCanceledException){cancelled=true;}Require(cancelled);
            var atom=new MathNode("ChemElement",name:"H");var huge=new MathNode("ChemSubscript",new[]{atom,new MathNode("Number",value:"1"+new string('0',1300))});
            Require(ReactionBalancer.Balance(new ChemistryDocument(new MathNode("ChemReaction",new[]{huge,atom},@operator:"arrow"))).Status==BalanceStatus.Limit);
            Require(!ReactionBalancer.VerifyConservation(Parse("H2+O2->H2O").Root));Require(!ReactionBalancer.VerifyConservation(Parse("Na^+->Na").Root));
        });
        Check("limits/species-budget",()=>{
            var atoms="H He Li Be B C N O F Ne Na Mg Al Si P S Cl Ar K Ca Sc Ti V Cr Mn Fe Co Ni Cu Zn Ga Ge As".Split(' ');
            MathNode left=new("ChemElement",name:atoms[0]);foreach(var name in atoms.Skip(1))left=new("ChemSum",new[]{left,new MathNode("ChemElement",name:name)});
            Require(ReactionBalancer.Balance(new ChemistryDocument(new MathNode("ChemReaction",new[]{left,new MathNode("ChemElement",name:"H")},@operator:"arrow"))).Status==BalanceStatus.Limit);
        });
        foreach(var (raw,status) in new[]{("hoa-[h2+o2=h2o]","available"),("hoa-[h2+o2=h20]","needs-interpretation"),("hoa-[co+o2=co2]","needs-interpretation"),("hoa-[h2+o2=","needs-conditions"),("hoa-[h2+o2=]","needs-conditions"),("toan-[x+1=2]","disabled"),("ly-[v=10 m/s]","disabled"),("hoa-[h2+o2+=]","needs-interpretation")})
            Check("wire/"+raw,()=>{
                var input=new AnalysisRequest(raw,404,new(EnabledDomains:DetectionDomains.All,MarkerProfiles:MarkerPreferences.Default));var request=ChemistryAssistanceWire.Request(new(input,0,[]));
                var response=ChemistryAssistanceWire.Deserialize(AnalysisWire.Run(request));Require(response.Status==status,"Expected "+status+" got "+response.Status);
                fixtures.Add(new{id="wire/"+raw,request,result=AnalysisWire.Run(request)});
            });
        elapsed.Sort();
        File.WriteAllText(Path.Combine(output,"balance-fixtures.json"),JsonSerializer.Serialize(fixtures));
        File.WriteAllText(Path.Combine(output,"balance-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,scope="Conservation-only exact solver; no claim of chemical occurrence or product prediction",summary=new{checks=checks.Count,passed=checks.Count-failed,failed},timing=new{cases=elapsed.Count,p50Ms=elapsed[elapsed.Count/2],p95Ms=elapsed[(int)Math.Floor((elapsed.Count-1)*.95)],maxMs=elapsed[^1],note="Single native pass including cold start; not UI ghost latency or a performance acceptance."},checks},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"SC1 exact balance: {checks.Count-failed}/{checks.Count}");return failed;
    }
}
