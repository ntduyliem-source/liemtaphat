using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Locus.Core;

namespace Locus.Word;

internal sealed class SpaceTrialPanel:Form
{
    private readonly Label status=new(){Dock=DockStyle.Top,Height=52,Padding=new Padding(10)};
    private readonly TextBox source=new(){Dock=DockStyle.Top,Height=45,ReadOnly=true,Multiline=true};
    private readonly FormulaSketch preview=new(){Dock=DockStyle.Fill};
    private readonly FlowLayoutPanel choices=new(){Dock=DockStyle.Bottom,Height=46};
    private string displayedRaw="",displayedKey="";
    private readonly SpaceTrial trial;
    public SpaceTrialPanel(SpaceTrial trial)
    {
        this.trial=trial;Text="Locus W0 — thử nối công thức";Width=760;Height=320;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;
        var actions=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48};
        var keep=new Button{Text="Giữ văn bản",AutoSize=true};keep.Click+=(_,_)=>Run(()=>{if(trial.Raw!=displayedRaw)throw new InvalidOperationException("Nguồn đã đổi.");trial.KeepText();});
        var stop=new Button{Text="Dừng thử",AutoSize=true};stop.Click+=(_,_)=>{trial.Stop("Đã dừng; giữ nguyên tài liệu.");RefreshTrial();};
        actions.Controls.Add(keep);actions.Controls.Add(stop);
        Controls.Add(preview);Controls.Add(source);Controls.Add(status);Controls.Add(choices);Controls.Add(actions);
        FormClosing+=(_,_)=>trial.Stop("Đóng khung thử; giữ nguyên tài liệu.");RefreshTrial();
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var value=base.CreateParams;value.ExStyle|=0x08000000;return value;}}
    private void Run(Action action){try{action();}catch(Exception error){status.Text=error.Message;return;}RefreshTrial();}
    public void RefreshTrial()
    {
        string heading=trial.Mode=="native-live"?"B — Space cập nhật native, tiếp tục cùng nguồn":"A — Giữ nguồn trong Word, chốt khi chọn kết quả";
        status.Text=heading+Environment.NewLine+trial.Status;
        displayedRaw=trial.Raw;source.Text=displayedRaw;
        var candidates=trial.Candidates?.Candidates;
        string key=string.Join(";",candidates?.Select(c=>c.Id)??Enumerable.Empty<string>());
        if(key!=displayedKey)
        {
            displayedKey=key;choices.Controls.Clear();preview.Candidate=candidates?.FirstOrDefault();preview.Invalidate();
            if(candidates!=null)for(int i=0;i<candidates.Count;i++)
            {
                int index=i;var candidate=candidates[i];string raw=displayedRaw;
                var button=new Button{AutoSize=true,Text="Chốt "+(i+1)+" — "+(candidate.Kind=="repair"?"đề nghị sửa":candidate.Kind=="direct"?"trực tiếp":"cách hiểu khác")};
                button.MouseEnter+=(_,_)=>{preview.Candidate=candidate;preview.Invalidate();};
                button.Click+=(_,_)=>Run(()=>trial.Commit(index,raw));
                choices.Controls.Add(button);
            }
        }
        choices.Enabled=trial.Active;
    }
}

// Research preview from the exact core tree; deliberately parenthesizes binary subexpressions.
internal sealed class FormulaSketch:Control
{
    public Candidate? Candidate{get;set;}
    public FormulaSketch(){DoubleBuffered=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(Candidate==null)return;
        e.Graphics.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        Draw(e.Graphics,Candidate.Document.Root,16,Height/2f,25);
    }
    private static float DrawText(Graphics g,string text,float x,float baseline,float size,bool paint=true)
    {using var font=new Font("Cambria Math",size,FontStyle.Regular,GraphicsUnit.Pixel);if(paint)g.DrawString(text,font,Brushes.Black,x,baseline-size);return g.MeasureString(text,font).Width;}
    private static float Draw(Graphics g,MathNode n,float x,float y,float size,bool paint=true)
    {
        if(n.Type=="Number"||n.Type=="Symbol")return DrawText(g,n.Value??n.Name!,x,y,size,paint);
        if(n.Type=="Power")
        {
            bool grouped=n.Children[0].Type=="Power"||n.Children[0].Type=="Unary"||n.Children[0].Operator=="divide";
            float a=grouped?DrawText(g,"(",x,y,size,paint):0;
            a+=Draw(g,n.Children[0],x+a,y,size,paint);
            if(grouped)a+=DrawText(g,")",x+a,y,size,paint);
            return a+Draw(g,n.Children[1],x+a,y-size*.6f,size*.65f,paint);
        }
        if(n.Type=="Sqrt")
        {float a=DrawText(g,"√(",x,y,size,paint);a+=Draw(g,n.Children[0],x+a,y,size,paint);return a+DrawText(g,")",x+a,y,size,paint);}
        if(n.Operator=="divide")
        {
            float a=Draw(g,n.Children[0],0,0,size*.85f,false),b=Draw(g,n.Children[1],0,0,size*.85f,false),w=Math.Max(a,b)+10;
            if(paint){Draw(g,n.Children[0],x+(w-a)/2,y-size*.4f,size*.85f);g.DrawLine(Pens.Black,x,y,x+w,y);Draw(g,n.Children[1],x+(w-b)/2,y+size*.9f,size*.85f);}
            return w+6;
        }
        if(n.Type=="Unary"){float sign=DrawText(g,n.Operator=="minus"?"−":"+",x,y,size,paint);return sign+Draw(g,n.Children[0],x+sign,y,size,paint);}
        string symbol=n.Operator=="add"?"+":n.Operator=="subtract"?"−":n.Operator=="multiply"?"·":n.Operator=="eq"?"=":n.Operator=="lt"?"<":n.Operator=="gt"?">":n.Operator=="le"?"≤":"≥";
        float width=DrawText(g,"(",x,y,size,paint);width+=Draw(g,n.Children[0],x+width,y,size,paint);width+=DrawText(g," "+symbol+" ",x+width,y,size,paint);width+=Draw(g,n.Children[1],x+width,y,size,paint);width+=DrawText(g,")",x+width,y,size,paint);return width;
    }
}
