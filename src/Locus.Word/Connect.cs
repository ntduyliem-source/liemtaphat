using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Locus.Core;
using Locus.Core.Detection;
using Office=Microsoft.Office.Core;
using WordApi=Microsoft.Office.Interop.Word;

namespace Locus.Word;

[ComVisible(true),Guid("D67B9C40-E18A-4BC3-8D29-8A78FA05E5D0"),ProgId("Locus.Word.W0"),ClassInterface(ClassInterfaceType.AutoDual)]
public sealed class Connect : Extensibility.IDTExtensibility2,Office.IRibbonExtensibility
{
    private WordApi.Application? app;
    private NativeInputState? input;
    private Timer? timer;
    private Control? dispatcher;
    private ProbePipe? pipe;
    private readonly Dictionary<long,string> sandboxes=new();
    private static long DocumentKey(WordApi.Document document)
    {
        IntPtr identity=Marshal.GetIUnknownForObject(document);
        try{return identity.ToInt64();}finally{Marshal.Release(identity);}
    }
    private readonly JavaScriptSerializer json=new(){MaxJsonLength=1024*1024};
    private readonly List<object> log=new();
    private readonly string session=Guid.NewGuid().ToString("N");
    private string directory="",last="",lastRaw="";
    private long revision;
    private bool disposed,observing;
    private Form? monitor;
    private TextBox? monitorText;
    private BadgeController? badge;
    private bool badgeEnabled;
    private SpaceTrial? spaceTrial;
    private SpaceTrialPanel? spacePanel;
    public Connect()
    {
        directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Locus","W0",Process.GetCurrentProcess().Id+"-"+session);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"constructed.json"),json.Serialize(new{session,pid=Process.GetCurrentProcess().Id,assembly=typeof(Connect).Assembly.Location}));
    }
    public void OnConnection(object application,Extensibility.ext_ConnectMode connectMode,object addIn,ref Array custom)
    {
        try
        {
            app=(WordApi.Application)application;
            directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Locus","W0",Process.GetCurrentProcess().Id+"-"+session);
            Directory.CreateDirectory(directory);
            dispatcher=new Control(); dispatcher.CreateControl(); _=dispatcher.Handle;
            input=new NativeInputState();
            ((Office.COMAddIn)addIn).Object=this;
            timer=new Timer{Interval=150}; timer.Tick+=(_,_)=>Observe(); timer.Start();
            pipe=new ProbePipe("Locus.W0."+Process.GetCurrentProcess().Id,()=>GetState(),dispatcher);
            app.WindowSelectionChange+=SelectionChanged;
            app.DocumentBeforeClose+=BeforeClose;
            File.WriteAllText(Path.Combine(directory,"connected.json"),json.Serialize(new{session,pid=Process.GetCurrentProcess().Id,connectMode,version=app.Version,build=app.Build,assembly=typeof(Connect).Assembly.Location,coreAssembly=typeof(SourceSnapshot).Assembly.Location}));
        }
        catch(Exception error){if(directory.Length>0)File.WriteAllText(Path.Combine(directory,"startup-error.txt"),error.ToString()); Dispose();}
    }
    public string GetCustomUI(string ribbonId)=>"<customUI xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\"><ribbon><tabs><tab id=\"locusW0\" label=\"Locus W0\"><group id=\"experiments\" label=\"Kiểm chứng connector\"><button id=\"newW0\" label=\"Tài liệu thử mới\" onAction=\"NewSandboxClick\"/><button id=\"observeW0\" label=\"Trạng thái W0\" onAction=\"MonitorClick\"/><button id=\"badgeW0\" label=\"Gắn fx vào vùng chọn\" onAction=\"BadgeClick\"/></group><group id=\"spaceW0\" label=\"Thử nối công thức\"><button id=\"sourceTrial\" label=\"A — Giữ nguồn\" onAction=\"SourceTrialClick\"/><button id=\"nativeTrial\" label=\"B — Native theo Space\" onAction=\"NativeTrialClick\"/></group></tab></tabs></ribbon></customUI>";
    public void NewSandboxClick(Office.IRibbonControl control)=>CreateSandbox("x mũ 2");
    public void MonitorClick(Office.IRibbonControl control)=>ShowMonitor();
    public void BadgeClick(Office.IRibbonControl control){try{SetBadgeEnabled(true);}catch(InvalidOperationException){ShowMonitor();}}
    public void SourceTrialClick(Office.IRibbonControl control)=>StartSpaceTrial("keep-source");
    public void NativeTrialClick(Office.IRibbonControl control)=>StartSpaceTrial("native-live");
    public string CreateSandbox(string source)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(()=>CreateSandbox(source)));
        if(app==null||source.Length>16384)throw new InvalidOperationException("Unavailable host or oversized sandbox.");
        var doc=app.Documents.Add(); string id=Guid.NewGuid().ToString("N"); sandboxes.Add(DocumentKey(doc),id);
        doc.Content.Text=source+"\r"; doc.Activate(); app.Selection.SetRange(0,source.Length); Observe(); return id;
    }
    public string GetState()
    {
        if(dispatcher!=null&&!dispatcher.IsDisposed&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(GetState));
        if(app==null||disposed)return json.Serialize(new{session,connected=false});
        try
        {
            if(app.Documents.Count==0)return json.Serialize(new{session,connected=true,documents=0});
            var doc=app.ActiveDocument; bool armed=sandboxes.TryGetValue(DocumentKey(doc),out string id);
            var observation=input!.Read((IntPtr)app.ActiveWindow.Hwnd);
            string? raw=armed && doc.Content.End<16384?doc.Content.Text:null;
            if((raw??"")!=lastRaw){revision++;lastRaw=raw??"";}
            int[]? bounds=null;
            if(armed && app.Selection.Start!=app.Selection.End && observation.EditorFocus)
            {
                try {app.ActiveWindow.GetPoint(out int x,out int y,out int w,out int h,app.Selection.Range);bounds=new[]{x,y,w,h};}catch(COMException){}
            }
            return json.Serialize(new{session,connected=true,documents=app.Documents.Count,sandbox=armed,documentId=armed?id:null,revision,raw,
                selection=armed?new[]{app.Selection.Start,app.Selection.End}:null,story=armed?(int)app.Selection.StoryType:0,
                nativeCount=armed?doc.OMaths.Count:0,controlCount=armed?doc.ContentControls.Count:0,input=observation,bounds,
                zoom=armed?app.ActiveWindow.View.Zoom.Percentage:0,autoAllowed=false,badge=badge?.State,spaceTrial=spaceTrial?.State});
        }
        catch(COMException error){return json.Serialize(new{session,connected=true,available=false,error=error.ErrorCode,autoAllowed=false});}
    }
    public string ConvertSandboxSelection(int candidateIndex,string expectedSession,string expectedDocumentId,long expectedRevision,int expectedStart,int expectedEnd)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(()=>ConvertSandboxSelection(candidateIndex,expectedSession,expectedDocumentId,expectedRevision,expectedStart,expectedEnd)));
        // Reject at entry as well as immediately before writing: some Word selection queries
        // can move focus from Navigation back into the document as a side effect.
        if(app==null||expectedSession!=session)throw new InvalidOperationException("stale-session");
        bool readyAtEntry=input!.Read((IntPtr)app.ActiveWindow.Hwnd).SafeExplicitTrigger;
        if(!sandboxes.ContainsKey(DocumentKey(app.ActiveDocument)))throw new InvalidOperationException("unarmed-document");
        if(sandboxes[DocumentKey(app.ActiveDocument)]!=expectedDocumentId)throw new InvalidOperationException("stale-document");
        if(!readyAtEntry)throw new InvalidOperationException("trigger-outside-editor-or-input-not-ready");
        GetState(); if(revision!=expectedRevision)throw new InvalidOperationException("stale-source");
        if(app.Selection.Start!=expectedStart||app.Selection.End!=expectedEnd)throw new InvalidOperationException("stale-selection");
        var doc=app.ActiveDocument; string raw=app.Selection.Text;
        var set=new AnalysisEngine().Analyze(new SourceSnapshot(raw)).Regions.Single();
        if(candidateIndex<0||candidateIndex>=set.Candidates.Count)throw new ArgumentOutOfRangeException(nameof(candidateIndex));
        var snapshot=new ManagedSnapshot(set.Select(set.Candidates[candidateIndex].Id));
        return WordOperations.ConvertSelected(app,snapshot,()=>
        {
            if(app.ActiveDocument!=doc || !input!.Read((IntPtr)app.ActiveWindow.Hwnd).SafeExplicitTrigger)throw new InvalidOperationException("focus-or-input-not-ready");
            GetState(); if(revision!=expectedRevision)throw new InvalidOperationException("stale-source");
        });
    }
    private void SelectionChanged(WordApi.Selection selection)=>Observe();
    private void BeforeClose(WordApi.Document document,ref bool cancel)
    {sandboxes.Remove(DocumentKey(document));spaceTrial?.DocumentClosing(document);spacePanel?.RefreshTrial();}
    private void Observe()
    {
        if(disposed||observing)return;
        observing=true;
        try
        {
            if(app!=null&&app.UndoRecord.IsRecordingCustomRecord)return;
            if(app!=null&&app.Documents.Count>0&&spaceTrial?.Active==true){spaceTrial.Tick(input!.Read((IntPtr)app.ActiveWindow.Hwnd));spacePanel?.RefreshTrial();}
            string state=GetState();
            // Bounded log contains source only for new documents explicitly created by this test add-in.
            var comparable=json.DeserializeObject(state) as Dictionary<string,object>;
            if(comparable!=null&&comparable.TryGetValue("input",out object inputValue)&&inputValue is Dictionary<string,object> inputValues)inputValues.Remove("QuietMilliseconds");
            string stable=json.Serialize(comparable);
            if(stable!=last){last=stable;if(log.Count>=100)log.RemoveAt(0);log.Add(new{utc=DateTime.UtcNow.ToString("o"),state=comparable});}
            File.WriteAllText(Path.Combine(directory,"latest.json"),state);
            if(monitorText!=null&&!monitorText.IsDisposed)monitorText.Text=state;
            if(badgeEnabled&&app!=null&&app.Documents.Count>0)
            {sandboxes.TryGetValue(DocumentKey(app.ActiveDocument),out string id);badge?.Update(app,id,input!.Read((IntPtr)app.ActiveWindow.Hwnd));}
            else badge?.Unavailable();
        }
        catch(Exception error) when(error is COMException || error is IOException || error is InvalidOperationException){badge?.Unavailable();}
        finally{observing=false;}
    }
    public void SaveObservations()
    {
        if(dispatcher!=null&&!dispatcher.IsDisposed&&dispatcher.InvokeRequired){dispatcher.Invoke(new Action(SaveObservations));return;}
        File.WriteAllText(Path.Combine(directory,"observations.json"),json.Serialize(log));
    }
    public string ObservationDirectory()=>directory;
    public string StartSpaceTrial(string mode,bool showPanel=true)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(()=>StartSpaceTrial(mode,showPanel)));
        if(app==null)throw new InvalidOperationException("host-unavailable");
        spaceTrial?.Stop("Bắt đầu bài mới.");spacePanel?.Close();spacePanel=null;SetBadgeEnabled(false);
        string id=CreateSandbox("");spaceTrial=new SpaceTrial(app,app.ActiveDocument,mode,input!.Read((IntPtr)app.ActiveWindow.Hwnd));
        if(showPanel){spacePanel=new SpaceTrialPanel(spaceTrial){Location=new Point(20,40)};spacePanel.Show(new TrialOwner((IntPtr)app.ActiveWindow.Hwnd));}
        return id;
    }
    public string GetSpaceTrialState()
    {if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(GetSpaceTrialState));return json.Serialize(spaceTrial?.State??new{active=false,status="not-started"});}
    public void CancelSandboxCloseForResearch()
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired){dispatcher.Invoke(new Action(CancelSandboxCloseForResearch));return;}
        if(app==null||!sandboxes.ContainsKey(DocumentKey(app.ActiveDocument)))throw new InvalidOperationException("unarmed-document");
        var document=app.ActiveDocument;long identity=DocumentKey(document);
        // Cancellation must be delivered synchronously on Word's thread. An out-of-process
        // COM event subscription did not preserve the by-ref cancel value in the probe.
        bool cancelled=false;
        WordApi.ApplicationEvents4_DocumentBeforeCloseEventHandler cancelClose=(WordApi.Document closing,ref bool cancel)=>{if(DocumentKey(closing)==identity){cancel=true;cancelled=true;}};
        app.DocumentBeforeClose+=cancelClose;
        try{document.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges);}
        catch(COMException error) when(cancelled&&error.ErrorCode==unchecked((int)0x800A1066)){}
        finally{app.DocumentBeforeClose-=cancelClose;}
        if(!cancelled)throw new InvalidOperationException("close-cancellation-not-observed");
    }
    private sealed class TrialOwner:IWin32Window{public IntPtr Handle{get;}public TrialOwner(IntPtr handle){Handle=handle;}}
    public string SpaceTrialAction(string action,string text="",int choice=0)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(()=>SpaceTrialAction(action,text,choice)));
        if(spaceTrial==null)throw new InvalidOperationException("trial-not-started");
        if(action=="replay")spaceTrial.ReplayTail(text,true);
        else if(action=="commit")spaceTrial.Commit(choice);
        else if(action=="keep-text")spaceTrial.KeepText();
        else if(action=="stop")spaceTrial.Stop("Đã dừng thử.");
        else throw new InvalidOperationException("unknown-trial-action");
        spacePanel?.RefreshTrial();return GetSpaceTrialState();
    }
    public string RunSandboxResearchOperation(string operation,string payload,string controlId,string fault)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(()=>RunSandboxResearchOperation(operation,payload,controlId,fault)));
        if(app==null||!sandboxes.ContainsKey(DocumentKey(app.ActiveDocument)))throw new InvalidOperationException("unarmed-document");
        var doc=app.ActiveDocument;
        if(operation=="target-change-probe")return WordOperations.ConvertSelected(app,ManagedSnapshot.Decode(payload),()=>app.Selection.SetRange(0,0));
        if(operation=="convert")return WordOperations.ConvertSelected(app,ManagedSnapshot.Decode(payload),()=>{if(app.ActiveDocument!=doc)throw new InvalidOperationException("target-changed");},fault);
        var control=doc.ContentControls.Cast<WordApi.ContentControl>().Single(c=>c.ID==controlId);
        if(operation=="replace")return WordOperations.ReplaceManaged(app,control,ManagedSnapshot.Decode(payload),()=>{if(app.ActiveDocument!=doc)throw new InvalidOperationException("target-changed");},fault);
        if(operation=="restore")WordOperations.Restore(app,control,fault);
        else if(operation=="detach")WordOperations.Detach(app,control);
        else throw new InvalidOperationException("unknown-research-operation");
        return "completed";
    }
    // Fault injection is confined to explicitly created W0 fixture documents. The production
    // ManualConnect entry points keep their own real input/focus guards and expose no faults.
    public string RunScanResearchOperation(string action, string fault)
    {
        if (dispatcher != null && dispatcher.InvokeRequired) return (string)dispatcher.Invoke(new Func<string>(() => RunScanResearchOperation(action, fault)));
        if (app == null || !sandboxes.ContainsKey(DocumentKey(app.ActiveDocument))) throw new InvalidOperationException("unarmed-document");
        var configuration = WordDetectionSettings.Upgrade(Locus.Core.MarkerConfiguration.Default, Locus.Core.Detection.DetectionDomains.All);
        var captured = ScanSession.Capture(app, true, configuration);
        var writes = captured.Regions.Where(r => r.Choice != null && (action == "convert" ? r.Choice.Ready : r.Choice.Native)).Select(r => new ScanWrite(r, action)).ToArray();
        int steps = 0;
        string before = app.ActiveDocument.Content.Text;
        var traces = new List<object>();
        try { var ids = ScanOperations.Execute(app, captured, writes, () => captured.Validate(app, configuration), () => {
            if (DocumentKey(app.ActiveDocument) != DocumentKey(captured.Document)) throw new InvalidOperationException("target-changed");
            if (fault == "cancel:1" && steps++ == 1) throw new OperationCanceledException("F_CANCEL");
        }, fault, stage => traces.Add(new { stage, recording = app.UndoRecord.IsRecordingCustomRecord, level = app.UndoRecord.CustomRecordLevel, text = captured.Document.Content.Text, math = captured.Document.OMaths.Count }));
        return string.Join(",", ids); }
        catch (Exception error) {
            File.WriteAllText(Path.Combine(directory, "scan-research-failure.json"), json.Serialize(new { before, after = app.ActiveDocument.Content.Text, math = app.ActiveDocument.OMaths.Count, traces, error = error.ToString() }));
            throw;
        }
    }
    public string RunUndoResearch(string raw,bool copyBefore)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(()=>RunUndoResearch(raw,copyBefore)));
        if(app==null||raw.Length>100)throw new InvalidOperationException("research-input-limit");
        var set=new AnalysisEngine().Analyze(new SourceSnapshot(raw)).Regions.Single();
        var snapshot=new ManagedSnapshot(set.Select(set.Candidates.Last().Id));
        var owned=new List<WordApi.Document>();
        string State(WordApi.Document doc)=>json.Serialize(new{text=doc.Content.Text,math=doc.OMaths.Count,tags=doc.ContentControls.Cast<WordApi.ContentControl>().Select(c=>c.Tag).ToArray()});
        WordApi.Document New(){CreateSandbox("Before: "+raw+" After.");var doc=app.ActiveDocument;owned.Add(doc);app.Selection.SetRange(8,8+raw.Length);doc.UndoClear();return doc;}
        try
        {
            if(copyBefore){var copied=New();WordOperations.ConvertSelected(app,snapshot,()=>{});copied.Content.Copy();var target=New();target.Content.Paste();}
            var document=New();string original=State(document);WordOperations.ConvertSelected(app,snapshot,()=>{});string converted=State(document);
            WordOperations.Restore(app,document.ContentControls[1]);string restored=State(document);bool undo=document.Undo(1);string undone=State(document);
            bool redo=document.Redo(1);string redone=State(document);
            return json.Serialize(new{passed=restored==original&&undo&&undone==converted&&redo&&redone==original,original,converted,restored,undone,redone,copyBefore,thread=System.Threading.Thread.CurrentThread.ManagedThreadId});
        }
        finally{foreach(var doc in owned)doc.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges);}
    }
    public string RunContinuationResearch(string raw,string suffix,string variant)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(()=>RunContinuationResearch(raw,suffix,variant)));
        if(app==null||raw.Length>100||suffix.Length>100)throw new InvalidOperationException("research-input-limit");
        CreateSandbox("");var doc=app.ActiveDocument;
        try{return ContinuationResearch.Run(app,raw,suffix,variant);}
        finally{doc.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges);}
    }
    public void SetBadgeEnabled(bool enabled)
    {
        if(dispatcher!=null&&dispatcher.InvokeRequired){dispatcher.Invoke(new Action(()=>SetBadgeEnabled(enabled)));return;}
        if(!enabled){badgeEnabled=false;badge?.Clear();return;}
        if(app==null||!sandboxes.TryGetValue(DocumentKey(app.ActiveDocument),out string id))throw new InvalidOperationException("unarmed-document");
        if(badge==null){badge=new BadgeController();badge.Clicked+=()=>{Observe();var state=json.Deserialize<Dictionary<string,object>>(GetBadgeState());if(state.TryGetValue("visible",out object value)&&(bool)value)ShowMonitor();};}
        badge.Arm(app,id);badgeEnabled=true;Observe();
    }
    public string GetBadgeState()
    {if(dispatcher!=null&&dispatcher.InvokeRequired)return (string)dispatcher.Invoke(new Func<string>(GetBadgeState));Observe();return json.Serialize(badge?.State??new{visible=false,reason="disabled"});}
    public void ShowMonitor()
    {
        if(monitor==null||monitor.IsDisposed)
        {monitor=new Form{Text="Locus W0 — trạng thái thử nghiệm",Width=800,Height=380};monitorText=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both};monitor.Controls.Add(monitorText);}
        monitor.Show();Observe();
    }
    public void OnDisconnection(Extensibility.ext_DisconnectMode removeMode,ref Array custom)=>Dispose();
    public void OnAddInsUpdate(ref Array custom){}
    public void OnStartupComplete(ref Array custom)=>Observe();
    public void OnBeginShutdown(ref Array custom)=>Dispose();
    private void Dispose()
    {
        if(disposed)return;disposed=true;
        try{SaveObservations();File.WriteAllText(Path.Combine(directory,"disconnected.json"),json.Serialize(new{session,utc=DateTime.UtcNow.ToString("o")}));}catch{}
        timer?.Stop();timer?.Dispose();pipe?.Dispose();input?.Dispose();monitor?.Dispose();badge?.Dispose();spaceTrial?.Stop("Host disconnected.");spacePanel?.Dispose();
        if(app!=null)try{app.WindowSelectionChange-=SelectionChanged;app.DocumentBeforeClose-=BeforeClose;}catch(COMException){}
        dispatcher?.Dispose();sandboxes.Clear();app=null;
    }
}
