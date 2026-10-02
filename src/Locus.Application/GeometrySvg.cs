using System.Globalization;
using System.Security;
using System.Text;

namespace Locus.Application;

public readonly record struct ScreenPosition(double X,double Y,double Depth=0);
public sealed class GeometryProjection
{
    public const double Width=1000,Height=640;
    private readonly GeometryDocument document;
    private readonly Position right,up,forward;
    private readonly double scale,cx,cy;
    public GeometryProjection(GeometryDocument document)
    {
        this.document=document;
        if(document.Dimension==2)
        {
            var v=document.Viewport??new(-7.8125,5,7.8125,-5);scale=Math.Min(Width/(v.Right-v.Left),Height/(v.Top-v.Bottom));cx=(v.Left+v.Right)/2;cy=(v.Top+v.Bottom)/2;
            right=new(1,0);up=new(0,1);forward=new(0,0,1);
        }
        else
        {
            var camera=document.Camera??new();double a=camera.Azimuth*Math.PI/180,e=camera.Elevation*Math.PI/180;
            right=new(Math.Cos(a),Math.Sin(a));up=new(-Math.Sin(a)*Math.Sin(e),Math.Cos(a)*Math.Sin(e),Math.Cos(e));forward=right.Cross(up);scale=camera.Scale;
        }
    }
    public ScreenPosition Project(Position p)
    {
        var cam=document.Camera??new();return document.Dimension==2?new(Width/2+(p.X-cx)*scale,Height/2-(p.Y-cy)*scale):new(Width/2+p.Dot(right)*scale+cam.PanX,Height/2-p.Dot(up)*scale+cam.PanY,p.Dot(forward));
    }
    public double WorldPerPixel=>1/scale;
    public Position? Unproject(double x,double y,string plane="XY",double depth=0)
    {
        if(document.Dimension==2)return new((x-Width/2)/scale+cx,(Height/2-y)/scale+cy);
        var camera=document.Camera??new();var ray=right*((x-Width/2-camera.PanX)/scale)+up*((Height/2-y+camera.PanY)/scale);
        double denominator=plane=="XY"?forward.Z:plane=="XZ"?forward.Y:forward.X;
        if(Math.Abs(denominator)<1e-5)return null;
        double coordinate=plane=="XY"?ray.Z:plane=="XZ"?ray.Y:ray.X;
        return ray+forward*((depth-coordinate)/denominator);
    }
}

public static class GeometrySvg
{
    private static string N(double n)=>n.ToString("0.#####",CultureInfo.InvariantCulture);
    private static string E(string text)=>SecurityElement.Escape(text)??"";
    private static string Dash(string line)=>line=="dashed"?"9 6":line=="dotted"?"2 5":"none";
    public static string Render(GeometryDocument d,string? selected=null,bool editing=false,GeometrySnap? snap=null,string? pendingPoint=null,Position? pointer=null)
    {
        var resolved=GeometryEngine.Resolve(d);var projection=new GeometryProjection(d);
        ScreenPosition? Point(string id)=>resolved.Points.TryGetValue(id,out var p)&&p.HasValue?projection.Project(p.Value):null;
        string Hit(string id)=>editing?$" data-geo-id=\"{id}\"":"";
        var svg=new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1000\" height=\"640\" viewBox=\"0 0 1000 640\" data-background=\"white\" role=\"img\" aria-label=\"Hình học\"><rect width=\"1000\" height=\"640\" fill=\"white\"/><defs><marker id=\"geo-arrow\" viewBox=\"0 0 10 10\" refX=\"9\" refY=\"5\" markerWidth=\"6\" markerHeight=\"6\" orient=\"auto-start-reverse\"><path d=\"M0 0L10 5L0 10Z\" fill=\"context-stroke\"/></marker></defs>");
        var faces=(d.Faces??[]).Where(f=>f.Visible&&f.Vertices.All(v=>Point(v)!=null)).Select(f=>(Face:f,Points:f.Vertices.Select(v=>Point(v)!.Value).ToArray())).ToArray();
        foreach(var face in faces.OrderBy(f=>f.Points.Average(p=>p.Depth)))svg.Append($"<polygon{Hit(face.Face.Id)} points=\"{string.Join(" ",face.Points.Select(p=>N(p.X)+","+N(p.Y)))}\" fill=\"{face.Face.Color}\" fill-opacity=\"{N(face.Face.Opacity)}\" stroke=\"{(selected==face.Face.Id&&editing?"#73a892":"none")}\"/>");
        foreach(var c in d.Circles??[])
        {
            if(!c.Visible||Point(c.Center) is not {} center||Point(c.Through) is not {} edge)continue;
            double radius=Math.Sqrt(Math.Pow(edge.X-center.X,2)+Math.Pow(edge.Y-center.Y,2));if(radius<1e-8)continue;
            svg.Append($"<circle{Hit(c.Id)} cx=\"{N(center.X)}\" cy=\"{N(center.Y)}\" r=\"{N(radius)}\" fill=\"none\" stroke=\"{c.Color}\" stroke-width=\"{N(c.Width)}\" stroke-dasharray=\"{Dash(c.LineStyle)}\"/>");
            if(editing)svg.Append($"<circle{Hit(c.Id)} cx=\"{N(center.X)}\" cy=\"{N(center.Y)}\" r=\"{N(radius)}\" fill=\"none\" stroke=\"{(selected==c.Id?"#65aa8738":"transparent")}\" stroke-width=\"14\"/>");
        }
        foreach(var s in d.Segments.Where(s=>s.Visible))
        {
            if(Point(s.Start) is not {} a||Point(s.End) is not {} b)continue;
            double dx=b.X-a.X,dy=b.Y-a.Y,len=Math.Sqrt(dx*dx+dy*dy);if(len<1e-8)continue;
            if(s.Kind is "line" or "ray")
            {
                double start=s.Kind=="line"?-3000/len:0,end=3000/len;
                double depth=b.Depth-a.Depth;
                b=new(a.X+dx*end,a.Y+dy*end,a.Depth+depth*end);a=new(a.X+dx*start,a.Y+dy*start,a.Depth+depth*start);
            }
            var fragments=d.Dimension==3&&s.Hidden=="auto"?Visibility(a,b,faces.Select(f=>f.Points)):new[]{(Start:0d,End:1d,Hidden:s.Hidden=="hidden")};
            foreach(var fragment in fragments)
            {
                var p=Lerp(a,b,fragment.Start);var q=Lerp(a,b,fragment.End);string style=fragment.Hidden?"dashed":s.LineStyle;
                svg.Append($"<path{Hit(s.Id)} data-edge=\"{s.Id}\" data-hidden=\"{fragment.Hidden.ToString().ToLowerInvariant()}\" d=\"M{N(p.X)} {N(p.Y)}L{N(q.X)} {N(q.Y)}\" fill=\"none\" stroke=\"{s.Color}\" stroke-width=\"{N(s.Width)}\" stroke-dasharray=\"{Dash(style)}\"{(s.Kind=="vector"&&fragment.End==1?" marker-end=\"url(#geo-arrow)\"":"")}/>");
            }
            for(int m=0;m<s.Marks;m++)
            {
                double offset=(m-(s.Marks-1)/2d)*6,x=(a.X+b.X)/2+dx/len*offset,y=(a.Y+b.Y)/2+dy/len*offset;
                svg.Append($"<path d=\"M{N(x-dy/len*5)} {N(y+dx/len*5)}L{N(x+dy/len*5)} {N(y-dx/len*5)}\" stroke=\"{s.Color}\" stroke-width=\"1.5\"/>");
            }
            if(editing)svg.Append($"<path{Hit(s.Id)} d=\"M{N(a.X)} {N(a.Y)}L{N(b.X)} {N(b.Y)}\" stroke=\"{(s.Id==selected?"#55997738":"transparent")}\" stroke-width=\"14\" fill=\"none\"/>");
        }
        foreach(var angle in d.Angles??[])
        {
            if(resolved.Points[angle.A] is not {} aw||resolved.Points[angle.Vertex] is not {} bw||resolved.Points[angle.C] is not {} cw||GeometryEngine.Angle(aw,bw,cw,angle.Reflex) is not {} measure)continue;
            var a=projection.Project(aw);var b=projection.Project(bw);var c=projection.Project(cw);
            double start=Math.Atan2(a.Y-b.Y,a.X-b.X),finish=Math.Atan2(c.Y-b.Y,c.X-b.X),delta=finish-start;while(delta>Math.PI)delta-=Math.PI*2;while(delta< -Math.PI)delta+=Math.PI*2;if(angle.Reflex)delta+=delta>0?-2*Math.PI:2*Math.PI;
            // In space the arc is sampled in the actual plane spanned by the two rays.
            var u=(aw-bw)*(1/(aw-bw).Length);var v=(cw-bw)*(1/(cw-bw).Length);var normal=u.Cross(v);var tangent=normal.Length<1e-10?new Position(-u.Y,u.X,0):normal.Cross(u)*(1/normal.Length);
            for(int ring=0;ring<angle.Arcs;ring++)
            {
                double radius=angle.Radius+ring*5;var points=new List<ScreenPosition>();
                for(int i=0;i<=48;i++)
                {
                    double t=(d.Dimension==3?(angle.Reflex?-measure:measure):delta)*i/48;
                    points.Add(d.Dimension==3?projection.Project(bw+(u*Math.Cos(t)+tangent*Math.Sin(t))*(radius*projection.WorldPerPixel)):new(b.X+Math.Cos(start+t)*radius,b.Y+Math.Sin(start+t)*radius));
                }
                svg.Append($"<path{Hit(angle.Id)} d=\"{string.Join("",points.Select((p,i)=>(i==0?"M":"L")+N(p.X)+" "+N(p.Y)))}\" stroke=\"{angle.Color}\" stroke-width=\"{(editing&&selected==angle.Id?3:1.6)}\" stroke-dasharray=\"{Dash(angle.LineStyle)}\" fill=\"none\"/>");
            }
            string text=angle.Label+(angle.ShowValue?(angle.Label.Length>0?" = ":"")+(angle.Unit=="degree"?(measure*180/Math.PI).ToString("0.#",CultureInfo.InvariantCulture)+"°":measure.ToString("0.###",CultureInfo.InvariantCulture)+" rad"):"");
            var label=d.Dimension==3?projection.Project(bw+(u*Math.Cos((angle.Reflex?-measure:measure)/2)+tangent*Math.Sin((angle.Reflex?-measure:measure)/2))*((angle.Radius+20)*projection.WorldPerPixel)):new ScreenPosition(b.X+Math.Cos(start+delta/2)*(angle.Radius+20),b.Y+Math.Sin(start+delta/2)*(angle.Radius+20));
            svg.Append($"<text{Hit(angle.Id)} x=\"{N(label.X)}\" y=\"{N(label.Y)}\" text-anchor=\"middle\" font-family=\"Arial,sans-serif\" font-size=\"16\" fill=\"{angle.Color}\">{E(text)}</text>");
        }
        foreach(var p in d.Points.Where(p=>p.Visible))
        {
            if(Point(p.Id) is not {} position)continue;
            if(editing)svg.Append($"<circle{Hit(p.Id)} cx=\"{N(position.X)}\" cy=\"{N(position.Y)}\" r=\"12\" fill=\"{(selected==p.Id?"#73a89255":"transparent")}\"/>");
            svg.Append($"<circle{Hit(p.Id)} cx=\"{N(position.X)}\" cy=\"{N(position.Y)}\" r=\"3.5\" fill=\"{(p.Relation==null?"#28705c":"#9b7149")}\"/>");
            svg.Append($"<text{(editing?$" data-geo-label=\"{p.Id}\"":"")} x=\"{N(position.X+p.LabelX)}\" y=\"{N(position.Y+p.LabelY)}\" font-family=\"Arial,sans-serif\" font-size=\"18\" fill=\"#344c40\">{E(p.Label)}</text>");
        }
        foreach(var l in d.Labels??[]){var p=projection.Project(new(l.X,l.Y,l.Z));svg.Append($"<text{Hit(l.Id)} x=\"{N(p.X)}\" y=\"{N(p.Y)}\" font-family=\"Arial,sans-serif\" font-size=\"18\" fill=\"{l.Color}\">{E(l.Text)}</text>");}
        if(editing&&pendingPoint!=null&&Point(pendingPoint) is {} pending&&pointer is {} mouse){var p=projection.Project(mouse);svg.Append($"<path d=\"M{N(pending.X)} {N(pending.Y)}L{N(p.X)} {N(p.Y)}\" stroke=\"#789a89\" stroke-dasharray=\"4 5\" fill=\"none\" pointer-events=\"none\"/>");}
        if(editing&&snap!=null){var p=projection.Project(snap.Position);svg.Append($"<g pointer-events=\"none\"><circle cx=\"{N(p.X)}\" cy=\"{N(p.Y)}\" r=\"10\" fill=\"#ebfaf3\" stroke=\"#2b9567\" stroke-width=\"2\"/><text x=\"{N(p.X+15)}\" y=\"{N(p.Y-15)}\" fill=\"#28705c\" font-family=\"Arial,sans-serif\" font-size=\"14\">{E(snap.Label)}</text></g>");}
        return svg.Append("</svg>").ToString();
    }
    private static ScreenPosition Lerp(ScreenPosition a,ScreenPosition b,double t)=>new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Depth+(b.Depth-a.Depth)*t);
    /// <summary>Orthographic edge clipping against projected face triangles, splitting at exact depth crossings.</summary>
    internal static (double Start,double End,bool Hidden)[] Visibility(ScreenPosition a,ScreenPosition b,IEnumerable<ScreenPosition[]> faces)
    {
        var intervals=new List<(double Start,double End)>();
        static double Cross(ScreenPosition p,ScreenPosition q,ScreenPosition r)=>(q.X-p.X)*(r.Y-p.Y)-(q.Y-p.Y)*(r.X-p.X);
        foreach(var face in faces)for(int i=1;i+1<face.Length;i++)
        {
            var tri=new[]{face[0],face[i],face[i+1]};double area=Cross(tri[0],tri[1],tri[2]);if(Math.Abs(area)<1e-8)continue;
            double lo=0,hi=1;bool rejected=false;
            for(int j=0;j<3;j++)
            {
                double p=Cross(tri[j],tri[(j+1)%3],a)*Math.Sign(area),q=Cross(tri[j],tri[(j+1)%3],b)*Math.Sign(area);
                if(p<0&&q<0){rejected=true;break;}if(p<0)lo=Math.Max(lo,p/(p-q));if(q<0)hi=Math.Min(hi,p/(p-q));
            }
            if(rejected||hi-lo<1e-10)continue;
            double Depth(ScreenPosition p)=>(Cross(tri[1],tri[2],p)*tri[0].Depth+Cross(tri[2],tri[0],p)*tri[1].Depth+Cross(tri[0],tri[1],p)*tri[2].Depth)/area-p.Depth-1e-7;
            double d0=Depth(Lerp(a,b,lo)),d1=Depth(Lerp(a,b,hi));if(d0<=0&&d1<=0)continue;
            if(d0<=0)lo=lo+(hi-lo)*d0/(d0-d1);else if(d1<=0)hi=lo+(hi-lo)*d0/(d0-d1);
            if(hi-lo>1e-10)intervals.Add((lo,hi));
        }
        var cuts=intervals.SelectMany(i=>new[]{i.Start,i.End}).Append(0).Append(1).Distinct().Order().ToArray();var output=new List<(double,double,bool)>();
        for(int i=0;i+1<cuts.Length;i++){double mid=(cuts[i]+cuts[i+1])/2;bool hidden=intervals.Any(p=>mid>=p.Start&&mid<=p.End);if(output.Count>0&&output[^1].Item3==hidden){var previous=output[^1];output[^1]=(previous.Item1,cuts[i+1],hidden);}else output.Add((cuts[i],cuts[i+1],hidden));}return output.ToArray();
    }
}
