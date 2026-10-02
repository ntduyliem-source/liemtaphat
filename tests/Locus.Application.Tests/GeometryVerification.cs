using System.Text.Json;
using Locus.Application;

internal static class GeometryVerification
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);var checks=new List<object>();int failed=0;
        void Assert(bool value,string reason="Contract failed"){if(!value)throw new Exception(reason);}
        void Near(double a,double b)=>Assert(Math.Abs(a-b)<1e-9,$"{a} != {b}");
        void Check(string name,Action action){try{action();checks.Add(new{name,passed=true});}catch(Exception e){failed++;checks.Add(new{name,passed=false,error=e.ToString()});Console.Error.WriteLine($"FAIL {name}: {e.Message}");}}
        GeometryDocument Triangle()=>new(Guid.NewGuid(),0,[new("A",0,3,"A"),new("B",-3,0,"B"),new("C",3,0,"C")],[new("AB","A","B"),new("BC","B","C"),new("CA","C","A")],[],[],[],[],new(-7.8125,5,7.8125,-5));
        Check("starts-empty-2d-3d-independent",()=>{var model=new GeometryWorkspaceModel();model.Plane.AddPoint(new(1,2));Assert(model.Plane.Document.Points.Length==1&&model.Space.Document.Points.Length==0&&model.Space.Document.Dimension==3);});
        Check("midpoint-foot-angle-update-from-parent",()=>{
            var d=Triangle();d=d with{Points=[..d.Points,new("M",99,99,"M",Relation:new("midpoint",["B","C"])),new("H",99,99,"H",Relation:new("foot",["A","B","C"]))]};
            var r=GeometryEngine.Resolve(d);Near(r.Points["M"]!.Value.X,0);Near(r.Points["H"]!.Value.Y,0);Near(GeometryEngine.Angle(r.Points["B"]!.Value,r.Points["H"]!.Value,r.Points["A"]!.Value)!.Value,Math.PI/2);
            d=d with{Points=d.Points.Select(p=>p.Id=="C"?p with{Y=2}:p).ToArray()};r=GeometryEngine.Resolve(d);Near(r.Points["M"]!.Value.Y,1);var direction=r.Points["C"]!.Value-r.Points["B"]!.Value;var altitude=r.Points["A"]!.Value-r.Points["H"]!.Value;Near(direction.Dot(altitude),0);
        });
        Check("snap-midpoint-excludes-own-dependents",()=>{var d=Triangle();var target=GeometryEngine.Snap(d,new(0,0.04),"A",0.1);Assert(target?.Relation.Kind=="midpoint"&&target.Relation.Parents.SequenceEqual(new[]{"B","C"}));d=d with{Points=[..d.Points,new("M",0,0,"M",Relation:new("midpoint",["A","B"]))]};var invalid=GeometryEngine.Snap(d,new(-1.5,1.5),"A",0.01);Assert(invalid==null);});
        Check("explicit-constraints-and-degenerate-state",()=>{
            var d=Triangle();d=d with{Points=[..d.Points,new("P",0,0,"P",Relation:new("parallel",["A","B","C"],Distance:2)),new("V",0,0,"V",Relation:new("perpendicular",["A","B","C"],Distance:2)),new("Q",0,0,"Q",Relation:new("fixedAngle",["B","C"],60,3))]};GeometryEngine.Validate(d);var r=GeometryEngine.Resolve(d);Near(r.Points["P"]!.Value.Y,3);Near(r.Points["V"]!.Value.X,0);Near(GeometryEngine.Angle(r.Points["C"]!.Value,r.Points["B"]!.Value,r.Points["Q"]!.Value)!.Value,Math.PI/3);
            d=d with{Points=d.Points.Select(p=>p.Id=="C"?p with{X=-3}:p).ToArray()};r=GeometryEngine.Resolve(d);Assert(r.Points["P"]==null&&r.Points["V"]==null&&r.Points["Q"]==null&&r.Problems.Length==3);
        });
        Check("point-on-circle-remains-on-updated-circle",()=>{var d=Triangle();d=d with{Points=[..d.Points,new("P",0,0,"P",Relation:new("onCircle",["B","C"],Math.PI/2))]};var r=GeometryEngine.Resolve(d);Near(r.Points["P"]!.Value.X,-3);Near(r.Points["P"]!.Value.Y,6);});
        Check("cycle-and-dangling-ref-rejected-without-mutation",()=>{var s=new GeometrySession();s.Load(Triangle());var before=s.Document;try{s.Apply(before with{Points=before.Points.Select(p=>p.Id=="A"?p with{Relation=new("midpoint",["B","C"])}:p.Id=="B"?p with{Relation=new("coincident",["A"])}:p).ToArray()});throw new Exception("Cycle accepted");}catch(FormatException){}Assert(s.Document==before);try{s.Apply(before with{Segments=[new("bad","A","missing")]});throw new Exception("Dangling ref accepted");}catch(FormatException){}Assert(s.Document==before);});
        Check("drag-single-undo-escape-restores-relation",()=>{var s=new GeometrySession();s.Load(Triangle());var before=s.Document;s.BeginGesture();for(int i=0;i<60;i++)s.Move("C",new(3,i/10d));s.EndGesture();Assert(s.History(false)&&s.Document==before);Assert(!s.CanUndo&&s.CanRedo);s.History(true);var moved=s.Document;s.BeginGesture();s.Move("C",new(9,9));s.EndGesture(true);Assert(s.Document==moved);});
        Check("delete-parent-cascade-and-undo",()=>{var d=Triangle();d=d with{Points=[..d.Points,new("M",0,0,"M",Relation:new("midpoint",["B","C"]))],Segments=[..d.Segments,new("AM","A","M")],Angles=[new("angle","B","A","C")]};var s=new GeometrySession();s.Load(d);s.Remove("B");Assert(s.Document.Points.All(p=>p.Id is not("B" or "M"))&&s.Document.Segments.Length==1&&s.Document.Angles!.Length==0);Assert(s.History(false)&&s.Document==d);});
        Check("angle-reflex-radian-and-zero-length",()=>{Near(GeometryEngine.Angle(new(1,0),new(),new(0,1))!.Value,Math.PI/2);Near(GeometryEngine.Angle(new(1,0),new(),new(0,1),true)!.Value,3*Math.PI/2);Assert(GeometryEngine.Angle(new(),new(),new(0,1))==null);});
        Check("3d-angle-measured-in-world-not-projected",()=>{var a=new Position(1,0,0);var b=new Position();var c=new Position(0,0,1);Near(GeometryEngine.Angle(a,b,c)!.Value,Math.PI/2);var d=new GeometrySession(3).Document;var p=new GeometryProjection(d);var pa=p.Project(a);var pb=p.Project(b);var pc=p.Project(c);double projected=GeometryEngine.Angle(new(pa.X,pa.Y),new(pb.X,pb.Y),new(pc.X,pc.Y))!.Value;Assert(Math.Abs(projected-Math.PI/2)>0.1);});
        Check("projection-unprojection-fixed-plane-roundtrip",()=>{var d=new GeometrySession(3).Document;var p=new GeometryProjection(d);foreach(var plane in new[]{"XY","XZ","YZ"}){var point=new Position(2,-1,3);var screen=p.Project(point);var back=p.Unproject(screen.X,screen.Y,plane,plane=="XY"?point.Z:plane=="XZ"?point.Y:point.X)!.Value;Near((back-point).Length,0);}var edge=new GeometryProjection(d with{Camera=new(0,0)});Assert(edge.Unproject(500,320,"XY")==null);});
        Check("3d-hidden-edge-clips-partial-occlusion",()=>{
            var d=new GeometryDocument(Guid.NewGuid(),0,[new("A",-2,0,"A"),new("B",2,0,"B"),new("P",0,1,"P",-1),new("Q",3,1,"Q",-1),new("R",3,1,"R",1),new("S",0,1,"S",1)],[new("AB","A","B")],Faces:[new("f",["P","Q","R","S"])],Dimension:3,Camera:new(0,0));
            // At this camera forward is -Y: the occluding face must be closer, at Y=-1.
            d=d with{Points=d.Points.Select(p=>p.Id is "P" or "Q" or "R" or "S"?p with{Y=-1}:p).ToArray()};
            var svg=GeometrySvg.Render(d);Assert(svg.Contains("data-hidden=\"true\"")&&svg.Contains("data-hidden=\"false\""));File.WriteAllText(Path.Combine(directory,"hidden-clipping.svg"),svg);
            svg=GeometrySvg.Render(d with{Segments=[d.Segments[0] with{Hidden="visible"}]});Assert(!svg.Contains("data-hidden=\"true\""));
        });
        Check("3d-extended-line-preserves-depth",()=>{
            var a=new Position(-2,1);var b=new Position(2,-1);
            var d=new GeometryDocument(Guid.NewGuid(),0,[new("A",a.X,a.Y,"A"),new("B",b.X,b.Y,"B"),new("P",-4,-1.8,"P",-1),new("Q",4,-1.8,"Q",-1),new("R",4,-1.8,"R",1),new("S",-4,-1.8,"S",1)],[new("AB","A","B",Kind:"line")],Faces:[new("face",["P","Q","R","S"])],Dimension:3,Camera:new(0,0));
            var projection=new GeometryProjection(d);var pa=projection.Project(a);var pb=projection.Project(b);double factor=3000/Math.Sqrt(Math.Pow(pb.X-pa.X,2)+Math.Pow(pb.Y-pa.Y,2));
            var start=a+(b-a)*-factor;var end=a+(b-a)*factor;
            var equivalent=d with{Points=d.Points.Select(p=>p.Id=="A"?p with{X=start.X,Y=start.Y,Z=start.Z}:p.Id=="B"?p with{X=end.X,Y=end.Y,Z=end.Z}:p).ToArray(),Segments=[d.Segments[0] with{Kind="segment"}]};
            string[] Edges(string svg)=>System.Xml.Linq.XDocument.Parse(svg).Descendants().Where(e=>e.Attribute("data-hidden")!=null).Select(e=>e.ToString()).ToArray();
            Assert(Edges(GeometrySvg.Render(d)).SequenceEqual(Edges(GeometrySvg.Render(equivalent))));
        });
        Check("nonplanar-face-recoverable-by-undo",()=>{
            var s=new GeometrySession(3);var d=s.Document with{Points=[new("A",0,0,"A"),new("B",2,0,"B"),new("C",2,2,"C"),new("D",0,2,"D")],Faces=[new("f",["A","B","C","D"])]};
            s.Load(d);Assert(GeometryEngine.Resolve(s.Document).Problems.Length==0);s.Move("C",new(2,2,1));Assert(GeometryEngine.Resolve(s.Document).Problems.Any(p=>p.Contains("đồng phẳng")));Assert(s.History(false)&&GeometryEngine.Resolve(s.Document).Problems.Length==0);
        });
        Check("malformed-legacy-null-elements-retain-original",()=>{
            foreach(var kind in new[]{"plot","geometry"})foreach(var version in new[]{1,kind=="plot"?8:9})
            {
                var id=Guid.NewGuid();var payload=kind=="plot"?JsonSerializer.Serialize(new{id,revision=0,curves=new object?[]{null},viewport=new{left=-5,top=5,right=5,bottom=-5}}):JsonSerializer.Serialize(new{id,revision=0,points=new object?[]{null},segments=Array.Empty<object>()});
                var json=JsonSerializer.Serialize(new{format=DocumentCodec.Format,version,kind,id,revision=0,payload,sha256=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)))});
                Assert(!DocumentCodec.Open(json).IsSupported);
            }
        });
        Check("version9-cross-host-roundtrip-and-svg-no-controls",()=>{
            var d=Triangle();d=d with{Points=[..d.Points,new("H",0,0,"H",Relation:new("foot",["A","B","C"]),LabelX:22)],Segments=[..d.Segments,new("AH","A","H","dashed","#b36644",3)],Angles=[new("ang","B","A","C",true,"radian",2,45,"α")],Circles=[new("circle","A","B")],Faces=[new("face",["A","B","C"])],Labels=[new("label",-3,-1,"<script>")]};var json=DocumentCodec.Serialize(d);Assert(json.Contains("\"version\":9"));var open=DocumentCodec.Open(json).Document as GeometryDocument;Assert(open!=null&&DocumentCodec.Serialize(open)==json);var svg=GeometrySvg.Render(open!);Assert(!svg.Contains("data-geo-id")&&!svg.Contains("data-geo-label")&&!svg.Contains("<script>")&&svg.Contains("&lt;script&gt;")&&svg.Contains("#b36644"));File.WriteAllText(Path.Combine(directory,"geometry.svg"),svg);File.WriteAllText(Path.Combine(directory,"geometry.locus"),json);
        });
        var report=new{passed=checks.Count-failed,failed,checks};File.WriteAllText(Path.Combine(directory,"geometry-verification.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(new{passed=checks.Count-failed,failed}));return failed==0?0:1;
    }
}
