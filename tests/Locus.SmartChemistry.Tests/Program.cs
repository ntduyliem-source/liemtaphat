using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;
using Locus.Application;

var root=Path.GetFullPath(args.FirstOrDefault()??".");
var output=Path.Combine(root,"artifacts/sc1");Directory.CreateDirectory(output);
var checks=new List<object>();var fixtures=new List<object>();int failed=0;
void Check(string id,Action action){try{action();checks.Add(new{id,status="PASS"});}catch(Exception ex){failed++;checks.Add(new{id,status="FAIL",error=ex.ToString()});Console.WriteLine("FAIL "+id+": "+ex.Message);}}
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Throws(Action action){bool thrown=false;try{action();}catch(Exception e)when(e is ArgumentException or FormatException){thrown=true;}Require(thrown,"Invalid data accepted");}
FormulaSettings Settings(InputMode mode=InputMode.Explicit,int flags=7)=>new(mode,EnabledDomains:(DetectionDomains)flags,MarkerProfiles:MarkerPreferences.Default);
AnalysisResult Analyze(string raw,FormulaSettings? settings=null)=>AnalysisWire.Analyze(new(raw,17,settings??Settings()));
using(var corpus=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"corpus/sc1/markers.json"))))
foreach(var item in corpus.RootElement.EnumerateArray())
{
    string id=item.GetProperty("id").GetString()!,raw=item.GetProperty("raw").GetString()!;
    var settings=Settings(Enum.Parse<InputMode>(item.GetProperty("mode").GetString()!),item.GetProperty("flags").GetInt32());
    if(item.TryGetProperty("commonOpen",out var open))settings=settings with{Open=open.GetString()!};
    if(item.TryGetProperty("commonClose",out var close))settings=settings with{Close=close.GetString()!};
    if(item.TryGetProperty("disable",out _))settings=settings with{MarkerProfiles=settings.MarkerProfiles! with{Chemistry=settings.MarkerProfiles!.Chemistry with{Enabled=false}}};
    Check(id,()=>{
        var result=Analyze(raw,settings);var expected=item.GetProperty("regions").EnumerateArray().ToArray();
        Require(result.Regions.Count==expected.Length,$"Expected {expected.Length} regions, got {result.Regions.Count}: "+string.Join(',',result.Diagnostics.Select(d=>d.Code)));
        Require(result.Source.Raw==raw,"Raw changed");
        for(int i=0;i<expected.Length;i++)
        {
            var set=result.Regions[i];var e=expected[i];
            Require(set.OriginalContent==e.GetProperty("content").GetString(),"Content scope");
            Require(set.OriginalReplacement==e.GetProperty("replacement").GetString(),"Replacement scope");
            Require(set.Candidates[0].Document.Domain==e.GetProperty("domain").GetString(),"Domain override");
            Require(set.Intent?.ProfileId==(e.TryGetProperty("profile",out var p)?p.GetString():null),"Marker identity");
            var bytes=CandidateSetSerializer.Serialize(set);var restored=CandidateSetSerializer.Deserialize(bytes);
            Require(CandidateSetSerializer.Serialize(restored)==bytes,"Snapshot round trip");
            Require(CandidateExporter.ToMathMl(restored.Candidates[0])==CandidateExporter.ToMathMl(set.Candidates[0]),"Export drift");
            Require(set.Select(set.Candidates[0].Id).Intent==set.Intent,"Select lost intent");
        }
        if(item.TryGetProperty("diagnostic",out var d))Require(result.Diagnostics.Any(x=>x.Code==d.GetString()),"Missing diagnostic "+d);
        if(item.TryGetProperty("incomplete",out var incomplete))Require(result.IsIncomplete==incomplete.GetBoolean(),"Draft readiness");
        if(item.TryGetProperty("draft",out var draft))
        {
            var scan=ProfileMarkerScanner.Scan(new SourceSnapshot(raw),settings.MarkerProfiles!.ToProfiles(settings.Open,settings.Close));
            Require(scan.Drafts.Count==1&&!scan.Drafts[0].IsClosed&&raw[scan.Drafts[0].ContentSpan.Start..scan.Drafts[0].ContentSpan.End]==draft.GetString(),"Draft is not a complete candidate");
        }
        var request=new AnalysisRequest(raw,17,settings);
        fixtures.Add(new{id,request=AnalysisWire.Request(request),result=AnalysisWire.Serialize(result)});
    });
}
for(int mask=0;mask<8;mask++)foreach(var mode in Enum.GetValues<InputMode>())foreach(var pair in new[]{("toan-[x^2]","math"),("ly-[v_0]","physics"),("hoa-[H2SO4]","chemistry")})
{
    int flags=mask;
    Check($"override/{pair.Item2}/{mode}/{flags}",()=>{
        var request=new AnalysisRequest(pair.Item1,23,Settings(mode,flags));var result=AnalysisWire.Analyze(request);
        Require(result.Regions.Single().Candidates.All(c=>c.Document.Domain==pair.Item2),"Flags overrode explicit intent");
        fixtures.Add(new{id=$"override/{pair.Item2}/{mode}/{flags}",request=AnalysisWire.Request(request),result=AnalysisWire.Serialize(result)});
    });
}
Check("config/whole-set-validation",()=>{
    var defaults=Settings();Require(defaults.HasValidMarkerProfiles,"Default configuration invalid");
    foreach(var invalid in new[]{defaults with{Open="hoa-["},defaults with{Open="hoa-"},defaults with{Close="hoa-["},defaults with{Open=""},defaults with{Open="x\n"},defaults with{Open="x\\"},defaults with{Open="\uD800"}})
        Require(!invalid.HasValidMarkerProfiles,"Conflict accepted");
    using var session=new FormulaSession(new NativeAnalysisScheduler());session.Configure(defaults);var before=session.State;
    Throws(()=>session.Configure(defaults with{Open="hoa-["}));Require(ReferenceEquals(before,session.State),"Partial setting mutation");
});
Check("config/migration-retains-existing",()=>{
    var (profiles,notice)=MarkerPreferences.Migrate("hoa-[","]");
    Require(!profiles.Chemistry.Enabled&&profiles.Math.Enabled&&profiles.Physics.Enabled&&notice.Length>0,"Colliding preset not disabled");
    Require(profiles.ToProfiles("hoa-[","]").IsValid,"Migrated config invalid");
    var custom=MarkerPreferences.Migrate("{{","}}");Require(custom.Profiles.Chemistry.Enabled,"Valid custom config lost presets");
});
Check("scanner/limits-cancellation",()=>{
    var profiles=MarkerPreferences.Default.ToProfiles("lc[","]");
    var counted=ProfileMarkerScanner.Scan(new SourceSnapshot(string.Concat(Enumerable.Repeat("hoa-[H2O]",4))),profiles,maxRegions:3);
    Require(counted.LimitExceeded&&counted.Regions.Count==0,"Partial regions after limit");
    var depth=ProfileMarkerScanner.Scan(new SourceSnapshot("hoa-["+new string('[',129)+"H2"+new string(']',130)),profiles);
    Require(depth.Regions.Count==0&&depth.Diagnostics.Any(d=>d.Code=="MARKER_DEPTH_LIMIT"),"Unbounded nesting");
    using var cancel=new CancellationTokenSource();cancel.Cancel();bool stopped=false;
    try{ProfileMarkerScanner.Scan(new SourceSnapshot("hoa-[H2O]"),profiles,cancellationToken:cancel.Token);}catch(OperationCanceledException){stopped=true;}
    Require(stopped,"Cancellation ignored");
});
Check("serialization/intent-tampering",()=>{
    var region=Analyze("hoa-[H2SO4]").Regions.Single();
    foreach(var change in new Action<JsonObject>[]{p=>p["intent"]!["domain"]="math",p=>p["intent"]!["profileId"]="unknown",p=>p["intent"]!["grammarVersion"]="future",p=>p["intent"]=null,p=>p["snapshotVersion"]="locus-candidate-set/0.2"})
    {
        var envelope=JsonNode.Parse(CandidateSetSerializer.Serialize(region))!.AsObject();var payload=JsonNode.Parse(envelope["payload"]!.GetValue<string>())!.AsObject();
        change(payload);string text=payload.ToJsonString();envelope["payload"]=text;envelope["sha256"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        Throws(()=>CandidateSetSerializer.Deserialize(envelope.ToJsonString()));
    }
});
Check("application/file-preferences-wire",()=>{
    var settings=Settings(flags:0);var result=Analyze("hoa-[H₂SO₄]",settings);
    var state=new FormulaState(result.Source.Raw,17,settings,result,0,result.Regions[0].Candidates[0].Id);
    var document=new FormulaDocument(Guid.NewGuid(),1,state,new());var file=DocumentCodec.Serialize(document);
    Require(JsonNode.Parse(file)!["version"]!.GetValue<int>()==3,"Wrong new format version");
    var opened=(FormulaDocument)DocumentCodec.Open(file).Document!;Require(opened!=null&&opened.State.CandidateId==state.CandidateId,"File snapshot lost");
    File.WriteAllText(Path.Combine(output,"named-chemistry-off.locus"),file);
    var downgraded=JsonNode.Parse(file)!;downgraded["version"]=2;Require(!DocumentCodec.Open(downgraded.ToJsonString()).IsSupported,"Profile data in old format");
    var prefs=new EditorPreferences(3,settings,new(),true);Require(EditorPreferences.Read(prefs.Serialize())==prefs,"Preferences round trip");
    Throws(()=>new EditorPreferences(2,settings,new(),true).Serialize());
    var wire=AnalysisWire.Deserialize(AnalysisWire.Run(AnalysisWire.Request(new(state.Raw,17,settings))));Require(wire.Regions[0].Intent!.Domain=="chemistry","Worker lost domain");
});
Check("application/migrate-without-reparse",()=>{
    using var session=new FormulaSession(new NativeAnalysisScheduler());session.UpdateSource("x+1/2");session.AnalyzeAsync().GetAwaiter().GetResult();
    var old=session.State.Analysis;var candidate=session.State.CandidateId;var revision=session.State.SourceRevision;var lease=session.Lease();
    session.UpgradeMarkerProfiles();Require(ReferenceEquals(old,session.State.Analysis)&&candidate==session.State.CandidateId&&revision==session.State.SourceRevision,"Migration reparsed old snapshot");
    Require(!session.IsCurrent(lease),"Migration kept old lease");
    Require(DocumentCodec.Open(DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,session.State,new()))).IsSupported,"Migrated snapshot refused");
    session.UpdateSource("hoa-[H2SO4]");session.AnalyzeAsync().GetAwaiter().GetResult();Require(session.State.Candidate?.Document.Domain=="chemistry","Next input missing presets");
});
Check("application/flags-undo-stale",()=>{
    using var session=new FormulaSession(new NativeAnalysisScheduler());session.Configure(Settings());session.UpdateSource("hoa-[H2SO4]");session.AnalyzeAsync().GetAwaiter().GetResult();
    var before=session.State;var lease=session.Lease();session.Configure(session.State.Settings with{EnabledDomains=DetectionDomains.None});
    Require(session.CanExport&&session.State.CandidateId==before.CandidateId&&!session.IsCurrent(lease),"Flags discarded snapshot or kept lease");
    session.Configure(session.State.Settings with{MarkerProfiles=MarkerPreferences.Default with{Chemistry=new("chem{","}")}});
    Require(!session.CanExport,"New marker config kept stale candidate");session.Undo();Require(session.CanExport&&session.State.CandidateId==before.CandidateId,"Settings Undo lost candidate");
});
Check("application/late-result-after-config-or-source-change",()=>{
    foreach(bool changeSource in new[]{false,true})
    {
        var scheduler=new HeldScheduler();using var session=new FormulaSession(scheduler);session.Configure(Settings());session.UpdateSource("hoa-[H2SO4]");
        var request=session.AnalyzeAsync();Require(session.IsBusy,"Work did not begin");
        if(changeSource)session.UpdateSource("toan-[x^2]");else session.Configure(session.State.Settings with{MarkerProfiles=MarkerPreferences.Default with{Chemistry=new("chem{","}")}});
        var state=session.State;var version=session.Version;
        scheduler.Complete();Require(!request.GetAwaiter().GetResult(),"Late result reported success");
        Require(ReferenceEquals(state,session.State)&&version==session.Version&&!session.CanExport,"Late reply changed current state");
    }
});
Check("legacy/504-wire-fixtures-unchanged",()=>{
    foreach(var path in new[]{"artifacts/e3/worker-fixtures.json","artifacts/web1/worker-native-fixtures.json"})
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,path)));
        foreach(var item in data.RootElement.EnumerateArray())
        {
            string? expected=item.TryGetProperty("result",out var r)?r.GetString():null;bool expectsError=item.TryGetProperty("error",out var e)&&e.ValueKind!=JsonValueKind.Null;string? actual=null;bool error=false;
            try{actual=AnalysisWire.Run(item.GetProperty("request").GetString()!);}catch{error=true;}
            Require(expectsError?error:!error&&actual==expected,"Historical wire changed: "+item.GetProperty("id"));
        }
    }
});
var legacy=await CoreVerification.RunInMemory(File.ReadAllText(Path.Combine(root,"corpus/m0/cases.json")));
File.WriteAllText(Path.Combine(output,"math-regression.json"),legacy);
Check("legacy/238-contracts",()=>{using var json=JsonDocument.Parse(legacy);Require(json.RootElement.GetProperty("summary").GetProperty("failed").GetInt32()==0,"Legacy contracts failed");});
File.WriteAllText(Path.Combine(output,"worker-fixtures.json"),JsonSerializer.Serialize(fixtures));
File.WriteAllText(Path.Combine(output,"marker-verification.json"),JsonSerializer.Serialize(new{capturedAtUtc=DateTimeOffset.UtcNow,scope="SC1 marker routing and application contracts; no balance/prediction/ghost yet",summary=new{checks=checks.Count,passed=checks.Count-failed,failed},results=checks},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"SC1 markers: {checks.Count-failed}/{checks.Count}; native worker fixtures: {fixtures.Count}");
failed+=await AssistanceVerification.Run(output);
failed+=ChemistryInputVerification.Run(root,output);
failed+=BalanceVerification.Run(root,output);
failed+=await AssistanceSessionVerification.Run(output);
failed+=await CatalogVerification.Run(root,output);
failed+=await GhostTransactionVerification.Run(output);
return failed==0?0:1;

sealed class HeldScheduler : IAnalysisScheduler
{
    private readonly TaskCompletionSource<AnalysisResult> response=new();private AnalysisRequest? input;
    public Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request,CancellationToken cancellationToken){input=request;return response.Task;}
    public void Complete()=>response.SetResult(AnalysisWire.Analyze(input!));
}
