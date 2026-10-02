using System;
using System.Drawing;
using System.Windows.Forms;
using WordApi=Microsoft.Office.Interop.Word;

namespace Locus.Word;

// One anchor belongs to one sandbox/document/window/source revision. No cached coordinates.
internal sealed class BadgeController:IDisposable
{
    private readonly ProbeBadge form=new();
    private WordApi.Range? anchor;
    private string documentId="",raw="",source="";
    private int start,end,window,fontDpi;
    private Font? badgeFont;
    public object State{get;private set;}=new{visible=false,reason="disabled"};
    public event Action? Clicked;
    public BadgeController(string label="Locus fx W0"){form.Text=label;form.AccessibleName=label;form.Click+=(_,_)=>Clicked?.Invoke();}
    public void Arm(WordApi.Application app,string id)
    {
        Clear();
        if(app.Selection.StoryType!=WordApi.WdStoryType.wdMainTextStory||app.Selection.Start==app.Selection.End)throw new InvalidOperationException("select-badge-source");
        anchor=app.Selection.Range.Duplicate;start=anchor.Start;end=anchor.End;source=anchor.Text;
        documentId=id;raw=app.ActiveDocument.Content.Text;window=app.ActiveWindow.Hwnd;
    }
    public void Clear(){anchor=null;Hide("disabled");}
    private void Hide(string reason){form.Hide();State=new{visible=false,reason};}
    public void Update(WordApi.Application app,string? id,InputObservation input)
    {
        if(anchor==null){Hide("no-anchor");return;}
        if(id!=documentId||app.ActiveWindow.Hwnd!=window){Hide("different-document-or-window");return;}
        if(!input.EditorFocus){Hide("outside-editor");return;}
        if(app.ActiveDocument.Content.Text!=raw||anchor.Start!=start||anchor.End!=end||anchor.Text!=source){anchor=null;Hide("source-changed");return;}
        if(!BadgePlacement.TryEditorViewport((IntPtr)window,out var viewport,out int dpi)){Hide("viewport-unavailable");return;}
        app.ActiveWindow.GetPoint(out int x,out int y,out int w,out int h,anchor);
        var formula=new Rectangle(x,y,w,h);var placement=BadgePlacement.Place(formula,viewport,dpi);
        if(!placement.HasValue){Hide("outside-viewport-or-no-space");return;}
        form.Bounds=placement.Value;
        if(fontDpi!=dpi){badgeFont?.Dispose();badgeFont=new Font("Segoe UI",14*dpi/96f,FontStyle.Bold,GraphicsUnit.Pixel);form.Font=badgeFont;fontDpi=dpi;}
        if(!form.Visible)form.Show(new Owner((IntPtr)window));
        State=new{visible=true,reason="anchored",documentId,window,start,end,dpi,formula,viewport,bounds=form.Bounds,noActivate=true};
    }
    public void Unavailable(){Hide("host-unavailable");}
    public void Dispose(){form.Dispose();badgeFont?.Dispose();anchor=null;}
    private sealed class Owner:IWin32Window{public IntPtr Handle{get;}public Owner(IntPtr handle){Handle=handle;}}
}

internal sealed class ProbeBadge:Form
{
    public ProbeBadge()
    {
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;AutoScaleMode=AutoScaleMode.None;StartPosition=FormStartPosition.Manual;
        BackColor=Color.FromArgb(224,244,235);ForeColor=Color.FromArgb(20,92,63);Text="Locus fx W0";AccessibleName="Locus fx W0";
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint,true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {base.OnPaint(e);TextRenderer.DrawText(e.Graphics,"fx",Font,ClientRectangle,ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);ControlPaint.DrawBorder(e.Graphics,ClientRectangle,Color.SeaGreen,ButtonBorderStyle.Solid);}
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var value=base.CreateParams;value.ExStyle|=0x08000000|0x80;return value;}}
}
