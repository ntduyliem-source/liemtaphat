using System.Globalization;
using System.Text;
using Locus.Application;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class GeometryWorkspace
{
    [Parameter]public string Host{get;set;}="browser";
    [Parameter]public IEditorWindow? DesktopWindow{get;set;}
    private int dimension=2;
    private GeometrySession Session=>dimension==2?Model.Plane:Model.Space;
    private GeometryDocument Doc=>Session.Document;
    private GeometryResolution resolved=new(new Dictionary<string,Position?>(),[]);
    private string scene="",tool="select",notice="",plane="XY",labelText="Nhãn",draftStatus="";
    private bool snapping=true,contextMenu,autoSave=true,disposed;
    private string? selected;
    private double givenAngle=60,givenLength=3,solidWidth=4,solidDepth=3,solidHeight=3;
    private readonly List<string> pending=[];
    private GeometrySnap? snap;
    private Position? pointer;
    private GeometryDocument? dragDocument;
    private GeometryResolution? dragResolved;
    private string? dragId,dragLabel;
    private double downX,downY,dragDepth;
    private ElementReference root;
    private IJSObjectReference? module,storage,renderer;
    private DotNetObjectReference<GeometryWorkspace>? reference;
    private CancellationTokenSource? saveDelay;
    private byte[]? retained;
    private string retainedName="";
    private GeometryPoint? SelectedPoint=>Doc.Points.FirstOrDefault(p=>p.Id==selected);
    private GeometrySegment? SelectedSegment=>Doc.Segments.FirstOrDefault(s=>s.Id==selected);
    private GeometryCircle? SelectedCircle=>Doc.Circles?.FirstOrDefault(c=>c.Id==selected);
    private GeometryAngle? SelectedAngle=>Doc.Angles?.FirstOrDefault(a=>a.Id==selected);
    private GeometryFace? SelectedFace=>Doc.Faces?.FirstOrDefault(f=>f.Id==selected);
    private GeometryLabel? SelectedLabel=>Doc.Labels?.FirstOrDefault(l=>l.Id==selected);
    private bool CanExport=>pending.Count==0&&dragDocument==null&&(Doc.Points.Length>0||(Doc.Labels?.Length??0)>0)&&resolved.Problems.Length==0;
    private static string N(double value)=>value.ToString("0.####",CultureInfo.InvariantCulture);
    private string Label(string id)=>Doc.Points.FirstOrDefault(p=>p.Id==id)?.Label??id;
    private static string ToolName(string kind)=>kind switch{"select"=>"Chọn / kéo","point"=>"Điểm tự do","segment"=>"Đoạn thẳng","line"=>"Đường thẳng","ray"=>"Tia","vector"=>"Vector","circle"=>"Đường tròn","triangle"=>"Tam giác","polygon"=>"Đa giác / mặt","angle"=>"Đo góc","fixedAngle"=>"Góc cho trước","midpoint"=>"Trung điểm","foot"=>"Hình chiếu / đường cao","parallel"=>"Song song","perpendicular"=>"Vuông góc","onCircle"=>"Điểm trên đường tròn","box"=>"Hình hộp","pyramid"=>"Hình chóp","prism"=>"Lăng trụ","label"=>"Đặt chữ",_=>kind};
    private static string RelationName(string kind)=>kind switch{"coincident"=>"bám điểm","midpoint"=>"trung điểm","onLine"=>"trên đường","onCircle"=>"trên đường tròn","fixedAngle"=>"giữ góc","foot"=>"hình chiếu","parallel"=>"song song","perpendicular"=>"vuông góc",_=>kind};
    private string Instruction=>tool switch
    {
        "select"=>dimension==2?"Kéo điểm/cạnh tự do; kéo nền để di chuyển khung.":"Kéo điểm theo mặt phẳng đã chọn; kéo nền để xoay camera.",
        "point"=>"Bấm vị trí để đặt điểm. Giữ Alt để bỏ bắt điểm.","segment" or "line" or "ray" or "vector"=>pending.Count==0?"Chọn hoặc đặt điểm đầu.":"Chọn hoặc đặt điểm cuối.",
        "circle"=>pending.Count==0?"Chọn hoặc đặt tâm.":"Chọn hoặc đặt một điểm trên vành.",
        "angle"=>new[]{"Chọn A.","Chọn B — đỉnh góc.","Chọn C."}[Math.Min(pending.Count,2)],
        "fixedAngle"=>pending.Count==0?"Nhập góc rồi chọn đỉnh.":"Chọn điểm trên cạnh xuất phát.",
        "midpoint"=>pending.Count==0?"Chọn điểm thứ nhất.":"Chọn điểm thứ hai.",
        "foot" or "parallel" or "perpendicular"=>new[]{"Chọn điểm đi qua / cần chiếu.","Chọn đầu A của đường chuẩn.","Chọn đầu B của đường chuẩn."}[Math.Min(pending.Count,2)],
        "onCircle"=>"Bấm vào vành đường tròn; điểm mới bám đường tròn.","triangle"=>"Chọn hoặc đặt ba đỉnh.","polygon"=>"Chọn các đỉnh, bấm lại đỉnh đầu hoặc Enter để khép hình.",
        "label"=>"Nhập nội dung rồi bấm vị trí đặt chữ.",_=>"Bấm vị trí trên canvas để đặt khối."
    };
    protected override void OnInitialized(){dimension=Model.Dimension;Render();}
    private sealed record Startup(string? Plane,string? Space,bool AutoSave,string Message);
    private sealed record Saved(bool SavedOk,string Message);
    protected override async Task OnAfterRenderAsync(bool first)
    {
        if(!first)return;
        module=await Js.InvokeAsync<IJSObjectReference>("import","./_content/Locus.Editor/geometry.js");
        reference=DotNetObjectReference.Create(this);storage=await module.InvokeAsync<IJSObjectReference>("createStorage",Host);
        var startup=await storage.InvokeAsync<Startup>("read");autoSave=startup.AutoSave;draftStatus=startup.Message;
        foreach(var (json,session) in new[]{(startup.Plane,Model.Plane),(startup.Space,Model.Space)})
        {
            if(json==null||session.Version!=0)continue;
            if(DocumentCodec.Open(json).Document is GeometryDocument d&&d.Dimension==session.Document.Dimension)session.Load(d);
            else{retained=Encoding.UTF8.GetBytes(json);retainedName="nhap-hinh-hoc.locus";notice="Nháp chưa mở được. Đã giữ bản gốc để tải lại.";}
        }
        if(retained!=null)await storage.InvokeVoidAsync("fork");
        await module.InvokeVoidAsync("wire",root,reference);if(DesktopWindow!=null)DesktopWindow.PrepareClose=PrepareClose;
        Render();StateHasChanged();
    }
    private void Render(){resolved=GeometryEngine.Resolve(Doc);scene=GeometrySvg.Render(Doc,selected,true,snap,pending.LastOrDefault(),pointer);}
    private void Change(GeometryDocument next)
    {
        try{Session.Apply(next);notice="";Render();QueueSave();}catch(FormatException){notice="Dữ kiện không hợp lệ hoặc tạo quan hệ vòng. Hình trước thao tác được giữ.";}
    }
    private void Tool(string next){Cancel();tool=next;selected=null;Render();}
    private void Select(string id){Cancel();tool="select";selected=id;Render();}
    private void Cancel(){Session.EndGesture(true);pending.Clear();dragDocument=null;dragResolved=null;dragId=null;dragLabel=null;snap=null;pointer=null;Render();QueueSave();}
    private async Task SwitchDimension(int next){if(!await CanLeaveAsync())return;await Persist();dimension=next;Model.Dimension=next;selected=null;tool="select";Render();}
    private void History(bool forward){Cancel();if(Session.History(forward)){selected=null;Render();QueueSave();}}
    private void Delete(){if(selected==null)return;Cancel();Session.Remove(selected);selected=null;Render();QueueSave();}
    private void Complete(){Session.EndGesture();pending.Clear();snap=null;pointer=null;tool="select";Render();QueueSave();}
    private string TakePoint(string? id,Position world,bool alt)
    {
        if(id!=null&&Doc.Points.Any(p=>p.Id==id))return id;
        var target=!alt&&snapping?GeometryEngine.Snap(Doc,world,null,new GeometryProjection(Doc).WorldPerPixel*12):null;
        if(target?.Relation.Kind=="coincident")return target.Relation.Parents[0];
        return Session.AddPoint(target?.Position??world,target?.Relation);
    }
    [JSInvokable]public Task Pointer(string phase,double x,double y,string? id,string? label,bool alt,bool shift)
    {
        if(disposed)return Task.CompletedTask;
        try
        {
            var projection=new GeometryProjection(Doc);
            if(phase=="down")
            {
                downX=x;downY=y;
                if(tool=="select")
                {
                    Session.BeginGesture();dragDocument=Doc;dragResolved=resolved;dragId=id;dragLabel=label;selected=label??id;
                    var p=label??id;if(p!=null&&resolved.Points.TryGetValue(p,out var position)&&position is {} pos)dragDepth=plane=="XY"?pos.Z:plane=="XZ"?pos.Y:pos.X;else dragDepth=0;
                }
            }
            else if(phase=="cancel")Cancel();
            else if(phase=="move")
            {
                if(tool=="select"&&dragDocument!=null)MoveDrag(x,y,alt,shift);
                else if(tool!="select"&&projection.Unproject(x,y,plane) is {} world){pointer=world;snap=snapping&&!alt?GeometryEngine.Snap(Doc,world,null,projection.WorldPerPixel*12):null;}
            }
            else if(phase=="up")
            {
                if(tool=="select")
                {
                    if(Math.Abs(x-downX)+Math.Abs(y-downY)>2)MoveDrag(x,y,alt,shift);
                    Session.EndGesture();dragDocument=null;dragResolved=null;dragId=null;dragLabel=null;snap=null;QueueSave();
                }
                else if(Math.Abs(x-downX)+Math.Abs(y-downY)<8)
                {
                    if(projection.Unproject(x,y,plane) is {} world)Place(id??label,world,alt);
                    else notice="Mặt phẳng kéo đang nhìn cạnh. Đổi góc nhìn hoặc mặt phẳng.";
                }
            }
        }
        catch(FormatException ex){notice=ex.Message;}
        Render();StateHasChanged();return Task.CompletedTask;
    }
    private void MoveDrag(double x,double y,bool alt,bool shift)
    {
        var before=dragDocument!;var projection=new GeometryProjection(before);double dx=x-downX,dy=y-downY;
        if(Math.Abs(dx)+Math.Abs(dy)<1)return;
        if(dragLabel!=null)
        {
            var point=before.Points.First(p=>p.Id==dragLabel);Change(Doc with{Points=Doc.Points.Select(p=>p.Id==point.Id?p with{LabelX=point.LabelX+dx,LabelY=point.LabelY+dy}:p).ToArray()});return;
        }
        if(dragId==null)
        {
            if(dimension==3)
            {
                var cam=before.Camera??new();Change(Doc with{Camera=shift?cam with{PanX=cam.PanX+dx,PanY=cam.PanY+dy}:cam with{Azimuth=cam.Azimuth+dx*0.4,Elevation=Math.Clamp(cam.Elevation+dy*0.3,-89,89)}});
            }
            else
            {
                var view=before.Viewport??new(-7.8125,5,7.8125,-5);double ux=dx*projection.WorldPerPixel,uy=dy*projection.WorldPerPixel;Change(Doc with{Viewport=new(view.Left-ux,view.Top+uy,view.Right-ux,view.Bottom+uy)});
            }
            return;
        }
        if(before.Points.FirstOrDefault(p=>p.Id==dragId) is {} moving&&dragResolved!.Points[moving.Id] is {} original)
        {
            if(moving.Relation is {} fixedRelation&&fixedRelation.Kind!="onCircle"&&!fixedRelation.Attached){notice="Điểm đang có quan hệ dựng. Chọn Tách thành điểm tự do để bỏ quan hệ trước khi kéo.";return;}
            var start=projection.Unproject(downX,downY,plane,dragDepth);var current=projection.Unproject(x,y,plane,dragDepth);
            if(start==null||current==null){notice="Mặt phẳng kéo đang nhìn cạnh. Đổi góc nhìn hoặc mặt phẳng.";return;}
            var world=original+current.Value-start.Value;GeometryRelation? relation=null;
            if(moving.Relation?.Kind=="onCircle"&&!alt)
            {
                var r=moving.Relation;var center=resolved.Points[r.Parents[0]]!.Value;relation=r with{Value=Math.Atan2(world.Y-center.Y,world.X-center.X)};
            }
            else
            {
                snap=!alt&&snapping?GeometryEngine.Snap(Doc,world,moving.Id,projection.WorldPerPixel*(snap!=null?17:11)):null;
                if(snap!=null){world=snap.Position;relation=snap.Relation;}
                else if(moving.Relation!=null)notice="Thả để tách khỏi điểm đang bám.";
            }
            Session.Move(moving.Id,world,relation);return;
        }
        if(before.Segments.FirstOrDefault(s=>s.Id==dragId) is {} edge)
        {
            var a=before.Points.First(p=>p.Id==edge.Start);var b=before.Points.First(p=>p.Id==edge.End);
            if(a.Relation!=null||b.Relation!=null){notice="Cạnh có đầu mút ràng buộc. Kéo điểm cha hoặc tách quan hệ trước.";return;}
            double z=plane=="XY"?a.Z:plane=="XZ"?a.Y:a.X;
            var start=projection.Unproject(downX,downY,plane,z);var current=projection.Unproject(x,y,plane,z);if(start==null||current==null)return;var delta=current.Value-start.Value;
            Change(Doc with{Points=before.Points.Select(p=>p.Id==a.Id||p.Id==b.Id?p with{X=p.X+delta.X,Y=p.Y+delta.Y,Z=p.Z+delta.Z}:p).ToArray()});return;
        }
        if(before.Labels?.FirstOrDefault(l=>l.Id==dragId) is {} text)
        {
            var start=projection.Unproject(downX,downY,plane,text.Z);var current=projection.Unproject(x,y,plane,text.Z);if(start==null||current==null)return;var delta=current.Value-start.Value;
            Change(Doc with{Labels=before.Labels.Select(l=>l.Id==text.Id?l with{X=l.X+delta.X,Y=l.Y+delta.Y,Z=l.Z+delta.Z}:l).ToArray()});
        }
    }
    private void Place(string? id,Position world,bool alt)
    {
        if(tool is "box" or "pyramid" or "prism"){CreateSolid(world);return;}
        if(tool=="label")
        {
            if(string.IsNullOrWhiteSpace(labelText)){notice="Nhập chữ trước khi đặt.";return;}
            selected=GeometrySession.Id();Change(Doc with{Labels=[..Doc.Labels??[],new(selected,world.X,world.Y,labelText,world.Z)]});tool="select";return;
        }
        if(tool=="onCircle")
        {
            var circle=Doc.Circles?.FirstOrDefault(c=>c.Id==id);if(circle==null){notice="Bấm vào vành một đường tròn đã vẽ.";return;}
            if(resolved.Points[circle.Center] is not {} center)return;
            Session.BeginGesture();selected=Session.AddPoint(world,new("onCircle",[circle.Center,circle.Through],Math.Atan2(world.Y-center.Y,world.X-center.X)));Complete();return;
        }
        bool requiresExisting=tool is "angle" or "fixedAngle" or "midpoint" or "foot" or "parallel" or "perpendicular";
        if(requiresExisting&&(id==null||!Doc.Points.Any(p=>p.Id==id))){notice="Chọn một điểm đã có trên canvas.";return;}
        if(tool=="polygon"&&pending.Count>=3&&id==pending[0]){FinishPolygon();return;}
        if(id!=null&&pending.Contains(id)){notice="Chọn điểm khác.";return;}
        Session.BeginGesture();string point=TakePoint(id,world,alt);
        if(tool=="point"){selected=point;Session.EndGesture();QueueSave();return;}
        if(pending.Contains(point)){notice="Chọn điểm khác; đang bắt lại điểm đã chọn.";return;}
        pending.Add(point);
        int required=tool is "segment" or "line" or "ray" or "vector" or "circle" or "fixedAngle" or "midpoint"?2:3;
        if(tool=="polygon"||pending.Count<required)return;
        string a=pending[0],b=pending[1],c=pending.Count>2?pending[2]:"";selected=GeometrySession.Id();
        switch(tool)
        {
            case "segment":case "line":case "ray":case "vector":Change(Doc with{Segments=[..Doc.Segments,new(selected,a,b,Kind:tool)]});break;
            case "circle":Change(Doc with{Circles=[..Doc.Circles??[],new(selected,a,b)]});break;
            case "angle":Change(Doc with{Angles=[..Doc.Angles??[],new(selected,a,b,c)]});break;
            case "triangle":FinishPolygon();return;
            case "midpoint":selected=Session.AddPoint(world,new("midpoint",[a,b]));break;
            case "foot":case "parallel":case "perpendicular":
                string end=Session.AddPoint(world,new(tool,[a,b,c],Distance:3));Change(Doc with{Segments=[..Doc.Segments,new(selected,a,end,tool=="foot"?"dashed":"solid",Kind:tool=="foot"?"segment":"line")]});break;
            case "fixedAngle":
                if(!double.IsFinite(givenAngle)||Math.Abs(givenAngle)>360||!double.IsFinite(givenLength)||givenLength<=0){notice="Góc cần nằm trong −360…360°, độ dài dương.";Cancel();return;}
                string vertex=Session.AddPoint(world,new("fixedAngle",[a,b],givenAngle,givenLength));Change(Doc with{Segments=[..Doc.Segments,new(GeometrySession.Id(),a,vertex)],Angles=[..Doc.Angles??[],new(selected,b,a,vertex,Math.Abs(givenAngle)>180)]});break;
        }
        Complete();
    }
    private void FinishPolygon()
    {
        if(pending.Count<3)return;var ids=pending.ToArray();selected=GeometrySession.Id();
        Change(Doc with{Faces=[..Doc.Faces??[],new(selected,ids)],Segments=[..Doc.Segments,..ids.Select((id,i)=>new GeometrySegment(GeometrySession.Id(),id,ids[(i+1)%ids.Length]))]});Complete();
    }
    private void CreateSolid(Position anchor)
    {
        if(!new[]{solidWidth,solidDepth,solidHeight}.All(n=>double.IsFinite(n)&&n>0&&n<1000)){notice="Kích thước khối cần dương và nhỏ hơn 1000.";return;}
        Session.BeginGesture();double w=solidWidth/2,d=solidDepth/2,h=solidHeight;
        Position[] positions;int[][] faceIndices;
        if(tool=="pyramid") {positions=[new(-w,-d),new(w,-d),new(w,d),new(-w,d),new(0,0,h)];faceIndices=[[0,3,2,1],[0,1,4],[1,2,4],[2,3,4],[3,0,4]];}
        else if(tool=="prism"){positions=[new(-w,-d),new(w,-d),new(0,d),new(-w,-d,h),new(w,-d,h),new(0,d,h)];faceIndices=[[0,2,1],[3,4,5],[0,1,4,3],[1,2,5,4],[2,0,3,5]];}
        else {positions=[new(-w,-d),new(w,-d),new(w,d),new(-w,d),new(-w,-d,h),new(w,-d,h),new(w,d,h),new(-w,d,h)];faceIndices=[[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]];}
        var ids=positions.Select(p=>Session.AddPoint(anchor+p)).ToArray();var edges=new HashSet<(int,int)>();foreach(var face in faceIndices)for(int i=0;i<face.Length;i++){int a=face[i],b=face[(i+1)%face.Length];edges.Add((Math.Min(a,b),Math.Max(a,b)));}
        Change(Doc with{Faces=[..Doc.Faces??[],..faceIndices.Select(f=>new GeometryFace(GeometrySession.Id(),f.Select(i=>ids[i]).ToArray()))],Segments=[..Doc.Segments,..edges.Select(e=>new GeometrySegment(GeometrySession.Id(),ids[e.Item1],ids[e.Item2]))]});selected=ids[0];Complete();notice="Đã đặt khối. Các đỉnh kéo tự do; hình dạng khối có thể thay đổi.";
    }
    [JSInvokable]public Task Command(string key)
    {
        if(disposed)return Task.CompletedTask;if(key=="escape")Cancel();else if(key=="undo")History(false);else if(key=="redo")History(true);else if(key=="delete")Delete();else if(key=="enter"&&tool=="polygon")FinishPolygon();else if(key=="save")return Save();StateHasChanged();return Task.CompletedTask;
    }
    private void PointProperty(GeometryPoint p)=>Change(Doc with{Points=Doc.Points.Select(x=>x.Id==p.Id?p:x).ToArray()});
    private void EdgeProperty(GeometrySegment s)=>Change(Doc with{Segments=Doc.Segments.Select(x=>x.Id==s.Id?s:x).ToArray()});
    private void CircleProperty(GeometryCircle c)=>Change(Doc with{Circles=Doc.Circles?.Select(x=>x.Id==c.Id?c:x).ToArray()});
    private void AngleProperty(GeometryAngle a)=>Change(Doc with{Angles=Doc.Angles?.Select(x=>x.Id==a.Id?a:x).ToArray()});
    private void FaceProperty(GeometryFace f)=>Change(Doc with{Faces=Doc.Faces?.Select(x=>x.Id==f.Id?f:x).ToArray()});
    private void LabelProperty(GeometryLabel l)=>Change(Doc with{Labels=Doc.Labels?.Select(x=>x.Id==l.Id?l:x).ToArray()});
    private void EdgeNumber(GeometrySegment s,ChangeEventArgs e){if(double.TryParse(e.Value?.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var v))EdgeProperty(s with{Width=v});}
    private void AngleRadius(GeometryAngle a,ChangeEventArgs e){if(double.TryParse(e.Value?.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var v))AngleProperty(a with{Radius=v});}
    private void FaceOpacity(GeometryFace f,ChangeEventArgs e){if(double.TryParse(e.Value?.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var v))FaceProperty(f with{Opacity=v});}
    private double? Length(GeometrySegment s)=>resolved.Points[s.Start] is {} a&&resolved.Points[s.End] is {} b?(b-a).Length:null;
    private void Detach(GeometryPoint p){if(resolved.Points[p.Id] is not {} pos)return;PointProperty(p with{Relation=null,X=pos.X,Y=pos.Y,Z=pos.Z});}
    private void Zoom(double factor)
    {
        Cancel();if(dimension==3){var cam=Doc.Camera??new();Change(Doc with{Camera=cam with{Scale=Math.Clamp(cam.Scale*factor,1,2000)}});}
        else{var v=Doc.Viewport??new(-7.8125,5,7.8125,-5);double x=(v.Left+v.Right)/2,y=(v.Top+v.Bottom)/2,w=(v.Right-v.Left)/factor/2,h=(v.Top-v.Bottom)/factor/2;Change(Doc with{Viewport=new(x-w,y+h,x+w,y-h)});}
    }
    private void ResetView(){Cancel();Change(Doc with{Viewport=new(-7.8125,5,7.8125,-5),Camera=new()});}
    private void CameraPreset(string preset){Cancel();var c=Doc.Camera??new();Change(Doc with{Camera=preset switch{"front"=>c with{Azimuth=0,Elevation=0},"side"=>c with{Azimuth=90,Elevation=0},"top"=>c with{Azimuth=0,Elevation=90},_=>new()}});}
    private async Task Save(){if(pending.Count>0||dragDocument!=null){notice="Hoàn tất hoặc hủy nét đang vẽ trước khi lưu.";return;}var session=Session;long version=session.Version;var transfer=await Files.SaveAsync($"hinh-{dimension}d.locus","application/json",Encoding.UTF8.GetBytes(DocumentCodec.Serialize(Doc)),()=>!disposed&&session==Session&&session.Version==version);notice=Transfer(transfer);if(transfer.Status==TransferStatus.Completed&&storage!=null)await storage.InvokeVoidAsync("downloaded");}
    private static string Transfer(TransferResult result)=>result.Message.Length>0?result.Message:result.Status==TransferStatus.Completed?"Đã chuyển dữ liệu.":result.Status==TransferStatus.Cancelled?"Đã hủy; hình vẫn được giữ.":"Chưa chuyển được. Thử tải SVG/PNG.";
    private async Task Open(InputFileChangeEventArgs e)
    {
        if(!await CanLeaveAsync())return;
        var session=Session;long a=Model.Plane.Version,b=Model.Space.Version;
        bool Current()=>!disposed&&session==Session&&a==Model.Plane.Version&&b==Model.Space.Version&&pending.Count==0&&dragDocument==null;
        try
        {
            using var stream=e.File.OpenReadStream(DocumentCodec.MaxBytes);using var memory=new MemoryStream();await stream.CopyToAsync(memory);
            var bytes=memory.ToArray();var opened=DocumentCodec.Open(new UTF8Encoding(false,true).GetString(bytes));
            if(!Current()){notice="Hình đã thay đổi. Chọn lại tệp khi sẵn sàng.";return;}
            if(opened.Document is not GeometryDocument geometry){retained=bytes;retainedName=Path.GetFileName(e.File.Name);notice="Tệp chưa dùng được trong tab Hình học. Giữ nguyên phiên hiện tại.";return;}
            await Persist();if(!Current()){notice="Hình vừa thay đổi. Hãy chọn lại tệp.";return;}
            dimension=geometry.Dimension;Model.Dimension=dimension;Session.Load(geometry);selected=null;retained=null;tool="select";
            if(storage!=null)await storage.InvokeVoidAsync("fork");Render();QueueSave();notice="Đã mở hình học.";
        }
        catch(Exception ex)when(ex is IOException or FormatException or DecoderFallbackException){notice="Chưa mở được tệp; hình hiện tại vẫn được giữ.";}
    }
    private Task<TransferResult> Recover()=>Files.SaveAsync(retainedName,"application/octet-stream",retained!,()=>!disposed);
    private async Task Export(string format,bool copy=false)
    {
        if(!CanExport)return;var session=Session;long version=session.Version;string svg=GeometrySvg.Render(Doc);
        bool Current()=>!disposed&&Session==session&&session.Version==version&&CanExport;
        try{byte[] bytes;if(format=="png"){renderer??=await Js.InvokeAsync<IJSObjectReference>("import","./_content/Locus.Editor/renderer.js");bytes=await renderer.InvokeAsync<byte[]>("png",svg,2);}else bytes=Encoding.UTF8.GetBytes(svg);if(!Current())return;var transfer=copy?format=="png"?await Clipboard.WritePngAsync(bytes,Current):await Clipboard.WriteSvgAsync(svg,Current):await Files.SaveAsync($"hinh-{dimension}d."+format,format=="png"?"image/png":"image/svg+xml",bytes,Current);if(Current())notice=Transfer(transfer);contextMenu=false;}catch(JSException){notice="Chưa xuất được ảnh. Hãy thử Tải SVG.";}
    }
    private void QueueSave(){saveDelay?.Cancel();saveDelay?.Dispose();saveDelay=new();if(storage!=null)_=storage.InvokeVoidAsync("dirty");_=SaveAfterPause(saveDelay.Token);}
    private async Task SaveAfterPause(CancellationToken token){try{await Task.Delay(300,token);if(!token.IsCancellationRequested&&!disposed&&pending.Count==0&&dragDocument==null){await Persist();await InvokeAsync(StateHasChanged);}}catch(OperationCanceledException){}}
    private async Task<bool> Persist()
    {
        if(storage==null)return false;if(!autoSave){draftStatus="Tự lưu đang tắt. Lưu .locus trước khi đóng.";return false;}
        long a=Model.Plane.Version,b=Model.Space.Version;
        try{var result=await storage.InvokeAsync<Saved>("save",DocumentCodec.Serialize(Model.Plane.Document),DocumentCodec.Serialize(Model.Space.Document));if(a==Model.Plane.Version&&b==Model.Space.Version)draftStatus=result.Message;return result.SavedOk&&a==Model.Plane.Version&&b==Model.Space.Version;}
        catch(Exception ex)when(ex is JSException or FormatException){draftStatus="Chưa lưu được nháp. Hãy lưu .locus.";return false;}
    }
    private async Task AutoSaveChanged(ChangeEventArgs e){autoSave=e.Value is true;if(storage!=null)await storage.InvokeVoidAsync("configure",autoSave);await Persist();}
    private async Task<DesktopClosePreparation> PrepareClose(bool exit)
    {
        DesktopClosePreparation preparation=new(false);
        await InvokeAsync(async()=>
        {
            if(module==null||disposed)return;
            try
            {
                if(!await CanLeaveAsync())return;
                long a=Model.Plane.Version,b=Model.Space.Version;bool saved=await Persist();await module.InvokeVoidAsync("settle",root);
                if(a!=Model.Plane.Version||b!=Model.Space.Version||pending.Count>0||dragDocument!=null)return;
                preparation=new(saved||!exit||!autoSave,exit&&!autoSave);
            }
            catch(Exception ex)when(ex is JSException or InvalidOperationException){notice="Chưa lưu xong phiên. Cửa sổ vẫn được giữ.";}
            StateHasChanged();
        });
        return preparation;
    }
    public async Task<bool> CanLeaveAsync()
    {
        if(module==null||disposed)return false;await module.InvokeVoidAsync("settle",root);
        if(pending.Count>0||dragDocument!=null){notice="Hoàn tất hoặc nhấn Esc hủy nét đang vẽ trước khi đổi tab.";StateHasChanged();return false;}return true;
    }
    public async ValueTask DisposeAsync(){saveDelay?.Cancel();if(module!=null)await module.InvokeVoidAsync("settle",root);Session.EndGesture(true);await Persist();disposed=true;if(DesktopWindow?.PrepareClose==PrepareClose)DesktopWindow.PrepareClose=null;if(module!=null){await module.InvokeVoidAsync("unwire",root);await module.DisposeAsync();}reference?.Dispose();if(renderer!=null)await renderer.DisposeAsync();if(storage!=null)await storage.DisposeAsync();saveDelay?.Dispose();}
}
