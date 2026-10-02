using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Web.Script.Serialization;
using WordApi=Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static Dictionary<string,object> Observe(dynamic connector)=>json.Deserialize<Dictionary<string,object>>((string)connector.GetState());
    private static string PipeRead(int processId,int timeout=3000)
    {
        using var client=new NamedPipeClientStream(".","Locus.W0."+processId,PipeDirection.In);
        client.Connect(timeout); using var reader=new StreamReader(client);
        var task=reader.ReadLineAsync();if(!task.Wait(timeout))throw new TimeoutException("Pipe response timeout.");return task.Result!;
    }
    private static void RejectReason(Action action,string reason)
    {
        try{action();}catch(Exception error){Check(error.ToString().Contains(reason),"Unexpected refusal: "+error);return;}throw new Exception("Request unexpectedly allowed: "+reason);
    }
    private static void RunLifecycle()
    {
        app.Visible=true;
        var addin=app.COMAddIns.Item("Locus.Word.W0");
        Test("lifecycle/installed-and-startup",()=>{Check(addin.Connect,"Installed add-in did not connect at Word startup.");dynamic connector=addin.Object;var state=Observe(connector);Check((bool)state["connected"],"Host disconnected.");Write("startup.json",state);});
        Test("lifecycle/document-identities-and-unarmed-data",()=>
        {
            dynamic connector=addin.Object;
            string firstId=connector.CreateSandbox("x^2");var first=app.ActiveDocument;documents.Add(first);
            var firstState=Observe(connector);Thread.Sleep(200);var later=Observe(connector);
            Check((bool)later["sandbox"]&&(string)later["documentId"]==firstId&&System.Convert.ToInt64(later["revision"])==System.Convert.ToInt64(firstState["revision"]),"Host lost document identity or invented a source change.");
            string secondId=connector.CreateSandbox("x^2");var second=app.ActiveDocument;documents.Add(second);Check(firstId!=secondId,"Document IDs collided.");
            RejectReason(()=>connector.ConvertSandboxSelection(0,(string)firstState["session"],firstId,System.Convert.ToInt64(firstState["revision"]),0,3),"stale-document");
            first.Activate();Check((string)Observe(connector)["documentId"]==firstId,"Switching documents associated wrong source.");
            var ordinary=New("DO NOT OBSERVE THIS ORDINARY DOCUMENT");var state=Observe(connector);Check(!(bool)state["sandbox"]&&state["raw"]==null&&state["selection"]==null,"Observer read unarmed content.");
            RejectReason(()=>connector.ConvertSandboxSelection(0,(string)firstState["session"],firstId,System.Convert.ToInt64(firstState["revision"]),0,3),"unarmed-document");
            Write("documents.json",new{firstId,secondId,firstState,ordinary=state});
        });
        Test("lifecycle/read-only-pipe-multiple-clients",()=>
        {
            dynamic connector=addin.Object;connector.CreateSandbox("x mũ 2");documents.Add(app.ActiveDocument);
            int pid=Process.GetProcessesByName("WINWORD")[0].Id;var first=json.Deserialize<Dictionary<string,object>>(PipeRead(pid));var second=json.Deserialize<Dictionary<string,object>>(PipeRead(pid));
            Check((string)first["session"]==(string)second["session"]&&(string)first["raw"]=="x mũ 2\r\r","Read-only clients lost source/session.");Write("ipc.json",new{first,second});
        });
        Test("lifecycle/same-document-new-window",()=>
        {
            dynamic connector=addin.Object;string id=connector.CreateSandbox("x^2");var doc=app.ActiveDocument;documents.Add(doc);var original=app.ActiveWindow;var other=app.Windows.Add(original);other.Activate();
            Check((string)Observe(connector)["documentId"]==id,"Second window changed document identity.");
            original.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges);var state=Observe(connector);
            Check((bool)state["sandbox"]&&(string)state["documentId"]==id,"Closing first window lost live document identity.");Write("multiwindow.json",state);
        });
        Test("lifecycle/disconnect-reconnect-expire-session",()=>
        {
            dynamic old=addin.Object;old.CreateSandbox("x^2");var doc=app.ActiveDocument;documents.Add(doc);
            var before=Observe(old);string content=doc.Content.Text;old.SaveObservations();addin.Connect=false;
            Check(!(bool)Observe(old)["connected"]&&doc.Content.Text==content,"Disconnect left host active or changed document.");
            bool offline=false;try{PipeRead(Process.GetProcessesByName("WINWORD")[0].Id,500);}catch(TimeoutException){offline=true;}catch(IOException){offline=true;}Check(offline,"Pipe remained active after disconnect.");
            addin.Connect=true;dynamic current=addin.Object;var after=Observe(current);
            Check((bool)after["connected"]&&(string)after["session"]!=(string)before["session"],"Reconnect reused session or failed to start.");
            Check(!(bool)after["sandbox"]&&doc.Content.Text==content,"Reconnection silently re-armed existing content.");
            RejectReason(()=>current.ConvertSandboxSelection(0,(string)before["session"],(string)before["documentId"],System.Convert.ToInt64(before["revision"]),0,3),"stale-session");
            string id=current.CreateSandbox("y^3");documents.Add(app.ActiveDocument);Check((string)Observe(current)["documentId"]==id,"Reconnected connector cannot create a new sandbox.");
            Write("reconnect.json",new{before,after,offline});
        });
        Test("lifecycle/no-documents",()=>{dynamic connector=addin.Object;var state=Observe(connector);Check((int)state["documents"]==0&&(bool)state["connected"],"Closed documents left stale host state.");Write("empty.json",state);});
        Test("lifecycle/in-process-undo-after-clipboard",()=>{dynamic connector=addin.Object;var result=json.Deserialize<Dictionary<string,object>>((string)connector.RunUndoResearch("x+1/2",true));Write("inproc-undo.json",result);Check((bool)result["passed"],"In-process restore/Undo/Redo failed after clipboard.");});
    }
}
