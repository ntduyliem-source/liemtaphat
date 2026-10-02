using System.Globalization;
using System.Xml.Linq;

namespace Locus.WebProbe;

public sealed record ScenePoint(string Id, double X, double Y);
public sealed class Scene
{
    public ScenePoint[] Points {get;private set;}=[new("A",130,260),new("B",410,260),new("C",280,90)];
    private readonly Stack<ScenePoint[]> undo=new(), redo=new();
    public bool CanUndo=>undo.Count>0;
    public bool CanRedo=>redo.Count>0;
    public void Move(string id,double x,double y)
    {
        if(!double.IsFinite(x)||!double.IsFinite(y)||x<20||x>540||y<20||y>320||!Points.Any(p=>p.Id==id))throw new ArgumentException("Điểm nằm ngoài khung thử.");
        var next=Points.Select(p=>p.Id==id?p with {X=Math.Round(x,2),Y=Math.Round(y,2)}:p).ToArray();
        if(Points.SequenceEqual(next))return;
        undo.Push(Points);redo.Clear();Points=next;
    }
    public void Undo(){if(CanUndo){redo.Push(Points);Points=undo.Pop();}}
    public void Redo(){if(CanRedo){undo.Push(Points);Points=redo.Pop();}}
    public string Svg()
    {
        XNamespace ns="http://www.w3.org/2000/svg";
        static string N(double value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
        var root=new XElement(ns+"svg",new XAttribute("viewBox","0 0 560 340"),new XAttribute("width",560),new XAttribute("height",340),
            new XElement(ns+"title","Tam giác thử Locus"),
            new XElement(ns+"polygon",new XAttribute("points",string.Join(" ",Points.Select(p=>N(p.X)+","+N(p.Y)))),new XAttribute("fill","#e4f0ec"),new XAttribute("stroke","#28705c"),new XAttribute("stroke-width",2)));
        foreach(var p in Points){root.Add(new XElement(ns+"circle",new XAttribute("cx",N(p.X)),new XAttribute("cy",N(p.Y)),new XAttribute("r",6),new XAttribute("fill","#28705c")));root.Add(new XElement(ns+"text",new XAttribute("x",N(p.X+12)),new XAttribute("y",N(p.Y-12)),new XAttribute("font-size",18),p.Id));}
        return root.ToString(SaveOptions.DisableFormatting);
    }
}
