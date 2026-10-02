using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Locus.Application;
using Locus.Core.Plotting;

internal static class PlotVerification
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory); var checks = new List<object>(); var timings = new List<object>(); int failed = 0;
        void Check(string name, Action action) { try { action(); checks.Add(new { name, passed = true }); } catch(Exception e) { failed++; checks.Add(new { name, passed = false, error = e.ToString() }); Console.Error.WriteLine($"FAIL {name}: {e.Message}"); } }
        void Assert(bool value, string reason = "Contract failed") { if(!value) throw new Exception(reason); }
        void Near(double value, double expected) => Assert(Math.Abs(value - expected) < 1e-10, $"{value} != {expected}");
        PlotExpression Parse(string source) => PlotExpression.Parse(source);
        double Eval(string source, double x = 2) => Parse(source).Bind(new Dictionary<string,double>()).Evaluate(x);
        PlotDocument Doc(string source) => new(Guid.NewGuid(),0,[new("f",source)],new(),[],new(),PlotExpression.Grammar);
        Check("vietnamese-nfd-original-spans",()=>{ var raw="y = x mu\u0303 2 cộng căn(4)"; var e=Parse(raw);Assert(e.Raw==raw&&e.Root.End==raw.Length);Near(e.Bind(new Dictionary<string,double>()).Evaluate(3),11);Assert(e.MathMl.Contains("msqrt")&&e.Latex.Contains("\\sqrt")); });
        Check("precedence-power-unary-and-direct-fraction",()=>{Near(Eval("-x^2"),-4);Near(Eval("2^3^2"),512);Near(Eval("x+1/2"),2.5);Near(Eval("can2"),Math.Sqrt(2));Near(Eval("2x+3(x+1)"),13);});
        Check("functions-constants-decimal-comma",()=>{Near(Eval("sin(pi/2)+cos(0)+ln(e)"),3);Near(Eval("exp(0)+abs(-3)+1,5"),5.5);});
        Check("undefined-zero-power-and-ast-cache",()=>{Assert(double.IsNaN(Eval("0^0")));Assert(ReferenceEquals(Parse("a*x"),Parse("a*x")));var sample=PlotWire.Analyze(new(Doc("x^0"),1)).Curves[0];Assert(sample.Paths.Length>=2&&sample.Paths.All(p=>!(p[0][0]<0&&p[^1][0]>0)));});
        Check("case-sensitive-indexed-parameters-unassigned",()=>{var result=PlotWire.Analyze(new(Doc("a_100*x+A*x+a"),1));Assert(result.Curves[0].Parameters.SequenceEqual(new[]{"a_100","A","a"}));Assert(result.Curves[0].Problem!=null&&result.Curves[0].Paths.Length==0);});
        Check("direct-first-defensive-alternative-and-stable-ids",()=>{
            var first=PlotWire.Analyze(new(Doc("x+1/2"),1)).Curves[0];var second=PlotWire.Analyze(new(Doc("x+1/2"),2)).Curves[0];
            Assert(first.Readings.Length==2&&first.Readings[0].Kind=="direct"&&first.Readings[1].Kind=="alternative"&&first.SelectedReadingId==first.Readings[0].Id&&first.Paths.Length>0);
            Assert(first.Readings.Select(r=>r.Id).SequenceEqual(second.Readings.Select(r=>r.Id))&&first.Readings[1].Raw=="(x+1)/(2)");
            var alternative=first.Readings[1];var selected=Doc("x+1/2") with{Curves=[new("f","x+1/2",InterpretedRaw:alternative.Raw,InterpretationKind:alternative.Kind)]};
            var changed=PlotWire.Analyze(new(selected,3)).Curves[0];Assert(changed.SelectedReadingId==alternative.Id&&changed.Paths.Length>0&&changed.Latex.Contains("\\frac"));
        });
        Check("ambiguous-fraction-requires-explicit-choice",()=>{
            var document=Doc("1/2x");var pending=PlotWire.Analyze(new(document,1)).Curves[0];
            Assert(pending.Readings.Length==2&&pending.Readings.All(r=>r.Kind=="alternative")&&pending.SelectedReadingId==null&&pending.Paths.Length==0&&pending.Problem!.Contains("mơ hồ"));
            var choice=pending.Readings[1];document=document with{Curves=[document.Curves[0] with{InterpretedRaw=choice.Raw,InterpretationKind=choice.Kind}]};
            var accepted=PlotWire.Analyze(new(document,2)).Curves[0];Assert(accepted.SelectedReadingId==choice.Id&&accepted.Problem==null&&accepted.Paths.Length>0);
        });
        Check("repair-never-auto-selected-and-source-remains",()=>{
            var document=Doc("sqrt(x");var pending=PlotWire.Analyze(new(document,1)).Curves[0];
            Assert(pending.Readings.Length==1&&pending.Readings[0].Kind=="repair"&&pending.SelectedReadingId==null&&pending.Paths.Length==0&&pending.Problem!.Contains("chưa áp dụng"));
            var repair=pending.Readings[0];document=document with{Curves=[document.Curves[0] with{InterpretedRaw=repair.Raw,InterpretationKind=repair.Kind}]};
            var accepted=PlotWire.Analyze(new(document,2)).Curves[0];Assert(document.Curves[0].Raw=="sqrt(x"&&repair.Raw=="sqrt(x)"&&accepted.Paths.Length>0);
            var typo=PlotWire.Analyze(new(Doc("sni(x)"),3)).Curves[0];Assert(typo.Readings.Length==0&&typo.Paths.Length==0);
        });
        foreach(var raw in new[]{"sni(x)","x=2","x^2+y^2=1","sqrt(x","1/2x","x;alert(1)","sin x"}) Check("reject-"+raw,()=>{try {Parse(raw);throw new Exception("Accepted invalid/ambiguous expression");} catch(PlotParseException e){Assert(e.Start>=0&&e.End<=raw.Length);}});
        Check("real-domain-and-poles-no-bridge",()=>{
            foreach(var (raw,poles) in new[]{("1/x",new[]{0d}),("1/(x-0.17)^2",new[]{0.17}),("tan(x)",new[]{-Math.PI/2,Math.PI/2}),("(x^2-1)/(x-1)",new[]{1d})})
            {var r=PlotWire.Analyze(new(Doc(raw),1)).Curves[0];Assert(r.Paths.Length>0&&!r.Limited,raw);foreach(var path in r.Paths)foreach(var pole in poles)Assert(!(path[0][0]<pole&&path[^1][0]>pole),raw+" bridged "+pole);}
            var sqrt=PlotWire.Analyze(new(Doc("sqrt(x)"),1)).Curves[0];Assert(!sqrt.Limited&&sqrt.Paths.Length>0&&sqrt.Paths.SelectMany(p=>p).All(p=>p[0]>=0));
            var none=PlotWire.Analyze(new(Doc("sqrt(-1)"),1)).Curves[0];Assert(!none.Limited&&none.Paths.Length==0);
        });
        Check("singularity-independent-of-sign-change",()=>{var r=PlotWire.Analyze(new(Doc("1/(x^2-2)^2"),1)).Curves[0];foreach(var p in r.Paths)foreach(var root in new[]{Math.Sqrt(2),-Math.Sqrt(2)})Assert(!(p[0][0]<root&&p[^1][0]>root));});
        Check("oscillation-alias-cannot-appear-as-flat-line",()=>{var d=Doc("sin(192*pi*x)") with{Viewport=new(0,2,1,-2)};var r=PlotWire.Analyze(new(d,1)).Curves[0];Assert(r.Limited||r.Paths.SelectMany(p=>p).Any(p=>Math.Abs(p[1])>0.8));});
        Check("domain-independent-of-viewport",()=>{var d=Doc("x") with{Curves=[new("f","x",DomainMin:1,DomainMax:2)]};var r=PlotWire.Analyze(new(d,0));Assert(r.Curves[0].Paths.SelectMany(p=>p).All(p=>p[0]>=1&&p[0]<=2));});
        Check("cancel-and-resource-bound",()=>{using var c=new CancellationTokenSource();c.Cancel();try{PlotWire.Analyze(new(Doc("sin(x)"),1),c.Token);throw new Exception("No cancellation");}catch(OperationCanceledException){} var e=Parse("sin(100000*x)").Bind(new Dictionary<string,double>());var sample=PlotSampler.Sample(e,-5,5,-5,5);Assert(sample.Evaluations<=12002&&sample.Limited);});
        foreach(int count in new[]{10,100,300}) Check("parameters-"+count,()=>{
            string raw=string.Join("+",Enumerable.Range(1,count).Select(i=>$"a_{i}*x"));var d=Doc(raw) with{Parameters=Enumerable.Range(1,count).Select(i=>new PlotParameter("a_"+i,1d/count)).ToArray()};
            var watch=Stopwatch.StartNew();var reply=PlotWire.Analyze(new(d,9));watch.Stop();timings.Add(new{count,elapsedMs=watch.Elapsed.TotalMilliseconds});Assert(reply.Curves[0].Parameters.Length==count&&reply.Curves[0].Problem==null&&!reply.Curves[0].Limited);Near(Parse(raw).Bind(d.Parameters!.ToDictionary(p=>p.Name,p=>p.Value!.Value)).Evaluate(2),2);
            var session=new PlotSession();session.Load(d);File.WriteAllText(Path.Combine(directory,$"parameters-{count}.locus"),session.Serialize());
        });
        Check("slider-transaction-cancel-undo-redo-exact-values",()=>{
            var s=new PlotSession();s.AddCurve("a*x");s.UpdateParameter(new("a",0.123456789,-1,1,0.1));var before=s.Document;s.BeginGesture();for(int i=0;i<40;i++)s.UpdateParameter(new("a",i/10d,-1,1));s.EndGesture();Assert(s.Document.Parameters![0].Value==3.9);Assert(s.History(false)&&s.Document==before);Assert(s.History(true)&&s.Document.Parameters![0].Value==3.9);s.BeginGesture();s.UpdateParameter(new("a",10));s.EndGesture(true);Assert(s.Document.Parameters![0].Value==3.9);
        });
        Check("version10-roundtrip-source-interpretation-ast-all-settings",()=>{
            var s=new PlotSession();s.AddCurve("a_1*x mũ 2");s.UpdateParameter(new("a_1",7,-2,2,0.25,true));s.UpdateCurve(s.Document.Curves[0] with{Color="#aabbcc",Width=4,LineStyle="dotted",DomainMin=-2,DomainMax=3});s.Apply(s.Document with{Viewport=new(-4,8,10,-2),Axes=new(false,true,"thời gian","giá trị")});
            var json=s.Serialize();Assert(json.Contains("\"version\":10"));var opened=(PlotDocument)DocumentCodec.Open(json).Document!;Assert(opened.Curves[0].AstSnapshot==Parse(opened.Curves[0].Raw).Snapshot&&opened.Parameters![0].Value==7&&opened.Parameters[0].Pinned);Assert(DocumentCodec.Serialize(opened)==json);
            File.WriteAllText(Path.Combine(directory,"plot.locus"),json);
            var node=JsonNode.Parse(json)!;var payload=JsonNode.Parse(node["payload"]!.GetValue<string>())!;payload["curves"]![0]!["raw"]="x^3";var altered=payload.ToJsonString();node["payload"]=altered;node["sha256"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(altered)));var bad=node.ToJsonString();Assert(DocumentCodec.Open(bad).Document==null&&DocumentCodec.Open(bad).OriginalJson==bad);
            node=JsonNode.Parse(json)!;node["version"]=1;Assert(!DocumentCodec.Open(node.ToJsonString()).IsSupported);
        });
        Check("version10-selected-reading-roundtrip-tamper-and-v8-migration",()=>{
            var s=new PlotSession();s.AddCurve("1/2x");var offered=PlotWire.Analyze(new(s.Document,1)).Curves[0].Readings[0];s.UpdateCurve(s.Document.Curves[0] with{InterpretedRaw=offered.Raw,InterpretationKind=offered.Kind});
            var json=s.Serialize();var opened=(PlotDocument)DocumentCodec.Open(json).Document!;Assert(opened.Curves[0].Raw=="1/2x"&&opened.Curves[0].InterpretedRaw==offered.Raw&&opened.Curves[0].AstSnapshot==Parse(offered.Raw).Snapshot&&DocumentCodec.Serialize(opened)==json);
            var node=JsonNode.Parse(json)!;var payload=JsonNode.Parse(node["payload"]!.GetValue<string>())!;payload["curves"]![0]!["raw"]="x+2";var altered=payload.ToJsonString();node["payload"]=altered;node["sha256"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(altered)));Assert(!DocumentCodec.Open(node.ToJsonString()).IsSupported);
            var direct=new PlotSession();direct.AddCurve("x^2");var legacy=JsonNode.Parse(direct.Serialize())!;var legacyPayload=JsonNode.Parse(legacy["payload"]!.GetValue<string>())!;legacyPayload["curves"]![0]!.AsObject().Remove("interpretedRaw");legacyPayload["curves"]![0]!.AsObject().Remove("interpretationKind");var legacyText=legacyPayload.ToJsonString();legacy["version"]=8;legacy["payload"]=legacyText;legacy["sha256"]=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(legacyText)));Assert(DocumentCodec.Open(legacy.ToJsonString()).Document is PlotDocument migrated&&migrated.Curves[0].InterpretedRaw==null);
        });
        Check("legacy-v1-plot-open-with-defaults",()=>{
            var id=Guid.NewGuid();var payload=JsonSerializer.Serialize(new{id,revision=1,curves=new[]{new{id="f",raw="x^2",color="#28705c",lineStyle="solid",visible=true}},viewport=new{left=-5,top=5,right=5,bottom=-5}});var json=JsonSerializer.Serialize(new{format=DocumentCodec.Format,version=1,kind="plot",id,revision=1,payload,sha256=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))});Assert(DocumentCodec.Open(json).Document is PlotDocument old&&old.Curves[0].Width==2.5&&old.Parameters==null);
        });
        Check("invalid-source-saveable-and-stale-path-absent",()=>{var s=new PlotSession();s.AddCurve("x^2");s.UpdateCurve(s.Document.Curves[0] with{Raw="sqrt("});var json=s.Serialize();Assert(DocumentCodec.Open(json).Document is PlotDocument);var r=PlotWire.Analyze(new(s.Document,8));Assert(r.Version==8&&r.Curves[0].Paths.Length==0&&!r.Svg.Contains("data-curve"));});
        Check("svg-source-of-preview-and-export-style-escaping",()=>{var d=Doc("x") with{Curves=[new("f","x","#aabbcc","dashed",Width:4)],Axes=new(true,true,"<script>","y")};var r=PlotWire.Analyze(new(d,2));Assert(r.Svg.Contains("#aabbcc")&&r.Svg.Contains("stroke-dasharray=\"10 6\"")&&r.Svg.Contains("&lt;script&gt;")&&!r.Svg.Contains("<script>"));File.WriteAllText(Path.Combine(directory,"plot.svg"),r.Svg);});
        var report=new{passed=checks.Count-failed,failed,checks,timings};File.WriteAllText(Path.Combine(directory,"plot-verification.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{passed=checks.Count-failed,failed,timings}));return failed==0?0:1;
    }
}
