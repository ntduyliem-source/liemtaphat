using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using Locus.Word;
using WordApi=Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static void RunUxResearch()
    {
        Test("space/two-native-variants-and-undo",()=>
        {
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;
            var rows=new List<object>();
            foreach(string mode in new[]{"keep-source","native-live"})
            foreach(var pieces in new[]{new[]{"x ","mũ ","2 ","cộng ","1 "},new[]{"1 ","trên ","2 "},new[]{"căn ","x ","cộng ","1 "}})
            {
                connector.StartSpaceTrial(mode,false);var doc=app.ActiveDocument;documents.Add(doc);
                foreach(string piece in pieces)
                {
                    string before=State(doc);var state=json.Deserialize<Dictionary<string,object>>((string)connector.SpaceTrialAction("replay",piece,0));
                    rows.Add(new{mode,piece,state,native=doc.OMaths.Count});Write("space-variants.json",rows);
                    Check((string)state["raw"]==string.Concat(pieces.Take(Array.IndexOf(pieces,piece)+1)),"Session source split.");
                    if(mode=="keep-source")Check(doc.OMaths.Count==0,"Keep-source changed Word early.");
                }
                string prior=State(doc);connector.SpaceTrialAction("commit","",0);string committed=State(doc);
                Check(doc.OMaths.Count==1&&WordOperations.ReadUnique(doc,doc.ContentControls[1]).Candidates.OriginalReplacement==string.Concat(pieces),"Final source/candidate differs.");
                Check(doc.Undo(1)&&State(doc)==prior,"Commit Undo differs.");Check(doc.Redo(1)&&State(doc)==committed,"Commit Redo differs.");Close(doc);
            }
        });
        Test("space/repair-blocked-and-source-restore",()=>
        {
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;connector.StartSpaceTrial("native-live",false);var doc=app.ActiveDocument;documents.Add(doc);
            connector.SpaceTrialAction("replay","x+1/2 ",0);Check(doc.OMaths.Count==0,"Repair set converted on Space.");
            connector.SpaceTrialAction("keep-text","",0);Check(doc.Content.Text.Contains("x+1/2 ")&&doc.OMaths.Count==0,"Keep source differs.");Close(doc);
            connector.StartSpaceTrial("native-live",false);doc=app.ActiveDocument;documents.Add(doc);connector.SpaceTrialAction("replay","x mu\u0303 2 ",0);
            string native=State(doc);connector.SpaceTrialAction("keep-text","",0);Check(doc.Content.Text.Contains("x mu\u0303 2 ")&&doc.OMaths.Count==0&&doc.ContentControls.Count==0,"NFD source restore differs.");
            Check(doc.Undo(1)&&State(doc)==native,"Undo keep-text differs.");
        });
        Test("space/undo-update-stops-session-and-preserves-tail",()=>
        {
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;connector.StartSpaceTrial("native-live",false);var doc=app.ActiveDocument;documents.Add(doc);
            connector.SpaceTrialAction("replay","x mũ 2 ",0);string initial=State(doc);
            connector.SpaceTrialAction("replay","cộng 1 ",0);Check(doc.Undo(1),"No update Undo.");
            Check(doc.OMaths.Count==1&&doc.Content.Text.Contains("cộng 1 ")&&WordOperations.ReadUnique(doc,doc.ContentControls[1]).Candidates.OriginalReplacement=="x mũ 2 ","Undo update lost prior equation or typed tail.");
            Reject(()=>connector.SpaceTrialAction("commit","",0));
            Check(doc.Content.Text.Contains("cộng 1 "),"Stale update removed tail.");
        });
        Test("space/stale-document-and-outside-source",()=>
        {
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;connector.StartSpaceTrial("keep-source",false);var doc=app.ActiveDocument;documents.Add(doc);
            connector.SpaceTrialAction("replay","x^2 ",0);string original=State(doc);var other=New("other");
            string otherBefore=State(other);Reject(()=>connector.SpaceTrialAction("replay","never write",0));Check(State(other)==otherBefore,"Replay wrote into another document.");
            Reject(()=>connector.SpaceTrialAction("commit","",0));Check(State(doc)==original,"Wrong document committed.");Close(other);doc.Activate();
            doc.Range(0,1).Text="Y";string changed=State(doc);Reject(()=>connector.SpaceTrialAction("commit","",0));Check(State(doc)==changed,"Changed prefix was overwritten.");
        });
        Test("space/cancelled-close-expires-session",()=>
        {
            dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;connector.StartSpaceTrial("native-live",false);
            var doc=app.ActiveDocument;documents.Add(doc);connector.SpaceTrialAction("replay","x mũ 2 ",0);string before=State(doc);
            connector.CancelSandboxCloseForResearch();
            Check(app.Documents.Count==1&&State(doc)==before,"Cancelled close changed the document.");
            var state=json.Deserialize<Dictionary<string,object>>((string)connector.GetSpaceTrialState());
            Check(!(bool)state["active"]&&!(bool)Observe(connector)["sandbox"],"Cancelled close left source session armed.");
            Reject(()=>connector.SpaceTrialAction("replay","cộng 1 ",0));Reject(()=>connector.SpaceTrialAction("commit","",0));Reject(()=>connector.SpaceTrialAction("keep-text","",0));
            Check(State(doc)==before,"Expired source session wrote after cancelled close.");
        });
        Test("badge/layout-dpi-and-clipping",()=>
        {
            var observations=new List<object>();
            foreach(int dpi in new[]{96,120,144,192})
            foreach(var viewport in new[]{new Rectangle(0,0,800,600),new Rectangle(-1920,100,1280,900)})
            {
                var range=new Rectangle(viewport.Left+200,viewport.Top+100,100,40);var placement=BadgePlacement.Place(range,viewport,dpi);
                Check(placement.HasValue&&viewport.Contains(placement.Value)&&!placement.Value.IntersectsWith(range),"Badge overlaps source or viewport.");
                Check(placement.GetValueOrDefault().Width==(int)Math.Ceiling(42*dpi/96d),"DPI width differs.");
                Check(!BadgePlacement.Place(new Rectangle(viewport.Left,viewport.Top-20,20,40),viewport,dpi).HasValue,"Partly scrolled-out source accepted.");
                Check(!BadgePlacement.Place(new Rectangle(viewport.Left,viewport.Top-2000,20,40),viewport,dpi).HasValue,"Offscreen source accepted.");
                observations.Add(new{dpi,viewport,range,bounds=placement.GetValueOrDefault()});
            }
            Check(!BadgePlacement.Place(new Rectangle(0,0,20,20),new Rectangle(0,0,20,20),96).HasValue,"No-space viewport accepted.");
            Write("badge-layout.json",new{method="Pure layout tests; synthetic DPI and monitor origins, not physical display acceptance.",observations});
        });
        Test("badge/actual-window-zoom-scroll-and-expiry",()=>
        {
            app.Visible=true;dynamic connector=app.COMAddIns.Item("Locus.Word.W0").Object;
            connector.CreateSandbox("x^2\r"+string.Concat(Enumerable.Repeat("A paragraph for W0 scrolling.\r",100)));var doc=app.ActiveDocument;documents.Add(doc);
            doc.Activate();app.Activate();var window=app.ActiveWindow;window.View.Type=WordApi.WdViewType.wdPrintView;
            app.Selection.SetRange(0,3);window.ScrollIntoView(app.Selection.Range,true);
            Stage("badge/wait-for-real-editor-focus");var waiting=System.Diagnostics.Stopwatch.StartNew();
            while(!(bool)((Dictionary<string,object>)Observe(connector)["input"])["EditorFocus"]&&waiting.Elapsed.TotalSeconds<30)Thread.Sleep(200);
            connector.SetBadgeEnabled(true);
            var first=json.Deserialize<Dictionary<string,object>>((string)connector.GetBadgeState());
            Write("badge-first.json",new{state=first,host=Observe(connector)});Check((bool)first["visible"],"Badge unavailable in focused Word editor.");
            int[] selection={app.Selection.Start,app.Selection.End};string before=doc.Content.Text;
            window.View.Zoom.Percentage=150;window.ScrollIntoView(doc.Range(0,3),true);var zoom=json.Deserialize<Dictionary<string,object>>((string)connector.GetBadgeState());
            Check((bool)zoom["visible"],"Zoom hid visible range.");
            window.ScrollIntoView(doc.Range(doc.Content.End-2,doc.Content.End-1),true);var scrolled=json.Deserialize<Dictionary<string,object>>((string)connector.GetBadgeState());Check(!(bool)scrolled["visible"],"Offscreen badge left visible.");
            window.ScrollIntoView(doc.Range(0,3),true);var returned=json.Deserialize<Dictionary<string,object>>((string)connector.GetBadgeState());Check((bool)returned["visible"],"Badge failed to follow scroll back.");
            var other=app.Windows.Add(window);other.Activate();var different=json.Deserialize<Dictionary<string,object>>((string)connector.GetBadgeState());Check(!(bool)different["visible"],"Anchor leaked into a different window.");other.Close();window.Activate();
            Check(doc.Content.Text==before&&app.Selection.Start==selection[0]&&app.Selection.End==selection[1],"Badge changed source/selection.");
            doc.Range(0,1).Text="y";var stale=json.Deserialize<Dictionary<string,object>>((string)connector.GetBadgeState());Check(!(bool)stale["visible"],"Changed source kept old badge.");
            Write("badge-host.json",new{first,zoom,scrolled,returned,different,stale,method="Actual Word/WinForms geometry, visibility and selection checks; not screenshot or multi-DPI display acceptance."});connector.SetBadgeEnabled(false);
        });
    }
}
