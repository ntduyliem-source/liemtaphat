using System.Text.RegularExpressions;

namespace Locus.Application;

public readonly record struct Position(double X, double Y, double Z = 0)
{
    public static Position operator +(Position a, Position b) => new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    public static Position operator -(Position a, Position b) => new(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
    public static Position operator *(Position a, double b) => new(a.X*b,a.Y*b,a.Z*b);
    public double Length => Math.Sqrt(Dot(this));
    public double Dot(Position b) => X*b.X+Y*b.Y+Z*b.Z;
    public Position Cross(Position b) => new(Y*b.Z-Z*b.Y,Z*b.X-X*b.Z,X*b.Y-Y*b.X);
}
public sealed record GeometryResolution(IReadOnlyDictionary<string,Position?> Points, string[] Problems);
public sealed record GeometrySnap(Position Position, GeometryRelation Relation, string Label);

public static class GeometryEngine
{
    public static void Validate(GeometryDocument d)
    {
        if(d.Points==null||d.Segments==null||d.Points.Length>2048||d.Segments.Length>4096||d.Dimension is not (2 or 3)||(d.Circles?.Length??0)>512||(d.Angles?.Length??0)>512||(d.Faces?.Length??0)>512||(d.Labels?.Length??0)>512)throw new FormatException("Invalid geometry capacity.");
        var ids=new HashSet<string>();
        void Id(string id){if(id==null||!Regex.IsMatch(id,"\\A[A-Za-z0-9_-]{1,64}\\z")||!ids.Add(id))throw new FormatException("Invalid/duplicate geometry identity.");}
        bool Number(double n)=>double.IsFinite(n)&&Math.Abs(n)<=1e6;
        void Color(string c){if(c==null||!Regex.IsMatch(c,"\\A#[a-fA-F0-9]{6}\\z"))throw new FormatException("Invalid color.");}
        void Style(string style,double width){if(style is not ("solid" or "dashed" or "dotted")||!double.IsFinite(width)||width<0.5||width>12)throw new FormatException("Invalid line style.");}
        foreach(var p in d.Points)
        {
            if(p==null)throw new FormatException();Id(p.Id);
            if(!Number(p.X)||!Number(p.Y)||!Number(p.Z)||d.Dimension==2&&p.Z!=0||p.Label==null||p.Label.Length>64||!Number(p.LabelX)||!Number(p.LabelY))throw new FormatException("Invalid point.");
        }
        var pointIds=d.Points.Select(p=>p.Id).ToHashSet();
        void Parents(string[] parents,int min,int max){if(parents==null||parents.Length<min||parents.Length>max||parents.Distinct().Count()!=parents.Length||parents.Any(p=>!pointIds.Contains(p)))throw new FormatException("Invalid geometry parents.");}
        foreach(var p in d.Points)
        {
            if(p.Relation is not {} r)continue;
            int count=r.Kind switch{"coincident"=>1,"midpoint" or "onLine" or "onCircle" or "fixedAngle"=>2,"foot" or "parallel" or "perpendicular"=>3,_=>throw new FormatException("Unknown relation.")};
            Parents(r.Parents,count,count);if(!Number(r.Value)||!Number(r.Distance)||r.Distance<=0||r.Parents.Contains(p.Id)||d.Dimension==3&&r.Kind is "fixedAngle" or "perpendicular" or "onCircle"||r.Attached&&r.Kind is not("coincident" or "midpoint"))throw new FormatException("Invalid relation.");
        }
        var map=d.Points.ToDictionary(p=>p.Id);var visiting=new HashSet<string>();var done=new HashSet<string>();
        void Visit(string id,int depth)
        {
            if(depth>128||visiting.Contains(id))throw new FormatException("Geometry relation cycle/depth.");if(done.Contains(id))return;
            visiting.Add(id);foreach(var parent in map[id].Relation?.Parents??[])Visit(parent,depth+1);visiting.Remove(id);done.Add(id);
        }
        foreach(var p in d.Points)Visit(p.Id,0);
        foreach(var s in d.Segments){if(s==null)throw new FormatException();Id(s.Id);Parents([s.Start,s.End],2,2);Color(s.Color);Style(s.LineStyle,s.Width);if(s.Kind is not("segment" or "line" or "ray" or "vector")||s.Hidden is not("auto" or "visible" or "hidden")||s.Marks is <0 or >3)throw new FormatException("Invalid edge.");}
        foreach(var c in d.Circles??[]){if(c==null||d.Dimension!=2)throw new FormatException();Id(c.Id);Parents([c.Center,c.Through],2,2);Color(c.Color);Style(c.LineStyle,c.Width);}
        foreach(var a in d.Angles??[]){if(a==null)throw new FormatException();Id(a.Id);Parents([a.A,a.Vertex,a.C],3,3);Color(a.Color);Style(a.LineStyle,2);if(a.Unit is not("degree" or "radian")||a.Arcs is <1 or >3||!Number(a.Radius)||a.Radius<10||a.Radius>160||a.Label==null||a.Label.Length>64)throw new FormatException("Invalid angle.");}
        foreach(var f in d.Faces??[]){if(f==null)throw new FormatException();Id(f.Id);Parents(f.Vertices,3,64);Color(f.Color);if(!double.IsFinite(f.Opacity)||f.Opacity<0||f.Opacity>1)throw new FormatException();}
        foreach(var l in d.Labels??[]){if(l==null)throw new FormatException();Id(l.Id);if(!Number(l.X)||!Number(l.Y)||!Number(l.Z)||l.Text==null||l.Text.Length>512)throw new FormatException("Invalid label.");Color(l.Color);}
        if(d.Viewport is {} v&&(!new[]{v.Left,v.Right,v.Top,v.Bottom}.All(Number)||v.Right-v.Left<1e-6||v.Top-v.Bottom<1e-6))throw new FormatException("Invalid view.");
        if(d.Camera is {} cam&&(!new[]{cam.Azimuth,cam.Elevation,cam.PanX,cam.PanY,cam.Scale}.All(Number)||cam.Scale<1||cam.Scale>2000||cam.Elevation < -90||cam.Elevation>90))throw new FormatException("Invalid camera.");
    }
    public static GeometryResolution Resolve(GeometryDocument d)
    {
        var map=d.Points.ToDictionary(p=>p.Id);var resolved=new Dictionary<string,Position?>();var problems=new List<string>();
        Position? Get(string id)
        {
            if(resolved.TryGetValue(id,out var prior))return prior;var p=map[id];Position? value=new(p.X,p.Y,p.Z);
            if(p.Relation is {} r)
            {
                var parents=r.Parents.Select(Get).ToArray();
                if(parents.Any(v=>v==null))value=null;
                else
                {
                    var a=parents[0]!.Value;var b=parents.Length>1?parents[1]!.Value:a;var delta=b-a;double length=delta.Length;
                    value=r.Kind switch
                    {
                        "coincident"=>a,"midpoint"=>(a+b)*0.5,"onLine"=>length<1e-10?null:a+delta*r.Value,
                        "onCircle"=>length<1e-10?null:a+new Position(Math.Cos(r.Value),Math.Sin(r.Value))*length,
                        "fixedAngle"=>length<1e-10?null:a+Rotate(delta*(r.Distance/length),r.Value*Math.PI/180),
                        "foot"=>Foot(a,b,parents[2]!.Value),
                        "parallel"=>DirectionPoint(a,b,parents[2]!.Value,r.Distance,false),
                        "perpendicular"=>DirectionPoint(a,b,parents[2]!.Value,r.Distance,true),_=>null
                    };
                }
                if(value==null)problems.Add($"{p.Label}: quan hệ không xác định do điểm cha trùng hoặc không còn hợp lệ.");
            }
            if(value is {} pos&&(!double.IsFinite(pos.X)||!double.IsFinite(pos.Y)||!double.IsFinite(pos.Z)||pos.Length>1e7)){value=null;problems.Add($"{p.Label}: vượt miền tọa độ.");}
            resolved[id]=value;return value;
        }
        foreach(var p in d.Points)Get(p.Id);
        foreach(var face in d.Faces??[])
        {
            var vertices=face.Vertices.Select(Get).ToArray();
            if(vertices.Any(p=>p==null)){problems.Add("Mặt có đỉnh không xác định.");continue;}
            var points=vertices.Select(p=>p!.Value).ToArray();var normal=new Position();
            for(int i=1;i+1<points.Length&&normal.Length<1e-10;i++)normal=(points[i]-points[0]).Cross(points[i+1]-points[0]);
            if(normal.Length<1e-10){problems.Add("Mặt / đa giác suy biến: các đỉnh thẳng hàng.");continue;}
            if(d.Dimension==3&&points.Any(p=>Math.Abs((p-points[0]).Dot(normal))>normal.Length*1e-7))problems.Add("Mặt "+string.Join("",face.Vertices.Select(id=>map[id].Label))+" không còn đồng phẳng; bản xem trước chia mặt thành tam giác. Chỉnh lại đỉnh trước khi xuất.");
        }
        return new(resolved,problems.ToArray());
    }
    public static Position Rotate(Position p,double angle)=>new(p.X*Math.Cos(angle)-p.Y*Math.Sin(angle),p.X*Math.Sin(angle)+p.Y*Math.Cos(angle),p.Z);
    public static Position? Foot(Position p,Position a,Position b){var d=b-a;return d.Dot(d)<1e-20?null:a+d*((p-a).Dot(d)/d.Dot(d));}
    private static Position? DirectionPoint(Position p,Position a,Position b,double distance,bool perpendicular){var d=b-a;if(d.Length<1e-10)return null;return p+(perpendicular?new Position(-d.Y,d.X):d)*(distance/d.Length);}
    public static double? Angle(Position a,Position b,Position c,bool reflex=false)
    {
        var u=a-b;var v=c-b;if(u.Length<1e-10||v.Length<1e-10)return null;
        double angle=Math.Acos(Math.Clamp(u.Dot(v)/(u.Length*v.Length),-1,1));return reflex?Math.PI*2-angle:angle;
    }
    public static bool DependsOn(GeometryDocument d,string child,string ancestor)
    {
        var seen=new HashSet<string>();var map=d.Points.ToDictionary(p=>p.Id);
        bool Contains(string id)=>id==ancestor||seen.Add(id)&&(map[id].Relation?.Parents.Any(Contains)??false);
        return Contains(child);
    }
    public static GeometrySnap? Snap(GeometryDocument d,Position target,string? moving,double threshold)
    {
        var r=Resolve(d);var options=new List<GeometrySnap>();
        bool Allowed(string id)=>id!=moving&&(moving==null||!DependsOn(d,id,moving));
        foreach(var p in d.Points.Where(p=>p.Visible&&Allowed(p.Id)))if(r.Points[p.Id] is {} pos)options.Add(new(pos,new("coincident",[p.Id],Attached:true),p.Label));
        foreach(var s in d.Segments.Where(s=>s.Visible&&Allowed(s.Start)&&Allowed(s.End)))
        {
            if(r.Points[s.Start] is not {} a||r.Points[s.End] is not {} b)continue;
            options.Add(new((a+b)*0.5,new("midpoint",[s.Start,s.End],Attached:true),"Trung điểm"));
        }
        return options.Where(s=>(s.Position-target).Length<=threshold).OrderBy(s=>(s.Position-target).Length).FirstOrDefault();
    }
}

public sealed class GeometrySession
{
    public GeometryDocument Document{get;private set;}
    public long Version{get;private set;}
    private readonly List<GeometryDocument> undo=[],redo=[];private GeometryDocument? gesture;
    public bool CanUndo=>undo.Count>0||gesture!=null;public bool CanRedo=>redo.Count>0;
    public GeometrySession(int dimension=2){Document=new(Guid.NewGuid(),0,[],[],[],[],[],[],new(-7.5,5,7.5,-5),dimension,new());}
    public void BeginGesture(){gesture??=Document;}
    public void EndGesture(bool cancel=false){if(gesture==null)return;if(cancel){Document=gesture;Version++;}else if(gesture!=Document)Push(gesture);gesture=null;}
    private void Push(GeometryDocument d){undo.Add(d);if(undo.Count>80)undo.RemoveAt(0);redo.Clear();}
    public void Apply(GeometryDocument next){next=next with{Id=Document.Id,Revision=Document.Revision+1};GeometryEngine.Validate(next);if(gesture==null)Push(Document);Document=next;Version++;}
    public void Load(GeometryDocument d){GeometryEngine.Validate(d);gesture=null;undo.Clear();redo.Clear();Document=d;Version++;}
    public bool History(bool forward){EndGesture();var a=forward?redo:undo;var b=forward?undo:redo;if(a.Count==0)return false;b.Add(Document);Document=a[^1];a.RemoveAt(a.Count-1);Version++;return true;}
    public static string Id()=>Guid.NewGuid().ToString("N");
    public string AddPoint(Position pos,GeometryRelation? relation=null)
    {
        string id=Id(),label="";for(int n=0;n<10000;n++){label=n<26?((char)('A'+n)).ToString():"P"+(n-25);if(Document.Points.All(p=>p.Label!=label))break;}
        Apply(Document with{Points=[..Document.Points,new(id,pos.X,pos.Y,label,pos.Z,relation)]});return id;
    }
    public void Move(string id,Position pos,GeometryRelation? relation=null)=>Apply(Document with{Points=Document.Points.Select(p=>p.Id==id?p with{X=pos.X,Y=pos.Y,Z=pos.Z,Relation=relation}:p).ToArray()});
    public void Remove(string id)
    {
        var removed=Document.Points.Where(p=>GeometryEngine.DependsOn(Document,p.Id,id)).Select(p=>p.Id).Append(id).ToHashSet();
        Apply(Document with{Points=Document.Points.Where(p=>!removed.Contains(p.Id)).ToArray(),Segments=Document.Segments.Where(s=>!removed.Contains(s.Id)&&!removed.Contains(s.Start)&&!removed.Contains(s.End)).ToArray(),Circles=Document.Circles?.Where(c=>!removed.Contains(c.Id)&&!removed.Contains(c.Center)&&!removed.Contains(c.Through)).ToArray(),Angles=Document.Angles?.Where(a=>!removed.Contains(a.Id)&&!new[]{a.A,a.Vertex,a.C}.Any(removed.Contains)).ToArray(),Faces=Document.Faces?.Where(f=>!removed.Contains(f.Id)&&!f.Vertices.Any(removed.Contains)).ToArray(),Labels=Document.Labels?.Where(l=>l.Id!=id).ToArray()});
    }
}
public sealed class GeometryWorkspaceModel
{
    public GeometrySession Plane{get;}=new();public GeometrySession Space{get;}=new(3);
    public int Dimension{get;set;}=2;
}
