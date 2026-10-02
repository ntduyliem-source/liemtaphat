using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;

public static class DocumentExportVerification
{
    public static async Task<int> Run(string output)
    {
        Directory.CreateDirectory(output);var checks=new List<object>();int failed=0;
        void Assert(bool value,string reason="Export contract failed"){if(!value)throw new Exception(reason);}
        async Task Check(string name,Func<Task> action){try{await action();checks.Add(new{name,passed=true});}catch(Exception e){failed++;checks.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}}
        var settings=new FormulaSettings(EnabledDomains:DetectionDomains.All,MarkerProfiles:MarkerPreferences.Default);
        async Task<FormulaSession> Ready(string raw){var session=new FormulaSession(new NativeAnalysisScheduler());session.Configure(settings);session.UpdateSource(raw);Assert(await session.AnalyzeContentAsync());return session;}
        XNamespace w="http://schemas.openxmlformats.org/wordprocessingml/2006/main",m=CandidateExporter.OmmlNamespace;
        XDocument ReadDoc(byte[] bytes)
        {
            using var memory=new MemoryStream(bytes);using var zip=new ZipArchive(memory);
            Assert(zip.Entries.Count==6&&zip.GetEntry("[Content_Types].xml")!=null&&zip.GetEntry("_rels/.rels")!=null);
            foreach(var entry in zip.Entries){using var stream=entry.Open();var xml=XDocument.Load(stream);Assert(!xml.Descendants().Attributes().Any(a=>a.Name.LocalName=="TargetMode"&&a.Value=="External"));}
            using var document=zip.GetEntry("word/document.xml")!.Open();return XDocument.Load(document);
        }
        await Check("docx-native-result-prose-keep-text-and-balanced",async()=>{
            string raw="Bài tập Locus\r\nGiữ câu chữ <b> & dấu tab:\tα.\r\nToán lc[x mũ 2 + 1] và lc[1 trên 2].\r\nVận tốc ly-[v=10 m/s].\r\nPhản ứng hoa-[3H2+O2=H2O].\r\nGiữ văn bản hoa-[H2SO4].\r\nĐường dẫn https://example.org/bai và tiếng Việt e\u0301.\r\n";
            using var s=await Ready(raw);var kept=s.State.Content!.Regions.Single(r=>r.Raw=="hoa-[H2SO4]");s.KeepContentText(kept.Id,true);
            s.SetBalanceSelection(s.State.Content.Select(0,raw.Length));await s.RefreshBalanceAsync();await s.BalanceBatchAsync(false);
            var snapshot=s.State;var projection=ContentExport.Capture(snapshot);var bytes=projection.ToDocx();var xml=ReadDoc(bytes);
            Assert(projection.FormulaCount==5&&xml.Descendants(m+"oMath").Count()==5,"Wrong native count "+projection.FormulaCount+": "+string.Join(" | ",s.State.Content.Regions.Select(r=>r.Raw)));
            var text=string.Concat(xml.Descendants(w+"t").Select(t=>t.Value));Assert(text.Contains("Giữ câu chữ <b> & dấu tab:")&&text.Contains("hoa-[H2SO4]")&&text.Contains("e\u0301")&&!text.Contains("lc[x"));
            Assert(xml.Descendants(w+"p").Count()==8&&xml.Descendants(w+"tab").Count()==1,"Paragraph or tab lost");
            Assert(xml.Descendants(m+"oMath").Last().ToString(SaveOptions.DisableFormatting)==XElement.Parse(CandidateExporter.ToOmml(s.State.Content.Regions.Single(r=>r.Raw=="hoa-[3H2+O2=H2O]").Display!)).ToString(SaveOptions.DisableFormatting),"Different equation snapshot");
            Assert(ReferenceEquals(snapshot,s.State)&&snapshot.Raw==raw&&bytes.SequenceEqual(projection.ToDocx()),"Export mutated or nondeterministic");
            await File.WriteAllBytesAsync(Path.Combine(output,"mixed-native.docx"),bytes);await File.WriteAllTextAsync(Path.Combine(output,"mixed-native.html"),projection.ToHtml());
            await File.WriteAllTextAsync(Path.Combine(output,"cross-host.locus"),DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),3,snapshot,new())));
        });
        await Check("selection-reversed-partial-formula-and-plain-text",async()=>{
            using var s=await Ready("Trước lc[x^2] sau hoa-[H2SO4].");var content=s.State.Content!;var first=content.Regions[0];
            var xml=ReadDoc(ContentExport.Capture(s.State,content.Select(first.End,first.Start)).ToDocx());Assert(xml.Descendants(m+"oMath").Count()==1&&!xml.Descendants(w+"t").Any());
            bool refused=false;try{ContentExport.Capture(s.State,content.Select(first.Start+1,first.End));}catch(InvalidOperationException){refused=true;}Assert(refused);
            refused=false;try{ContentExport.Capture(s.State,new(first.Start,first.End,[],[first.Id]));}catch(InvalidOperationException){refused=true;}Assert(refused);
            s.KeepContentText(first.Id,true);xml=ReadDoc(ContentExport.Capture(s.State,s.State.Content!.Select(first.Start+1,first.End-1)).ToDocx());Assert(string.Concat(xml.Descendants(w+"t").Select(t=>t.Value))==first.Raw[1..^1]&&!xml.Descendants(m+"oMath").Any());
            refused=false;try{ContentExport.Capture(s.State,new(0,s.State.Raw.Length,[Guid.NewGuid()],[]));}catch(ArgumentException){refused=true;}Assert(refused);
        });
        await Check("html-escapes-text-mathml-keeps-selected-snapshot",async()=>{
            using var s=await Ready("<script>alert(1)</script>\nCó lc[x^2].");string html=ContentExport.Capture(s.State).ToHtml();Assert(html.Contains("&lt;script&gt;")&&!html.Contains("<script>")&&html.Contains(CandidateExporter.ToMathMl(s.State.Content!.Regions.Single().Display!))&&System.Net.WebUtility.HtmlDecode(html).Contains("\nCó "),html);
        });
        await Check("products-ignore-roundtrip-export-without-reanalysis",async()=>{
            using var s=await Ready("hoa-[3H2+O2=]");await s.RequestAssistanceAsync(0,[new("activation","ignition")]);var proposal=s.AssistanceProposals.Single();Assert(s.TryPrepareContentAssistance(s.LeaseAssistance(proposal.Id),new(s.State.Raw,s.State.Raw.Length,s.State.Raw.Length,false,false,true),out var plan));s.CommitAssistance(plan!);
            s.SetBalanceSelection(s.State.Content!.Select(0,s.State.Raw.Length));await s.RefreshBalanceAsync();await s.BalanceStepAsync();
            string file=DocumentCodec.Serialize(new FormulaDocument(Guid.NewGuid(),1,s.State,new()));var opened=(FormulaDocument)DocumentCodec.Open(file).Document!;
            Assert(opened.State.Content!.Regions[0].KeepFromAuto&&opened.State.Content.Regions[0].ResultOverride!.ProductProposal!=null);
            Assert(ContentExport.Capture(s.State).ToDocx().SequenceEqual(ContentExport.Capture(opened.State).ToDocx()));
            await File.WriteAllTextAsync(Path.Combine(output,"products-ignore.locus"),file);await File.WriteAllBytesAsync(Path.Combine(output,"products-ignore.docx"),ContentExport.Capture(opened.State).ToDocx());
        });
        await Check("legacy-selected-repair-projected-without-parsing",()=>{
            var analysis=AnalysisWire.Analyze(new("x+1/2",7,new()));var choice=analysis.Regions.Single().Candidates.Single(c=>c.Kind=="repair");var state=new FormulaState("x+1/2",7,new(),analysis,0,choice.Id);
            var xml=ReadDoc(ContentExport.Capture(state).ToDocx());Assert(xml.Descendants(m+"oMath").Single().ToString(SaveOptions.DisableFormatting)==XElement.Parse(CandidateExporter.ToOmml(choice)).ToString(SaveOptions.DisableFormatting));return Task.CompletedTask;
        });
        await Check("cancel-xml-controls-and-surrogate-selection-refused",async()=>{
            using var s=await Ready("Văn bản 🧪");using var cancel=new CancellationTokenSource();cancel.Cancel();bool refused=false;try{ContentExport.Capture(s.State).ToDocx(cancel.Token);}catch(OperationCanceledException){refused=true;}Assert(refused);
            refused=false;try{ContentExport.Capture(s.State,new(0,s.State.Raw.Length-1,[],[]));}catch(ArgumentException){refused=true;}Assert(refused);
            var invalid=new FormulaState("a\0b",1,new());refused=false;try{ContentExport.Capture(invalid).ToDocx();}catch(XmlException){refused=true;}Assert(refused&&invalid.Raw=="a\0b");
        });
        await Check("long-document-export-measurement",async()=>{
            string raw=string.Join('\n',Enumerable.Repeat("Đoạn văn bảo toàn nguồn và công thức lc[x^2+1].",100));using var s=await Ready(raw);var watch=Stopwatch.StartNew();var bytes=ContentExport.Capture(s.State).ToDocx();watch.Stop();Assert(ReadDoc(bytes).Descendants(m+"oMath").Count()==100);
            await File.WriteAllTextAsync(Path.Combine(output,"export-timing.json"),JsonSerializer.Serialize(new{rawUtf16=raw.Length,formulas=100,elapsedMs=watch.Elapsed.TotalMilliseconds,bytes=bytes.Length,host="native .NET Release",scope="Single local measurement; not a latency guarantee"}));
        });
        var report=new{status=failed==0?"PASSED":"FAILED",total=checks.Count,failed,checks};await File.WriteAllTextAsync(Path.Combine(output,"document-export-tests.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{report.status,report.total,report.failed}));return failed==0?0:1;
    }
}
