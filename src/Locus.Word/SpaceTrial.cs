using System;
using System.Linq;
using System.Security;
using System.Runtime.InteropServices;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using WordApi=Microsoft.Office.Interop.Word;

namespace Locus.Word;

// Explicitly started W0 experiment in an owned document. Never enabled in ordinary documents.
internal sealed class SpaceTrial
{
    private readonly WordApi.Application app;
    private readonly WordApi.Document document;
    private readonly string prefix="Source: ",suffix;
    private readonly string mode;
    private readonly int window,initialEscapes,initialReturns;
    private readonly long documentIdentity;
    private int lastSpace;
    private string committedRaw="",controlId="";
    private bool writing,ticking;
    public bool Active{get;private set;}=true;
    public string Status{get;private set;}="Đang thử. Gõ công thức trong Word.";
    public string Raw{get;private set;}="";
    public CandidateSet? Candidates{get;private set;}
    public int NativeUpdates{get;private set;}
    public string Mode=>mode;
    public SpaceTrial(WordApi.Application app,WordApi.Document document,string mode,InputObservation input)
    {
        if(mode!="keep-source"&&mode!="native-live")throw new ArgumentException("Unknown trial mode.");
        this.app=app;this.document=document;documentIdentity=Identity(document);this.mode=mode;lastSpace=input.Spaces;window=app.ActiveWindow.Hwnd;initialEscapes=input.Escapes;initialReturns=input.Returns;
        document.Content.Text=prefix+" | Outside remains.\r";
        suffix=document.Content.Text.Substring(prefix.Length);
        app.Selection.SetRange(prefix.Length,prefix.Length);document.UndoClear();
    }
    public object State=>new{active=Active,mode,status=Status,raw=Raw,nativeUpdates=NativeUpdates,
        candidates=Candidates?.Candidates.Select(c=>new{id=c.Id,kind=c.Kind,latex=CandidateExporter.ToLatex(c)}).ToArray(),
        contentEligibility=Candidates?.ContentEligibility,autoProductAllowed=false};
    public void Stop(string reason){Active=false;Status=reason;}
    public void DocumentClosing(WordApi.Document closing)
    {if(Identity(closing)==documentIdentity)Stop("Tài liệu đã yêu cầu đóng; phiên thử hết hiệu lực kể cả khi hủy đóng.");}
    public void Tick(InputObservation input)
    {
        if(!Active||writing||ticking)return;
        ticking=true;
        try{
        if(!CurrentTarget()){Stop($"Đã dừng vì đổi tài liệu/cửa sổ. expectedWindow={window}, actualWindow={app.ActiveWindow.Hwnd}, sameDocument={Identity(app.ActiveDocument)==documentIdentity}");return;}
        if(input.Escapes!=initialEscapes||input.Returns!=initialReturns){Stop("Enter/Esc kết thúc phiên thử và giữ nội dung hiện tại; không tự chốt thêm.");return;}
        if(!input.EditorFocus){lastSpace=input.Spaces;Status="Tạm dừng vì focus ngoài vùng soạn thảo.";return;}
        if(input.MessageComposition||input.CompositionBytes!=0||input.QuietMilliseconds<300)return;
        try
        {
            Refresh();
            bool space=input.Spaces!=lastSpace;lastSpace=input.Spaces;
            if(app.Selection.Start!=app.Selection.End||app.Selection.Start!=End())
            {Status="Đang sửa/chọn vùng: chỉ xem trước, không tự đổi native.";return;}
            if(space&&Raw.EndsWith(" ",StringComparison.Ordinal)&&mode=="native-live"&&Candidates?.ContentEligibility=="eligible")
            {Replace(Candidates,0);Status="Đã cập nhật native; vẫn giữ toàn bộ nguồn để nối tiếp.";}
        }
        catch(Exception error) when(error is InvalidOperationException||error is ArgumentException||error is System.Runtime.InteropServices.COMException)
        {Stop("Đã dừng; giữ nội dung hiện tại. "+error.Message);}
        }finally{ticking=false;}
    }
    private static long Identity(WordApi.Document value){IntPtr pointer=Marshal.GetIUnknownForObject(value);try{return pointer.ToInt64();}finally{Marshal.Release(pointer);}}
    private bool CurrentTarget()=>Identity(app.ActiveDocument)==documentIdentity&&app.ActiveWindow.Hwnd==window;
    private int End()=>document.Content.End-suffix.Length;
    private void ValidateFrame()
    {
        if(document.ReadOnly||document.TrackRevisions||document.ProtectionType!=WordApi.WdProtectionType.wdNoProtection||document.Tables.Count!=0||document.Fields.Count!=0||document.InlineShapes.Count!=0)
            throw new InvalidOperationException("trial-context-changed");
        if(document.Content.End>8192||document.Range(0,prefix.Length).Text!=prefix||document.Range(End(),document.Content.End).Text!=suffix)
            throw new InvalidOperationException("trial-boundary-changed");
    }
    private void Refresh()
    {
        ValidateFrame();
        if(controlId.Length==0)
        {
            if(document.OMaths.Count!=0||document.ContentControls.Count!=0)throw new InvalidOperationException("trial-structure-changed");
            Raw=document.Range(prefix.Length,End()).Text??"";
        }
        else
        {
            var control=document.ContentControls.Cast<WordApi.ContentControl>().SingleOrDefault(c=>c.ID==controlId);
            if(control==null||document.ContentControls.Count!=1||document.OMaths.Count!=1)throw new InvalidOperationException("trial-undo-or-native-changed");
            var known=WordOperations.ReadUnique(document,control);
            if(known.Candidates.OriginalReplacement!=committedRaw)throw new InvalidOperationException("trial-metadata-changed");
            int nativeEnd=document.OMaths[1].Range.End;
            if(nativeEnd>End())throw new InvalidOperationException("trial-tail-changed");
            Raw=committedRaw+(document.Range(nativeEnd,End()).Text??"");
        }
        if(Raw.Length>1024||Raw.IndexOfAny(new[]{'\r','\n','\a'})>=0)throw new InvalidOperationException("trial-source-shape");
        var analysis=new AnalysisEngine().Analyze(new SourceSnapshot(Raw),new AnalysisOptions(InputMode.Explicit));
        Candidates=analysis.Regions.SingleOrDefault();
        Status=Candidates==null?"Nguồn chưa đủ để dựng công thức.":Candidates.ContentEligibility=="eligible"?"Một cách hiểu trực tiếp. Có thể gõ tiếp hoặc chốt.":"Cần chọn kết quả; Space giữ nguyên nội dung.";
    }
    public void Commit(int choice,string? expectedRaw=null)
    {
        if(!Active||!CurrentTarget())throw new InvalidOperationException("trial-not-active");
        Refresh();if(expectedRaw!=null&&Raw!=expectedRaw)throw new InvalidOperationException("trial-stale-choice");
        if(Candidates==null)throw new InvalidOperationException("trial-no-candidate");
        Replace(Candidates,choice);Stop("Đã chốt. Tiếp tục viết câu văn trong Word.");
    }
    public void KeepText()
    {
        if(!Active||!CurrentTarget())throw new InvalidOperationException("trial-not-active");
        Refresh();if(controlId.Length>0)WriteRegion("<w:r><w:t xml:space=\"preserve\">"+SecurityElement.Escape(Raw)+"</w:t></w:r>",null);
        Stop("Đã giữ nguyên văn nguồn; phiên thử kết thúc.");
    }
    private void Replace(CandidateSet set,int choice)
    {
        if(choice<0||choice>=set.Candidates.Count)throw new ArgumentOutOfRangeException(nameof(choice));
        var snapshot=new ManagedSnapshot(set.Select(set.Candidates[choice].Id));snapshot.Encode();
        string xml=CandidateExporter.ToOmml(snapshot.Selected);
        WriteRegion(xml,snapshot);committedRaw=set.OriginalReplacement;NativeUpdates++;
    }
    private void WriteRegion(string xml,ManagedSnapshot? expected)
    {
        NativeInputState.RequireWordThread((IntPtr)app.ActiveWindow.Hwnd);ValidateFrame();
        if(app.UndoRecord.IsRecordingCustomRecord)throw new InvalidOperationException("trial-nested-undo");
        int start=prefix.Length,end=End();string before=document.Content.Text;
        int beforeMath=document.OMaths.Count,beforeControls=document.ContentControls.Count;
        string payload=expected?.Encode()??"";
        writing=true;bool attempted=false;
        app.Selection.SetRange(start,end);
        app.UndoRecord.StartCustomRecord("Locus W0: update source session");
        try
        {
            attempted=true;app.Selection.InsertXML(WordOperations.Package(xml));
            // An imported sdt wrapper extends into text typed at its right edge on this
            // Word build. Attach the control to Word's native math range, as in manual conversion.
            foreach(WordApi.ContentControl old in document.ContentControls.Cast<WordApi.ContentControl>().ToArray())old.Delete(false);
            if(expected!=null)
            {
                if(document.OMaths.Count!=1)throw new InvalidOperationException("trial-native-count");
                var control=document.ContentControls.Add(WordApi.WdContentControlType.wdContentControlRichText,document.OMaths[1].Range);
                control.Title="Locus W0 session";control.Tag=payload;
                if(control.Tag!=payload)throw new InvalidOperationException("trial-tag-readback");
            }
        }
        catch{app.UndoRecord.EndCustomRecord();writing=false;if(attempted&&(document.Content.Text!=before||document.OMaths.Count!=beforeMath||document.ContentControls.Count!=beforeControls)&&!document.Undo(1))throw new InvalidOperationException("trial-rollback-failed");throw;}
        finally{if(app.UndoRecord.IsRecordingCustomRecord)app.UndoRecord.EndCustomRecord();}
        try
        {
            ValidateFrame();
            if(expected!=null)
            {
                if(document.ContentControls.Count!=1||document.OMaths.Count!=1)throw new InvalidOperationException("trial-native-shape");
                var control=document.ContentControls[1];
                if(WordOperations.ReadUnique(document,control).Encode()!=expected.Encode())throw new InvalidOperationException("trial-snapshot-differs");
                controlId=control.ID;WordOperations.PlaceCaretAfterEquation(document,app.Selection,control);
            }
            else
            {
                if(document.OMaths.Count!=0||document.ContentControls.Count!=0||document.Range(start,End()).Text!=Raw)throw new InvalidOperationException("trial-source-restore-differs");
                controlId="";app.Selection.SetRange(End(),End());
            }
        }
        catch{if(!document.Undo(1))throw new InvalidOperationException("trial-validation-rollback-failed");throw;}
        finally{writing=false;}
    }
    // Deterministic replay is kept separate from the real Space/focus path.
    public void ReplayTail(string text,bool commitSpace)
    {
        if(!Active||!CurrentTarget())throw new InvalidOperationException("trial-not-active");
        Refresh();
        if(app.Selection.Start!=End()||app.Selection.End!=End())throw new InvalidOperationException("trial-replay-caret");
        app.Selection.TypeText(text);Refresh();
        if(commitSpace&&mode=="native-live"&&Candidates?.ContentEligibility=="eligible")Replace(Candidates,0);
    }
}
