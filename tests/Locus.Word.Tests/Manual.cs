using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Locus.Word;
using WordApi = Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static int RunManualDemo(string[] args)
    {
        directory=Path.GetFullPath(args.Length>0?args[0]:"artifacts/m3/native");Directory.CreateDirectory(directory);
        app=new WordApi.Application();app.Visible=true;app.DisplayAlerts=WordApi.WdAlertLevel.wdAlertsNone;
        var doc=app.Documents.Add();doc.Content.Text="Locus M3 - Test document\rBefore: 👩‍🏫 e\u0301 x mu\u0303 2 After.\r1 trên 2\rx+1/2\r";
        string path=Path.Combine(directory,"manual-demo.docx");doc.SaveAs2(path,WordApi.WdSaveFormat.wdFormatXMLDocument);
        var range=doc.Content;Check(range.Find.Execute("x mu\u0303 2"),"Missing source fixture.");range.Select();doc.UndoClear();
        app.Activate();app.ActiveWindow.ActivePane.Activate();
        var process=System.Diagnostics.Process.GetProcessesByName("WINWORD").Single();
        Write("ownership.json",new{pid=process.Id,startTimeUtc=process.StartTime.ToUniversalTime().ToString("o"),path,window=app.ActiveWindow.Hwnd});
        Console.WriteLine("Owned M3 demo: "+path);return 0;
    }
    private static dynamic Manual => app.COMAddIns.Item("Locus.Word.Manual").Object;
    private static Dictionary<string,object> ManualState() => json.Deserialize<Dictionary<string,object>>((string)Manual.GetManualState());
    private static WordApi.Document ManualDocument(string raw)
    {
        Manual.CancelPreview(); var doc=app.Documents.Add();documents.Add(doc);
        doc.Content.Text="Before: "+raw+" After.\r";doc.Activate();app.Selection.SetRange(8,8+raw.Length);doc.UndoClear();FocusManual();return doc;
    }
    private static void FocusManual()
    {
        app.Visible=true;app.Activate();app.ActiveWindow.Activate();app.ActiveWindow.ActivePane.Activate();Thread.Sleep(400);
        var input=(Dictionary<string,object>)ManualState()["input"];
        if(Environment.GetEnvironmentVariable("LOCUS_WORD_TEST_SUITE")=="science"&&!(bool)input["EditorFocus"])
        {
            Write("focus-needed.json",new{waiting=true,hwnd=app.ActiveWindow.Hwnd,title=app.ActiveWindow.Caption,reason=input["FocusClass"]});
            var wait=System.Diagnostics.Stopwatch.StartNew();
            while(!(bool)input["EditorFocus"]&&wait.Elapsed.TotalSeconds<45){Thread.Sleep(250);input=(Dictionary<string,object>)ManualState()["input"];}
            Write("focus-needed.json",new{waiting=false});
        }
        Check((bool)input["EditorFocus"],"Test prerequisite: actual Word editor focus is required, got "+(string)input["FocusClass"]);
    }
    private static Dictionary<string,object> PreviewManual()
    {
        var state=json.Deserialize<Dictionary<string,object>>((string)Manual.OpenSelectionPreview());
        Check(state["sessionId"]!=null,"Preview refused: "+json.Serialize(state));
        Check((string)state["candidateId"]==(string)state["previewCandidateId"],"Renderer candidate mismatch.");return state;
    }
    private static Dictionary<string,object> ManageManual(WordApi.ContentControl control)
    {
        Manual.CancelPreview();control.Range.Select();FocusManual();
        var state=json.Deserialize<Dictionary<string,object>>((string)Manual.OpenManagedFormula());
        Check(state["sessionId"]!=null,"Manage refused: "+json.Serialize(state));return state;
    }
    private static Dictionary<string,object> CompleteManual(Dictionary<string,object> state,string action="convert")
    {
        Manual.ConfirmPreview((string)state["sessionId"],action,(string)state["candidateId"]);
        var watch=System.Diagnostics.Stopwatch.StartNew();Dictionary<string,object> result;
        // COM polling must leave the Word UI dispatcher time to deliver its 150 ms commit timer.
        do {Thread.Sleep(250);result=ManualState();}while((string)result["message"]=="pending"&&watch.Elapsed.TotalSeconds<8);
        return result;
    }
    private static void Converted(Dictionary<string,object> state) => Check((string)state["message"]=="converted","Conversion refused: "+json.Serialize(state));
    private static void RunManual()
    {
        app.Visible=true;
        // Keep one owned document open so closing a case does not send focus back to another app.
        var anchor=app.Documents.Add();anchor.Content.Text="Locus M3 automated test anchor.\r";anchor.Activate();app.ActiveWindow.Caption="Locus M3 verification";
        app.Activate();app.ActiveWindow.ActivePane.Activate();
        Stage("manual/wait-for-editor-focus");
        var ready=System.Diagnostics.Stopwatch.StartNew();
        while(!((bool)((Dictionary<string,object>)ManualState()["input"])["EditorFocus"])&&ready.Elapsed.TotalSeconds<55)Thread.Sleep(150);
        try {
        FocusManual();
        Test("manual/registration-and-no-auto",()=>{Check(app.COMAddIns.Item("Locus.Word.Manual").Connect,"Manual add-in disconnected.");var state=ManualState();Check(!(bool)state["autoAllowed"]&&state["sessionId"]==null,"Unrequested session/auto.");});
        foreach(var raw in new[]{"x mũ 2","1 trên 2","x+1/2","x mu\u0303 2","lc[x mũ 2]","can(2+3","(x+1)/(sqrt(2)+3)"})
        Test("manual/convert-undo-restore/"+raw,()=>
        {
            var doc=ManualDocument(raw);string original=State(doc);int start=app.Selection.Start,end=app.Selection.End;
            var preview=PreviewManual();Check(State(doc)==original,"Preview wrote source.");Check((string)preview["source"]==raw,"Preview lost raw.");
            Converted(CompleteManual(preview));Check(doc.OMaths.Count==1&&doc.ContentControls.Count==1,"Missing native+control.");
            var saved=WordOperations.ReadUnique(doc,doc.ContentControls[1]);Check(saved.Candidates.OriginalReplacement==raw&&saved.Selected.Id==(string)preview["candidateId"],"Commit did not use displayed candidate.");
            string converted=State(doc);Check(doc.Undo(1)&&State(doc)==original,"Undo conversion differs.");Check(app.Selection.Start==start&&app.Selection.End==end,"Undo selection differs.");
            Check(doc.Redo(1)&&State(doc)==converted,"Redo differs.");
            var managed=ManageManual(doc.ContentControls[1]);Check((string)managed["selectedCandidateId"]==saved.Selected.Id,"Reopen changed selected candidate.");
            var restored=CompleteManual(managed,"restore");Check((string)restored["message"]=="restored"&&State(doc)==original,"Restore failed: "+json.Serialize(restored));
            Check(doc.Undo(1)&&State(doc)==converted,"Undo restore differs.");Check(doc.Redo(1)&&State(doc)==original,"Redo restore differs.");
        });
        Test("manual/candidate-choice-and-detach",()=>
        {
            var doc=ManualDocument("x+1/2");var preview=PreviewManual();var candidates=(System.Collections.ArrayList)preview["candidates"];
            var alternative=(Dictionary<string,object>)candidates[1];Check((string)alternative["Kind"]=="repair","Wrong scope candidate kind.");
            preview=json.Deserialize<Dictionary<string,object>>((string)Manual.ChooseCandidate((string)preview["sessionId"],(string)alternative["Id"]));
            Converted(CompleteManual(preview));Check(WordOperations.ReadUnique(doc,doc.ContentControls[1]).Selected.Id==(string)alternative["Id"],"Choice lost.");
            string converted=State(doc);var detached=CompleteManual(ManageManual(doc.ContentControls[1]),"detach");
            Check((string)detached["message"]=="detached"&&doc.OMaths.Count==1&&doc.ContentControls.Count==0,"Detach destroyed native.");
            Check(doc.Undo(1)&&State(doc)==converted,"Undo detach differs.");
        });
        Test("manual/cancel-clears-source-session",()=>
        {var doc=ManualDocument("x^2");string before=State(doc);var preview=PreviewManual();Manual.CancelPreview();Check(State(doc)==before&&ManualState()["source"]==null,"Cancel changed source or retained session.");Check((string)ManualState()["hostId"]!="","Missing lifetime ID.");});
        foreach(var change in new[]{"source","prefix","selection","window","document","tracking","cancel"})
        Test("manual/stale/"+change,()=>
        {
            var doc=ManualDocument("x^2");var preview=PreviewManual();
            if(change=="source")doc.Range(8,11).Text="x^3";
            if(change=="prefix")doc.Range(0,0).Text="prefix ";
            if(change=="selection")app.Selection.SetRange(0,0);
            if(change=="window")app.Windows.Add(app.ActiveWindow).Activate();
            if(change=="document"){var other=app.Documents.Add();documents.Add(other);other.Content.Text="Other doc";other.Activate();}
            if(change=="tracking")doc.TrackRevisions=true;
            if(change=="cancel") { Manual.CancelPreview(); }
            string before=State(doc);Thread.Sleep(200);CompleteManual(preview);Check(State(doc)==before&&doc.OMaths.Count==0,"Stale request wrote: "+change);
        });
        Test("manual/save-reopen-and-no-addin",()=>
        {
            var doc=ManualDocument("x mu\u0303 2");Converted(CompleteManual(PreviewManual()));string tag=doc.ContentControls[1].Tag;
            string path=Path.Combine(directory,"manual-roundtrip.docx");doc.SaveAs2(path,WordApi.WdSaveFormat.wdFormatXMLDocument);Close(doc);
            app.COMAddIns.Item("Locus.Word.Manual").Connect=false;
            doc=app.Documents.Open(path);documents.Add(doc);Check(doc.OMaths.Count==1,"Native missing without add-in.");
            doc.OMaths[1].Functions[1].ScrSup.Sup.Range.Text="3";Check(doc.OMaths[1].Range.Text.Contains("3"),"Native cannot edit without Locus.");Close(doc);
            app.COMAddIns.Item("Locus.Word.Manual").Connect=true;doc=app.Documents.Open(path);documents.Add(doc);
            Check(doc.ContentControls[1].Tag==tag,"Save/reopen lost snapshot.");Check((string)ManageManual(doc.ContentControls[1])["source"]=="x mu\u0303 2","NFD lost.");Manual.CancelPreview();
        });
        foreach(var corruption in new[]{"native","tag","future","duplicate"})
        Test("manual/manage-refusal/"+corruption,()=>
        {
            var doc=ManualDocument("x^2");Converted(CompleteManual(PreviewManual()));var control=doc.ContentControls[1];
            if(corruption=="native")control.Range.OMaths[1].Functions[1].ScrSup.Sup.Range.Text="3";
            if(corruption=="tag")control.Tag="locus:broken";
            if(corruption=="future")control.Tag=control.Tag.Replace("word:1:","word:99:").Replace("word:2:","word:99:");
            if(corruption=="duplicate"){doc.Content.Copy();doc.Range(doc.Content.End-1,doc.Content.End-1).Paste();}
            control.Range.Select();FocusManual();string before=State(doc);var state=json.Deserialize<Dictionary<string,object>>((string)Manual.OpenManagedFormula());
            Check(state["sessionId"]==null&&State(doc)==before,"Drift/corrupt metadata allowed.");Manual.CancelPreview();
        });
        foreach(var context in new[]{"text","url","table","header","control","track","protected","read-only"})
        Test("manual/context-refusal/"+context,()=>
        {
            var doc=ManualDocument(context=="text"?"Câu văn thông thường":context=="url"?"https://example.com/x^2":"x^2");
            if(context=="table"){var table=doc.Tables.Add(doc.Range(0,0),1,1);table.Cell(1,1).Range.Text="x^2";var range=table.Cell(1,1).Range;range.SetRange(range.Start,range.Start+3);range.Select();}
            if(context=="header"){var range=doc.Sections[1].Headers[WordApi.WdHeaderFooterIndex.wdHeaderFooterPrimary].Range;range.Text="x^2";range.SetRange(range.Start,range.Start+3);range.Select();}
            if(context=="control")doc.ContentControls.Add(WordApi.WdContentControlType.wdContentControlRichText,app.Selection.Range);
            if(context=="track")doc.TrackRevisions=true;
            if(context=="protected")doc.Protect(WordApi.WdProtectionType.wdAllowOnlyReading,Password:"m3-fixture");
            if(context=="read-only"){string path=Path.Combine(directory,"readonly.docx");doc.SaveAs2(path);Close(doc);doc=app.Documents.Open(path,ReadOnly:true);documents.Add(doc);app.Selection.SetRange(8,11);}
            FocusManual();string before=State(doc);var state=json.Deserialize<Dictionary<string,object>>((string)Manual.OpenSelectionPreview());Check(state["sessionId"]==null&&State(doc)==before,"Unsupported context allowed: "+context);Manual.CancelPreview();
        });
        Test("manual/disconnect-expire",()=>
        {
            var doc=ManualDocument("x^2");var preview=PreviewManual();string before=State(doc);dynamic old=Manual;
            app.COMAddIns.Item("Locus.Word.Manual").Connect=false;Check(State(doc)==before,"Disconnect wrote.");
            app.COMAddIns.Item("Locus.Word.Manual").Connect=true;var state=ManualState();Check(state["sessionId"]==null&&(string)state["hostId"]!=(string)preview["hostId"],"Reused old session.");
        });
        foreach(var change in new[]{"typing","find","candidate","metadata","close"})
        Test("manual/pending-refusal/"+change,()=>
        {
            var doc=ManualDocument("x^2");var preview=PreviewManual();
            if(change=="metadata") {Converted(CompleteManual(preview));preview=ManageManual(doc.ContentControls[1]);}
            if(change=="candidate") {
                Manual.ConfirmPreview((string)preview["sessionId"],"convert","not-the-preview-id");
                Check(doc.OMaths.Count==0&&(string)ManualState()["message"]=="candidate-changed","Wrong candidate was allowed.");return;
            }
            Manual.ConfirmPreview((string)preview["sessionId"],change=="metadata"?"restore":"convert",(string)preview["candidateId"]);
            if(change=="typing")app.Selection.TypeText("new text");
            if(change=="find") {
                // Official Word control ID; "Find" and "NavPaneSearch" are not valid idMso values.
                app.CommandBars.ExecuteMso("NavigationPaneFind");
                Check(!(bool)((Dictionary<string,object>)ManualState()["input"])["EditorFocus"],"Find did not take focus away from the editor.");
            }
            if(change=="metadata")doc.ContentControls[1].Tag="locus:modified-while-pending";
            if(change=="close") {Close(doc);Thread.Sleep(500);Check(ManualState()["sessionId"]==null,"Closed document retained session.");return;}
            string before=State(doc);Thread.Sleep(550);var state=ManualState();
            Check(State(doc)==before&&state["sessionId"]==null,"Pending action overwrote new state: "+change);
            if(change=="find") {app.ActiveWindow.DocumentMap=false;app.ActiveWindow.ActivePane.Activate();}
        });
        Test("manual/custom-markers-and-settings-invalidation",()=>
        {
            string originalOpen=(string)ManualState()["markerOpen"],originalClose=(string)ManualState()["markerClose"];
            try {
                Manual.ConfigureMarkers("toan[[","]]");var doc=ManualDocument("toan[[1 trên 2]]");string original=State(doc);
                Converted(CompleteManual(PreviewManual()));Check(WordOperations.ReadUnique(doc,doc.ContentControls[1]).Candidates.OriginalReplacement=="toan[[1 trên 2]]","Custom wrapper lost.");
                Check((string)CompleteManual(ManageManual(doc.ContentControls[1]),"restore")["message"]=="restored"&&State(doc)==original,"Custom restore differs.");
                app.Selection.SetRange(8,8+"toan[[1 trên 2]]".Length);FocusManual();var preview=PreviewManual();
                Manual.ConfigureMarkers("math{","}");CompleteManual(preview);Check(State(doc)==original,"Settings change admitted old preview.");
                Reject(()=>Manual.ConfigureMarkers("x","xy"));Check((string)ManualState()["markerOpen"]=="math{","Invalid markers changed config.");
            } finally {Manual.ConfigureMarkers(originalOpen,originalClose);}
        });
        Test("manual/type-after-convert",()=>
        {
            var doc=ManualDocument("x mũ 2");string original=State(doc);
            Converted(CompleteManual(PreviewManual()));string converted=State(doc);
            string tag=doc.ContentControls[1].Tag;
            string signature=NativeMathSignature.FromXml(doc.OMaths[1].Range.WordOpenXML);
            app.Selection.TypeText(" tiếp tục");
            Check(doc.Content.Text.Contains(" tiếp tục After."),"Continuation did not enter body text.");
            Check(doc.OMaths.Count==1&&doc.ContentControls[1].Tag==tag&&NativeMathSignature.FromXml(doc.OMaths[1].Range.WordOpenXML)==signature,"Continuation changed native or metadata.");
            Check(doc.Undo(1)&&State(doc)==converted,"Typing Undo differs.");
            Check(doc.Undo(1)&&State(doc)==original,"Conversion Undo after typing differs.");
        });
        Manual.CancelPreview();
        } finally {Manual.CancelPreview();anchor.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges);}
    }
}
