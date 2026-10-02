using System;
using System.Linq;
using System.Web.Script.Serialization;
using Locus.Core;
using Locus.Core.Detection;
using WordApi=Microsoft.Office.Interop.Word;

namespace Locus.Word;

// W0-only experiment. Called on the Word UI thread with a fresh owned sandbox.
internal static class ContinuationResearch
{
    public static string Run(WordApi.Application app,string raw,string suffix,string variant)
    {
        NativeInputState.RequireWordThread((IntPtr)app.ActiveWindow.Hwnd);
        if(raw.Length>100||suffix.Length>100)throw new InvalidOperationException("research-input-limit");
        var doc=app.ActiveDocument;
        doc.Content.Text="Before: "+raw+suffix+"\r";
        app.Selection.SetRange(8,8+raw.Length);doc.UndoClear();
        var set=new AnalysisEngine().Analyze(new SourceSnapshot(raw),new AnalysisOptions(InputMode.Explicit)).Regions.Single();
        var snapshot=new ManagedSnapshot(set.Select(set.Candidates[0].Id));
        string original=doc.Content.Text;
        WordOperations.ConvertSelected(app,snapshot,()=>{});
        var control=doc.ContentControls[1];
        string tag=control.Tag,converted=doc.Content.Text;
        string signature=NativeMathSignature.FromXml(control.Range.WordOpenXML);
        int edge=control.Range.End,nativeEnd=doc.OMaths[1].Range.End;
        switch(variant)
        {
            case "adapter-default":break;
            case "edge":app.Selection.SetRange(edge,edge);break;
            case "select-control-right":control.Range.Select();app.Selection.MoveRight(WordApi.WdUnits.wdCharacter,1);break;
            case "select-control-right-twice":control.Range.Select();app.Selection.MoveRight(WordApi.WdUnits.wdCharacter,2);break;
            case "native-end":app.Selection.SetRange(nativeEnd,nativeEnd);break;
            case "select-tail-start":
                doc.Range(nativeEnd,doc.Content.End-1).Select();app.Selection.Collapse(WordApi.WdCollapseDirection.wdCollapseStart);break;
            case "select-next-left":
                doc.Range(nativeEnd,nativeEnd+1).Select();app.Selection.MoveLeft(WordApi.WdUnits.wdCharacter,1);break;
            case "next-collapse-start":
                doc.Range(nativeEnd,nativeEnd+1).Select();app.Selection.Collapse(WordApi.WdCollapseDirection.wdCollapseStart);break;
            case "edge-right":app.Selection.MoveRight(WordApi.WdUnits.wdCharacter,1);break;
            default:throw new InvalidOperationException("unknown-continuation-variant");
        }
        int caret=app.Selection.Start;
        app.Selection.TypeText(" cộng 1");
        string typed=doc.Content.Text;
        bool nativeUnchanged=doc.OMaths.Count==1&&NativeMathSignature.FromXml(doc.OMaths[1].Range.WordOpenXML)==signature;
        bool associationIntact=false;
        try{associationIntact=WordOperations.ReadUnique(doc,control).Encode()==snapshot.Encode();}catch(InvalidOperationException){}
        string outside=doc.Range(control.Range.End,doc.Content.End).Text;
        bool outsideExact=outside==" cộng 1"+suffix+"\r\r";
        bool typingUndo=doc.Undo(1)&&doc.Content.Text==converted&&doc.ContentControls.Count==1&&doc.ContentControls[1].Tag==tag;
        bool convertUndo=doc.Undo(1)&&doc.Content.Text==original&&doc.OMaths.Count==0&&doc.ContentControls.Count==0;
        return new JavaScriptSerializer().Serialize(new{raw,suffix,variant,edge,nativeEnd,caret,typed,outside,outsideExact,nativeUnchanged,associationIntact,typingUndo,convertUndo,
            method="In-process Word Selection API. Not physical keyboard input or a product Space trigger."});
    }
}
