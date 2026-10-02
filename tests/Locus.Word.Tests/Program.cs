using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Serialization;
using Locus.Word;
using WordApi = Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static WordApi.Application app = null!;
    private static string directory = "";
    private static readonly JavaScriptSerializer json = new() { MaxJsonLength = 16 * 1024 * 1024 };
    private static readonly List<object> results = new();
    private static readonly List<WordApi.Document> documents = new();
    private static int failures;
    private static int observations;
    [STAThread] private static int Main(string[] args)
    {
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="manual-panel")return RunManualPanel(args);
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="science-panel")return RunManualPanel(args);
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="smart-contract")return RunSmartContracts(args);
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="scan-contract")return RunScanContracts(args);
        if (Process.GetProcessesByName("WINWORD").Length != 0) { Console.Error.WriteLine("Refusing existing Word."); return 2; }
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="scan-demo")return RunScanDemo(args);
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="manual-demo")return RunManualDemo(args);
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="desktop")return DesktopLifecycle.Run(args);
        directory = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/w0/adapter-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")); Directory.CreateDirectory(directory);
        DateTime started = DateTime.UtcNow;
        try
        {
            Stage("create-word"); app = new WordApi.Application();
            if (!string.Equals(Path.GetFileName(app.Path), "Office16", StringComparison.OrdinalIgnoreCase)) throw new Exception("Unexpected Office path.");
            var word = Process.GetProcessesByName("WINWORD").Single(p => p.StartTime.ToUniversalTime() >= started.AddSeconds(-2));
            Write("ownership.json", new { pid=word.Id, startTimeUtc=word.StartTime.ToUniversalTime().ToString("o"), path=word.MainModule.FileName, app.Version, app.Build });
            app.Visible=false; app.DisplayAlerts=WordApi.WdAlertLevel.wdAlertsNone;
            if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="manual")RunManual();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="science")RunScience();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="docx-export")RunDocxExport();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="smart-native")RunSmartNative();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="scan-native")RunScanNative();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="scan-transactions")RunScanTransactions();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="smart-transactions")RunSmartTransactions();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="lifecycle")RunLifecycle();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="continuation-research")RunContinuationResearch();
            else if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="ux-research")RunUxResearch();
            else Run();
        }
        catch(Exception error) { failures++; results.Add(new {id="harness",status="FAIL",error=error.ToString()}); }
        finally
        {
            foreach(var doc in documents.ToArray()) { try { Close(doc); } catch { } }
            if(app != null) { try { if(app.Documents.Count == 0) app.Quit(WordApi.WdSaveOptions.wdDoNotSaveChanges); } catch { } Marshal.FinalReleaseComObject(app); }
            Write("report.json", new {capturedAtUtc=DateTime.UtcNow.ToString("o"),suite=Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")??"adapter",passed=results.Count-failures-observations,failed=failures,observed=observations,results,limits=new[]{"Synthetic documents; mutations execute inside the installed add-in UI thread. Observation/fixture setup use Word COM APIs.","Clipboard and typing in this runner use Word APIs, not native keyboard injection.","OBSERVED rows are research findings, not feature acceptance. UX research is separate from participant acceptance and production auto permission."}});
        }
        Console.WriteLine($"Word adapter: {results.Count-failures-observations} PASS / {failures} FAIL / {observations} OBSERVED; {directory}"); return failures == 0 ? 0 : 1;
    }
    private static void Write(string name,object value) => File.WriteAllText(Path.Combine(directory,name),json.Serialize(value));
    private static void Stage(string name) { Write("progress.json",new{stage=name,time=DateTime.UtcNow.ToString("o")}); Console.WriteLine(name); }
    private static void Check(bool pass,string message) { if(!pass) throw new Exception(message); }
    private static void Test(string id,Action action)
    {
        string? filter=Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_FILTER"); if(!string.IsNullOrEmpty(filter)&&id.IndexOf(filter,StringComparison.OrdinalIgnoreCase)<0)return;
        Stage(id);
        try { action();bool observed=id.StartsWith("observation/",StringComparison.Ordinal);if(observed)observations++;results.Add(new{id,status=observed?"OBSERVED":"PASS"}); }
        catch(Exception error) { failures++; results.Add(new{id,status="FAIL",error=error.ToString()}); Console.WriteLine("FAIL: "+error.Message); }
        finally {foreach(var doc in documents.ToArray()) Close(doc); Write("partial.json",results);}
    }
    private static WordApi.Document New(string raw)
    {
        WordApi.Document doc;
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="lifecycle") {doc=app.Documents.Add();doc.Content.Text="Before: "+raw+" After.\r";}
        else {dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;connector.CreateSandbox("Before: "+raw+" After.");doc=app.ActiveDocument;}
        documents.Add(doc);doc.Activate();
        app.Selection.SetRange(8,8+raw.Length); doc.UndoClear(); return doc;
    }
    private static void Close(WordApi.Document doc) { doc.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges); documents.Remove(doc); Marshal.FinalReleaseComObject(doc); }
    private static ManagedSnapshot Snapshot(string raw,int choice=0) { var region=new AnalysisEngine().Analyze(new SourceSnapshot(raw),new AnalysisOptions(raw.StartsWith("lc[",StringComparison.Ordinal)?InputMode.Markers:InputMode.Explicit)).Regions.Single(); return new ManagedSnapshot(region.Select(region.Candidates[choice].Id)); }
    private static string State(WordApi.Document doc) => json.Serialize(new {text=doc.Content.Text,math=doc.OMaths.Count,tags=doc.ContentControls.Cast<WordApi.ContentControl>().Select(c=>c.Tag).ToArray()});
    private static void Reject(Action action)
    { try {action();} catch(Exception e) when(e is InvalidOperationException || e is FormatException || e is ArgumentException || e is COMException ce && (ce.ErrorCode==unchecked((int)0x80131509)||ce.ErrorCode==unchecked((int)0x80131537)||ce.ErrorCode==unchecked((int)0x80070057))){return;} throw new Exception("Expected refusal."); }
    private static string Convert(ManagedSnapshot snapshot,string fault="") {dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;return connector.RunSandboxResearchOperation("convert",snapshot.Encode(),"",fault);}
    private static void Restore(WordApi.ContentControl control,string fault="") {dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;connector.RunSandboxResearchOperation("restore","",control.ID,fault);}
    private static void Detach(WordApi.ContentControl control) {dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;connector.RunSandboxResearchOperation("detach","",control.ID,"");}
    private static void Run()
    {
        foreach(string raw in new[]{"x mũ 2","x mu\u0303 2","x+1/2","(x+1)/(sqrt(2)+3)","can2","căn x cộng 1"})
        Test("undo/"+raw,()=>
        {
            var doc=New(raw); var before=State(doc); int start=app.Selection.Start,end=app.Selection.End;
            var snapshot=Snapshot(raw); Convert(snapshot);
            var converted=State(doc); Check(doc.OMaths.Count==1,"Missing native equation.");
            var decoded=WordOperations.ReadUnique(doc,doc.ContentControls[1]); Check(decoded.Candidates.OriginalReplacement==raw && decoded.Candidates.Candidates.Count==snapshot.Candidates.Candidates.Count,"Snapshot lost source or candidates.");
            Check(doc.Undo(1) && State(doc)==before,"Undo did not restore document state.");
            Check(app.Selection.Start==start && app.Selection.End==end,"Undo selection differs.");
            Check(doc.Redo(1) && State(doc)==converted,"Redo document differs.");
        });
        foreach(string fault in new[]{"InsertReturnedBeforeFlag","NativeInserted","ControlAdded","TagWritten","SelectionMoved"})
        Test("rollback/"+fault,()=>
        {
            var doc=New("x+1/2"); string before=State(doc); int start=app.Selection.Start,end=app.Selection.End;
            Reject(()=>Convert(Snapshot("x+1/2",1),fault));
            Check(State(doc)==before && app.Selection.Start==start && app.Selection.End==end,"Rollback left partial content/selection.");
        });
        Test("metadata/corruption-version-and-payload-before-write",()=>
        {
            var doc=New("x^2"); string before=State(doc); var snapshot=Snapshot("x^2"); string encoded=snapshot.Encode();
            Reject(()=>ManagedSnapshot.Decode(encoded.Replace("locus:word:1:","locus:word:2:")));
            Reject(()=>ManagedSnapshot.Decode(encoded.Substring(0,encoded.Length-3)+"BAD"));
            Reject(()=>ManagedSnapshot.Decode(new string('x',ManagedSnapshot.MaxTagCodeUnits+1)));
            Check(State(doc)==before,"Metadata refusal modified document.");
        });
        Test("guard/source-changed-and-target-changed",()=>
        {
            var doc=New("x^3"); var before=State(doc); Reject(()=>Convert(Snapshot("x^2"))); Check(State(doc)==before,"Source mismatch wrote.");
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;
            RejectReason(()=>connector.RunSandboxResearchOperation("target-change-probe",Snapshot("x^3").Encode(),"",""),"target-changed");Check(State(doc)==before,"Changed target wrote.");
            app.Selection.SetRange(8,11);RejectReason(()=>WordOperations.ConvertSelected(app,Snapshot("x^3"),()=>{}),"run-on-word-ui-thread");Check(State(doc)==before,"Out-of-process transaction was allowed.");
        });
        Test("metadata/payload-budget-and-missing-association",()=>
        {
            string raw="x^2";for(int depth=0;depth<7;depth++)raw="("+raw+")+("+raw+")";var doc=New(raw);string before=State(doc);
            var snapshot=Snapshot(raw);RejectReason(()=>Convert(snapshot),"Snapshot exceeds");Check(State(doc)==before,"Oversized payload wrote source.");
            Close(doc);doc=New("x^2");Convert(Snapshot("x^2"));doc.ContentControls[1].Tag="";before=State(doc);
            Reject(()=>Restore(doc.ContentControls[1]));Check(State(doc)==before&&doc.OMaths.Count==1,"Missing association destroyed native.");
        });
        Test("clipboard/same-document-duplicate-and-formattedtext",()=>
        {
            var doc=New("x^2");Convert(Snapshot("x^2"));doc.Content.Copy();var destination=doc.Range(doc.Content.End-1,doc.Content.End-1);destination.Paste();
            Check(doc.OMaths.Count==2&&doc.ContentControls.Count==2,"Normal Word clipboard lost the copied equation/control.");string before=State(doc);
            RejectReason(()=>WordOperations.ReadUnique(doc,doc.ContentControls[1]),"duplicate-entry-id");Check(State(doc)==before,"Duplicate refusal wrote.");
            var other=New("temporary");other.Content.FormattedText=doc.ContentControls[1].Range.FormattedText;
            Check(other.OMaths.Count==1,"FormattedText copy lost native.");Write("formattedtext.json",new{other.ContentControls.Count,native=other.OMaths.Count,meaning="Association may be missing; never recreate source from current parser."});
        });
        Test("guard/track-changes",()=>
        {
            var doc=New("x^2"); doc.TrackRevisions=true; var before=State(doc); Reject(()=>Convert(Snapshot("x^2"))); Check(State(doc)==before && doc.TrackRevisions,"Changed tracked document or disabled tracking.");
        });
        Test("guard/protected",()=>
        {
            var doc=New("x^2"); doc.Protect(WordApi.WdProtectionType.wdAllowOnlyReading,NoReset:true,Password:"w0-fixture"); var before=State(doc);
            Reject(()=>Convert(Snapshot("x^2"))); Check(State(doc)==before && doc.ProtectionType!=WordApi.WdProtectionType.wdNoProtection,"Changed protected document.");
        });
        Test("guard/existing-control",()=>
        {
            var doc=New("x^2"); doc.ContentControls.Add(WordApi.WdContentControlType.wdContentControlRichText,app.Selection.Range); var before=State(doc);
            Reject(()=>Convert(Snapshot("x^2"))); Check(State(doc)==before,"Nested existing control.");
        });
        Test("guard/header-and-table",()=>
        {
            var doc=New("x^2"); var header=doc.Sections[1].Headers[WordApi.WdHeaderFooterIndex.wdHeaderFooterPrimary].Range; header.Text="x^2"; header.SetRange(header.Start,header.Start+3); header.Select();
            Check(WordOperations.Refusal(doc,app.Selection)=="non-main-story","Header admitted.");
            var table=doc.Tables.Add(doc.Range(0,0),1,1); table.Cell(1,1).Range.Text="x^2"; var cell=table.Cell(1,1).Range; cell.SetRange(cell.Start,cell.Start+3); cell.Select();
            Check(WordOperations.Refusal(doc,app.Selection)=="table","Table admitted.");
        });
        Test("metadata/multiple-equations-and-duplicate-id",()=>
        {
            var doc=New("x^2"); Convert(Snapshot("x^2")); var first=doc.ContentControls[1];
            var range=doc.Range(doc.Content.End-1,doc.Content.End-1); range.Text=" y^3"; range.SetRange(range.Start+1,range.End); range.Select();
            Convert(Snapshot("y^3")); Check(doc.ContentControls.Count==2,"Second equation missing.");
            var second=doc.ContentControls[2]; WordOperations.ReadUnique(doc,first); WordOperations.ReadUnique(doc,second);
            second.Tag=first.Tag; string before=State(doc); Reject(()=>WordOperations.ReadUnique(doc,first)); Check(State(doc)==before,"Duplicate association mutated native.");
        });
        Test("restore-detach-undo-redo",()=>
        {
            var doc=New("x+1/2"); string original=State(doc); Convert(Snapshot("x+1/2",1)); string converted=State(doc);
            var traceControl=doc.ContentControls[1]; Write("convert-boundaries.json",new{original,converted,control=new{traceControl.Range.Start,traceControl.Range.End,traceControl.Range.Text},before=doc.Range(0,traceControl.Range.Start).Text,after=doc.Range(traceControl.Range.End,doc.Content.End).Text});
            Restore(doc.ContentControls[1]); Write("restore-states.json",new{original,restored=State(doc)}); Check(State(doc)==original,"Restore differs."); bool undone=doc.Undo(1);Write("restore-undo.json",new{converted,actual=State(doc)});Check(undone&&State(doc)==converted,"Undo restore differs."); Check(doc.Redo(1)&&State(doc)==original,"Redo restore differs.");
            app.Selection.SetRange(8,13); Convert(Snapshot("x+1/2",1)); converted=State(doc);
            Detach(doc.ContentControls[1]); string detached=State(doc); Check(doc.OMaths.Count==1&&doc.ContentControls.Count==0,"Detach removed native.");
            Check(doc.Undo(1)&&State(doc)==converted,"Undo detach differs."); Check(doc.Redo(1)&&State(doc)==detached,"Redo detach differs.");
        });
        Test("clipboard/cross-document-control-and-source",()=>
        {
            var doc=New("x+1/2"); var snapshot=Snapshot("x+1/2",1); Convert(snapshot);
            doc.Content.Copy(); var target=New("temporary"); target.Content.Paste(); Check(target.OMaths.Count==1,"Clipboard lost equation.");
            Write("clipboard.json",new{controls=target.ContentControls.Count,math=target.OMaths.Count});
            if(target.ContentControls.Count==1) Check(WordOperations.ReadUnique(target,target.ContentControls[1]).Encode()==snapshot.Encode(),"Copied metadata differs.");
        });
        foreach(string fault in new[]{"RestoreReturnedBeforeFlag","SourceRestored","ControlDetached"})
        Test("restore-rollback/"+fault,()=>{var doc=New("x+1/2");Convert(Snapshot("x+1/2",1));string before=State(doc);Reject(()=>Restore(doc.ContentControls[1],fault));Write("restore-fault-"+fault+".json",new{before,after=State(doc)});Check(State(doc)==before,"Restore fault left a partial write.");});
        foreach(string surrounding in new[]{""," ","  ","\t","😀 e\u0301 "})
        Test("outside-and-restore/"+surrounding,()=>
        {
            var doc=New("x mũ 2");doc.Content.Text=surrounding+"x mũ 2"+surrounding+"End.\r";
            var source=doc.Content;Check(source.Find.Execute(FindText:"x mũ 2",MatchCase:true,MatchWildcards:false),"Source not found.");source.Select();doc.UndoClear();
            string before=State(doc);int start=app.Selection.Start,end=app.Selection.End;string prefix=doc.Range(0,start).Text,suffix=doc.Range(end,doc.Content.End).Text;
            Convert(Snapshot("x mũ 2"));var control=doc.ContentControls[1];
            Check(doc.Range(0,control.Range.Start).Text==prefix&&doc.Range(control.Range.End,doc.Content.End).Text==suffix,"Conversion changed outside text.");
            Restore(control);Check(State(doc)==before,"Restore changed outside/source.");
        });
        Test("native-drift-text-and-structure",()=>
        {
            var doc=New("x^2");Convert(Snapshot("x^2"));var control=doc.ContentControls[1];
            var exponent=control.Range.OMaths[1].Functions[1].ScrSup.Sup;exponent.Range.Text="3";string before=State(doc);
            Reject(()=>Restore(control));Check(State(doc)==before,"Stale native was overwritten.");
            Check(NativeMathSignature.FromXml(Locus.Core.Export.CandidateExporter.ToOmml(Snapshot("x+1/2").Selected))!=NativeMathSignature.FromXml(Locus.Core.Export.CandidateExporter.ToOmml(Snapshot("x+1/2",1).Selected)),"Different fraction scopes compared equal.");
        });
        Test("save-reopen-complete-snapshot",()=>
        {
            var doc=New("x mu\u0303 2"); var snapshot=Snapshot("x mu\u0303 2"); Convert(snapshot);
            string path=Path.Combine(directory,"managed.docx"); Stage("save-reopen/SaveAs2"); doc.SaveAs2(path,WordApi.WdSaveFormat.wdFormatXMLDocument,AddToRecentFiles:false); Close(doc);
            Stage("save-reopen/Open"); doc=app.Documents.Open(path,ReadOnly:true,AddToRecentFiles:false); documents.Add(doc);
            Check(doc.OMaths.Count==1 && WordOperations.ReadUnique(doc,doc.ContentControls[1]).Encode()==snapshot.Encode(),"Reopened snapshot differs.");
            Check(WordOperations.Refusal(doc,app.Selection)=="read-only","Read-only document not refused.");
        });
        Test("native-drift/beyond-control-boundary",()=>
        {
            var doc=New("x^2");Convert(Snapshot("x^2"));var control=doc.ContentControls[1];
            int boundary=doc.OMaths[1].Range.End;
            // Enter the old math-side edge first. SetRange at the unchanged (fixed) caret
            // otherwise preserves plain-text affinity and does not reproduce the old drift.
            app.Selection.SetRange(control.Range.End,control.Range.End);
            app.Selection.SetRange(boundary,boundary);app.Selection.TypeText(" cộng 1");
            string before=State(doc);
            RejectReason(()=>WordOperations.ReadUnique(doc,control),"native-content-changed");
            RejectReason(()=>Restore(control),"native-content-changed");
            Check(State(doc)==before,"Expanded native was overwritten by historical source.");
        });
        Test("outside-formatting-and-marker-restore",()=>
        {
            var doc=New("lc[x mũ 2]");doc.Range(0,7).Font.Bold=-1;doc.Range(0,7).Font.Color=WordApi.WdColor.wdColorBlue;doc.Range(18,doc.Content.End-1).Font.Italic=-1;
            string before=State(doc);Convert(Snapshot("lc[x mũ 2]"));Restore(doc.ContentControls[1]);
            Write("outside-format.json",new{before,after=State(doc),bold=doc.Range(0,7).Font.Bold,color=doc.Range(0,7).Font.Color,italic=doc.Range(18,doc.Content.End-1).Font.Italic});
            Check(State(doc)==before&&doc.Range(0,7).Font.Bold==-1&&doc.Range(0,7).Font.Color==WordApi.WdColor.wdColorBlue&&doc.Range(18,doc.Content.End-1).Font.Italic==-1,"Outside formatting or marker source changed.");
        });
        Test("observation/native-continuation-api",()=>
        {
            var doc=New("x mũ 2");Convert(Snapshot("x mũ 2"));var control=doc.ContentControls[1];string before=State(doc);int caret=app.Selection.Start;
            var cursor=new{app.Selection.Start,app.Selection.End,math=app.Selection.OMaths.Count,font=app.Selection.Font.Name,nativeStart=doc.OMaths[1].Range.Start,nativeEnd=doc.OMaths[1].Range.End,controlStart=control.Range.Start,controlEnd=control.Range.End};
            Write("caret-boundaries.json",Enumerable.Range(control.Range.End-1,5).Select(p=>new{start=p,end=p+1,text=doc.Range(p,p+1).Text,math=doc.Range(p,p+1).OMaths.Count}).ToArray());
            app.Selection.TypeText(" cộng 1");var after=new{state=State(doc),controlText=control.Range.Text,caret=app.Selection.Start,math=doc.OMaths.Count};
            bool literalText=doc.Content.Text.Contains("cộng 1");
            Write("continuation-api.json",new{status=literalText?"OBSERVED_LITERAL":"UNRESOLVED_MATH_BOUNDARY",method="Word.Selection.TypeText; not physical keys or a Space trigger",before,caret,cursor,after,afterMathEnd=doc.OMaths[1].Range.End,literalText,decision="D-01 open; native continuation/physical keys/user trial required. Do not enable Space."});
        });
        Test("continuation/plain-text-api-matrix",()=>
        {
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;
            var rows=new List<object>();
            foreach(string raw in new[]{"x mũ 2","x mu\u0303 2","1 trên 2","(x+1)/(sqrt(2)+3)","x+1"})
            foreach(string suffix in new[]{""," "," After.",".","😀 e\u0301", "\tEnd."})
            {
                var row=json.Deserialize<Dictionary<string,object>>((string)connector.RunContinuationResearch(raw,suffix,"adapter-default"));
                rows.Add(row);Write("continuation-matrix.json",rows);
                foreach(string check in new[]{"outsideExact","nativeUnchanged","associationIntact","typingUndo","convertUndo"})Check((bool)row[check],raw+" / "+suffix+" failed "+check);
            }
        });
        Test("continuation/second-equation-api",()=>
        {
            var doc=New("x^2");doc.Content.Text="Before: x^2 / y^3 End.\r";app.Selection.SetRange(8,11);Convert(Snapshot("x^2"));
            var source=doc.Content;source.Find.ClearFormatting();Check(source.Find.Execute(FindText:"y^^3",MatchCase:true,MatchWildcards:false),"Second source missing: "+doc.Content.Text);source.Select();
            string suffix=doc.Range(source.End,doc.Content.End).Text;doc.UndoClear();string before=State(doc);
            Convert(Snapshot("y^3"));string converted=State(doc);
            var control=doc.ContentControls[2];
            var signatures=doc.OMaths.Cast<WordApi.OMath>().Select(math=>NativeMathSignature.FromXml(math.Range.WordOpenXML)).ToArray();
            app.Selection.TypeText(" tiếp ");
            Check(doc.Range(control.Range.End,doc.Content.End).Text==" tiếp "+suffix,"Text was not inserted immediately after the second equation.");
            Check(doc.OMaths.Count==2&&signatures.SequenceEqual(doc.OMaths.Cast<WordApi.OMath>().Select(math=>NativeMathSignature.FromXml(math.Range.WordOpenXML))),"Typing changed an equation.");
            foreach(WordApi.ContentControl item in doc.ContentControls)WordOperations.ReadUnique(doc,item);
            Check(doc.Undo(1)&&State(doc)==converted,"Typing Undo changed the second conversion.");
            Check(doc.Undo(1)&&State(doc)==before,"Conversion Undo changed the first equation or outside text.");
        });
        Test("geometry-api-zoom-scroll-and-second-window",()=>
        {
            var doc=New("x^2");doc.Content.Text="x^2\r"+string.Concat(Enumerable.Repeat("A test paragraph for scrolling.\r",100));app.Visible=true;
            var range=doc.Range(0,3);var window=doc.ActiveWindow;window.View.Type=WordApi.WdViewType.wdPrintView;window.View.Zoom.Percentage=100;window.ScrollIntoView(range,true);
            window.GetPoint(out int x,out int y,out int w,out int h,range);var first=new{x,y,w,h};Check(w>0&&h>0,"No range box at 100%.");
            window.View.Zoom.Percentage=150;window.ScrollIntoView(range,true);window.GetPoint(out x,out y,out w,out h,range);var zoom=new{x,y,w,h};Check(w>first.w&&h>first.h,"Box failed to scale with zoom.");
            window.ScrollIntoView(doc.Range(doc.Content.End-2,doc.Content.End-1),true);bool offscreen=false;try{window.GetPoint(out x,out y,out w,out h,range);}catch(COMException){offscreen=true;}
            var afterScroll=new{x,y,w,h};
            var other=app.Windows.Add(window);other.Activate();other.View.Zoom.Percentage=80;other.ScrollIntoView(range,true);other.GetPoint(out x,out y,out w,out h,range);var second=new{x,y,w,h};
            Write("geometry-api.json",new{method="Word.Window.GetPoint; no visual badge/DPI acceptance. Offscreen may still return a box; do not infer visibility.",first,zoom,offscreen,afterScroll,second,firstHwnd=window.Hwnd,secondHwnd=other.Hwnd});other.Close();
        });
    }
    private static void RunContinuationResearch()
    {
        Test("observation/continuation-variants",()=>
        {
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;
            var variants=new List<object>();
            foreach(string variant in new[]{"edge","select-control-right","select-control-right-twice","native-end","select-tail-start","select-next-left","next-collapse-start","edge-right"})
                variants.Add(json.DeserializeObject((string)connector.RunContinuationResearch("x mũ 2"," After.",variant)));
            Write("continuation-variants.json",variants);
        });
    }
}
