using System;
using System.IO;
using System.Linq;
using System.Threading;
using Locus.Word;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;
using WordApi=Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static void RunScience()
    {
        var settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Locus","word-manual-settings.json");
        var settings=File.Exists(settingsPath)?File.ReadAllBytes(settingsPath):null;
        var originalSettings=ManualState();
        app.Visible=true;var anchor=app.Documents.Add();anchor.Content.Text="Locus E3 synthetic verification anchor.\r";anchor.Activate();app.ActiveWindow.Caption="Locus E3 verification";
        try
        {
            Stage("science/wait-editor-focus");var focusWait=System.Diagnostics.Stopwatch.StartNew();
            while(!((bool)((System.Collections.Generic.Dictionary<string,object>)ManualState()["input"])["EditorFocus"])&&focusWait.Elapsed.TotalSeconds<55)Thread.Sleep(150);
            FocusManual();
            Test("science/native-registration",()=>{Check(app.COMAddIns.Item("Locus.Word.Manual").Connect,"Disconnected");Check(!(bool)ManualState()["autoAllowed"],"Auto enabled");});
            int file=0;
            foreach(var item in new[]{(4,"H2SO4"),(4,"Ca(OH)2"),(4,"Co"),(4,"CO"),(4,"SO₄²⁻"),(4,"Na^+"),(4,"2H2 + O2 -> 2H2O"),(4,"K4[Fe(CN)6]"),(4,"lc[H₂SO₄]"),(2,"v_0 = 10 m/s^2"),(2,"vec(v_0)"),(2,"vec(v)_0"),(2,"alpha = 2"),(2,"F = 10 kg m/s^2"),(2,"v = 15 km/h"),(2,"x mu\u0303 2"),(7,"x+1/2")})
            Test("science/native-roundtrip/"+item.Item2,()=>
            {
                Manual.ConfigureDetection("lc[","]",item.Item1);
                var doc=ManualDocument(item.Item2);string before=State(doc);int start=app.Selection.Start,end=app.Selection.End;
                var preview=PreviewManual();Check(State(doc)==before,"Preview wrote document");
                Converted(CompleteManual(preview));Check(doc.OMaths.Count==1&&doc.ContentControls.Count==1,"Missing native+metadata");
                var saved=WordOperations.ReadUnique(doc,doc.ContentControls[1]);string snapshot=CandidateSetSerializer.Serialize(saved.Candidates),native=State(doc);
                Check(saved.Candidates.OriginalReplacement==item.Item2&&saved.Selected.Id==(string)preview["candidateId"],"Source/candidate mismatch");
                string omml=doc.OMaths[1].Range.WordOpenXML;
                if(item.Item2=="SO₄²⁻")Check(omml.Contains("sSup")&&omml.Contains("sSub"),"Charge/subscript missing");
                if(item.Item2.Contains("vec"))Check(omml.Contains("accPr"),"Vector accent missing");
                Write("native-"+(++file)+".json",new{raw=item.Item2,domain=saved.Selected.Document.Domain,candidate=saved.Selected.Id,snapshot,omml});
                Check(doc.Undo(1)&&State(doc)==before,"Undo source mismatch");Check(app.Selection.Start==start&&app.Selection.End==end,"Undo selection mismatch");
                Check(doc.Redo(1)&&State(doc)==native,"Redo mismatch");
                var path=Path.Combine(directory,"science-"+file+".docx");doc.SaveAs2(path,WordApi.WdSaveFormat.wdFormatXMLDocument);Close(doc);
                var reopened=app.Documents.Open(path);documents.Add(reopened);reopened.Activate();
                var restored=WordOperations.ReadUnique(reopened,reopened.ContentControls[1]);Check(CandidateSetSerializer.Serialize(restored.Candidates)==snapshot,"Save/reopen snapshot drift");
                Manual.ConfigureDetection("lc[","]",0);var managed=ManageManual(reopened.ContentControls[1]);
                Check((string)managed["selectedCandidateId"]==saved.Selected.Id,"Detector off changed managed candidate");
                var result=CompleteManual(managed,"restore");Check((string)result["message"]=="restored"&&State(reopened)==before,"Restore failed: "+json.Serialize(result));
                Check(reopened.Undo(1)&&reopened.OMaths.Count==1,"Undo restore failed");
            });
            for(int mask=0;mask<8;mask++)
            Test("science/checkbox-combination/"+mask,()=>
            {
                Manual.ConfigureDetection("lc[","]",mask);var doc=ManualDocument("H2SO4");string before=State(doc);
                var state=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>((string)Manual.OpenSelectionPreview());
                Check((state["sessionId"]!=null)==((mask&4)!=0),"Checkbox admission mismatch");Check(State(doc)==before,"Recognition wrote document");Manual.CancelPreview();
            });
            Test("science/stale-detector-settings",()=>{Manual.ConfigureDetection("lc[","]",4);var doc=ManualDocument("H2SO4");string before=State(doc);var preview=PreviewManual();Manual.ConfigureDetection("lc[","]",0);CompleteManual(preview);Check(State(doc)==before&&doc.OMaths.Count==0,"Stale checkbox result wrote document");});
            foreach(var raw in new[]{"Fe3+","H2 + -> H2O","Ca(OH","v = 10 kg/m/s","https://example.com/H2SO4"})
            Test("science/refuse-incomplete/"+raw,()=>{Manual.ConfigureDetection("lc[","]",raw.StartsWith("v")?2:4);var doc=ManualDocument(raw);string before=State(doc);var state=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>((string)Manual.OpenSelectionPreview());Check(state["sessionId"]==null&&State(doc)==before,"Partial or invalid source accepted");Manual.CancelPreview();});
            Test("science/signature-observes-accent-and-unit-style",()=>{
                var set=new AnalysisEngine().Analyze(new SourceSnapshot("vec(v)"),new AnalysisOptions(enabledDomains:DetectionDomains.Physics)).Regions.Single();var xml=CandidateExporter.ToOmml(set.Candidates[0]);
                Check(NativeMathSignature.FromXml(xml,true)!=NativeMathSignature.FromXml(xml.Replace("\u20d7","\u0302"),true),"Accent property ignored");
                var units=new AnalysisEngine().Analyze(new SourceSnapshot("10 m"),new AnalysisOptions(enabledDomains:DetectionDomains.Physics)).Regions.Single();xml=CandidateExporter.ToOmml(units.Candidates[0]);
                Check(NativeMathSignature.FromXml(xml,true)!=NativeMathSignature.FromXml(xml.Replace("val=\"p\"","val=\"i\""),true),"Upright unit ignored");
            });
        }
        finally
        {
            Manual.CancelPreview();Manual.ConfigureDetection((string)originalSettings["markerOpen"],(string)originalSettings["markerClose"],System.Convert.ToInt32(originalSettings["enabledDomains"]));
            if(settings!=null)File.WriteAllBytes(settingsPath,settings);else if(File.Exists(settingsPath))File.Delete(settingsPath);
            anchor.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges);
        }
    }
}
