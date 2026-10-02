using System.Diagnostics;
using System.Text.Json;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;
using Microsoft.JSInterop;

namespace Locus.WebProbe;

public sealed record ProbeInput(string Id, string Raw, string Mode = "Explicit", string Open = "lc[", string Close = "]", long Revision = 7);

public static class Probe
{
    public static string Corpus()
    { using var reader = new StreamReader(typeof(Probe).Assembly.GetManifestResourceStream("locus.corpus.json")!); return reader.ReadToEnd(); }
    public static IReadOnlyList<ProbeInput> Inputs()
    {
        using var corpus = JsonDocument.Parse(Corpus());
        var inputs = corpus.RootElement.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("status").GetString() != "pending" && c.GetProperty("context").GetProperty("action").GetString() != "reopen_snapshot").Select(c => {
            var context=c.GetProperty("context");
            string mode=context.GetProperty("inputMode").GetString() switch {"passive"=>"Passive","marked"=>"Markers",_=>"Explicit"};
            return new ProbeInput(c.GetProperty("id").GetString()!,c.GetProperty("source").GetProperty("raw").GetString()!,mode,
                context.TryGetProperty("delimiters",out var d)?d.GetProperty("open").GetString()!:"lc[",context.TryGetProperty("delimiters",out d)?d.GetProperty("close").GetString()!:"]");
        }).ToList();
        inputs.AddRange(new[]{
            new ProbeInput("web/nfd","x mu\u0303 2"),new ProbeInput("web/emoji","👩‍🏫 e\u0301 Ta có lc[x mu\u0303 2] và lc[1 trên 2].","Markers"),
            new ProbeInput("web/custom","Ta có toan[[x+1/2]] nhé.","Markers","toan[[","]]"),new ProbeInput("web/long",new string('1',4100)+"^2"),
            new ProbeInput("web/long-marked","lc["+new string('1',4100)+"^2]","Markers"),new ProbeInput("web/zero-revision","can2",Revision:0),
            new ProbeInput("web/huge-revision","x^2",Revision:long.MaxValue),new ProbeInput("web/empty",""),new ProbeInput("web/root-repair","can(2+3"),
            new ProbeInput("web/bad-markers","x^2","Markers","x","xy"),new ProbeInput("web/hangul","\u1100\u1161\u11A8+x"),new ProbeInput("web/hostile","<script>alert(1)</script>+x")
        });return inputs;
    }
    public static object Project(ProbeInput input)
    {
        try {
            var source=new SourceSnapshot(input.Raw,input.Revision);
            var normalized=NormalizedSource.Create(source);
            var analysis=new AnalysisEngine().Analyze(source,new AnalysisOptions(Enum.Parse<InputMode>(input.Mode),markers:new MarkerConfiguration(input.Open,input.Close)));
            return new {
                input.Id,source,normalized=new{normalized.Text,normalized.Mappings},analysis.Detection,analysis.ContentEligibility,analysis.Diagnostics,
                regions=analysis.Regions.Select(region=>new {
                    region.ContentSpan,region.ReplacementSpan,region.OriginalContent,region.OriginalReplacement,region.Markers,region.Diagnostics,
                    candidates=region.Candidates.Select(candidate=>new {
                        candidate.Id,candidate.Kind,candidate.Source,candidate.ContentSpan,candidate.ReplacementSpan,candidate.Document,candidate.Diagnostics,candidate.Edits,
                        exported=CandidateExporter.Export(candidate),snapshot=CandidateSetSerializer.Serialize(region.Select(candidate.Id)),
                        roundtrip=CandidateSetSerializer.Serialize(CandidateSetSerializer.Deserialize(CandidateSetSerializer.Serialize(region.Select(candidate.Id))))
                    }).ToArray(),emptySnapshot=region.Candidates.Count==0?CandidateSetSerializer.Serialize(region):null
                }).ToArray()
            };
        } catch(Exception e){return new {input.Id,error=e.GetType().FullName,message=e.Message};}
    }
    [JSInvokable] public static string Parity() => JsonSerializer.Serialize(Inputs().Select(Project).ToArray());
    [JSInvokable] public static async Task<string> Contracts() => await CoreVerification.RunInMemory(Corpus());
    [JSInvokable] public static string Analyze(string raw) => JsonSerializer.Serialize(Project(new ProbeInput("interactive",raw)));
    [JSInvokable] public static async Task<string> Scheduling()
    {
        using var cancellation=new CancellationTokenSource();
        var source=new SourceSnapshot("a"+new string('\u0301',200000));
        cancellation.CancelAfter(1);bool interrupted=false;var watch=Stopwatch.StartNew();
        try{NormalizedSource.Create(source,cancellation.Token);}catch(OperationCanceledException){interrupted=true;}
        double elapsedMs=watch.Elapsed.TotalMilliseconds;bool deliveredOnReturn=cancellation.IsCancellationRequested;
        await Task.Delay(15);
        return JsonSerializer.Serialize(new{host=OperatingSystem.IsBrowser()?"browser":"native",interrupted,elapsedMs,deliveredOnReturn,deliveredAfterYield=cancellation.IsCancellationRequested,meaning="Timing observation only. CancelAfter does not guarantee interruption of synchronous work; browser scheduling is single-threaded. The editor bounds source length before parsing."});
    }
    [JSInvokable] public static string Timing()
    {
        string[] samples=["can2","x mũ 2","x+1/2","(x+1)/(sqrt(2)+3)","1 trên 2"];
        var engine=new AnalysisEngine();
        for(int i=0;i<30;i++)engine.Analyze(new SourceSnapshot(samples[i%samples.Length]),new AnalysisOptions(InputMode.Explicit));
        var times=new List<double>();
        for(int i=0;i<250;i++){var watch=Stopwatch.StartNew();engine.Analyze(new SourceSnapshot(samples[i%samples.Length]),new AnalysisOptions(InputMode.Explicit));times.Add(watch.Elapsed.TotalMilliseconds);}
        times.Sort();return JsonSerializer.Serialize(new {iterations=times.Count,p50Ms=times[times.Count/2],p95Ms=times[(int)(times.Count*.95)-1],maxMs=times[^1],samples,scope="Warm synchronous core analysis of short sources; excludes UI/export/network."});
    }
}
