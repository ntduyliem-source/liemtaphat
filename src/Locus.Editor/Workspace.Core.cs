using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
namespace Locus.Editor;
public partial class Workspace
{
    [Parameter] public string Host {get;set;}="browser";
    [Parameter] public IEditorWindow? DesktopWindow {get;set;}
    private FormulaSession session=>Model.Session;
    private FormulaView view {get=>Model.View;set=>Model.View=value;}
    private Guid documentId {get=>Model.DocumentId;set=>Model.DocumentId=value;}
    private long documentRevision {get=>Model.DocumentRevision;set=>Model.DocumentRevision=value;}
    private readonly string[] samples=["x mũ 2 + 1","1 trên 2","x+1/2","căn(x+1)"];
    private IJSObjectReference? module,renderer;
    private DotNetObjectReference<Workspace>? reference;
    private ElementReference sourceElement;
    private CancellationTokenSource? debounce;
    private bool ready,disposed,alternatives;
    private long renderVersion;
    private string renderedSvg="",notice="";
    private byte[]? retainedFile;
    private string retainedName="";
    protected override void OnInitialized(){session.Changed+=SessionChanged;session.AssistanceChanged+=AssistanceChanged;session.BalanceChanged+=BalanceChanged;if(DesktopWindow!=null){DesktopWindow.Changed+=DesktopWindowChanged;DesktopWindow.PrepareClose=PrepareDesktopClose;}}
    protected override async Task OnAfterRenderAsync(bool first)
    {
        if(!first){await SyncGhost();return;}
        module=await Js.InvokeAsync<IJSObjectReference>("import","./_content/Locus.Editor/editor.js");
        renderer=await Js.InvokeAsync<IJSObjectReference>("import","./_content/Locus.Editor/renderer.js");
        await InitializeLocal();
        reference=DotNetObjectReference.Create(this);await module.InvokeVoidAsync("wire",sourceElement,reference);
        await module.InvokeVoidAsync("wireResult",resultElement,reference);
        ready=true;StateHasChanged();if(!session.State.HasResult)await AnalyzeAndRender();else await RenderCurrent();
    }
    private void SessionChanged(){ResetChemistryContext();ResetContentSelection();showDropProducts=false;renderVersion++;renderedSvg="";alternatives=false;contextMenu=false;fallbackFormat=null;if(!disposed){QueuePersist();_=InvokeAsync(StateHasChanged);}}
    [JSInvokable] public Task SourceInput(string value,bool composing)
    {
        if(disposed)return Task.CompletedTask;
        session.UpdateSource(value,composing);notice="";debounce?.Cancel();debounce?.Dispose();debounce=new();
        if(!composing)_=AfterPause(debounce.Token);StateHasChanged();return Task.CompletedTask;
    }
    private async Task AfterPause(CancellationToken token){try{await Task.Delay(180,token);if(!disposed&&!token.IsCancellationRequested){await AnalyzeAndRender();await TryAutomaticChemistry(token);}}catch(OperationCanceledException){}}
    private async Task AnalyzeAndRender(){if(disposed)return;if(await session.AnalyzeContentAsync())await session.ApplyAutoBalanceAsync();await RenderCurrent();}
    private async Task RenderCurrent()
    {
        session.PromoteSnapshot();ResetContentSelection();
        await RefreshWand();
        renderedSvg="";long ticket=++renderVersion;if(!session.CanExport||renderer==null){if(!disposed)StateHasChanged();return;}
        var lease=session.Lease();var options=view;
        try{var scene=await renderer.InvokeAsync<RenderedFormula>("render",CandidateExporter.ToMathMl(session.Require(lease)),lease.CandidateId,options);if(!disposed&&ticket==renderVersion&&session.IsCurrent(lease)&&options==view)renderedSvg=scene.Svg;}
        catch(JSException){if(ticket==renderVersion)notice="Chưa dựng được ảnh. Nguồn và công thức vẫn được giữ.";}
        if(!disposed){QueuePersist();StateHasChanged();}
    }
    private sealed record RenderedFormula(string Svg,double Width,double Height,string CandidateId);
    private async Task Sample(string raw){debounce?.Cancel();session.UpdateSource(raw);await module!.InvokeVoidAsync("setSource",sourceElement,raw);await AnalyzeAndRender();}
    private async Task ChangeMode(ChangeEventArgs e){await Configure(session.State.Settings with {Mode=Enum.Parse<InputMode>(e.Value!.ToString()!)});}
    private async Task Configure(FormulaSettings settings){debounce?.Cancel();session.Configure(settings);if(!session.State.HasResult)await AnalyzeAndRender();else await RenderCurrent();}
    private bool Detects(DetectionDomains domain)=>(session.State.Settings.EnabledDomains&domain)!=0;
    private async Task ChangeDomains(DetectionDomains domain,ChangeEventArgs e)
    {
        if(session.IsComposing)return;
        var flags=session.State.Settings.EnabledDomains;
        await Configure(session.State.Settings with{EnabledDomains=e.Value is true?flags|domain:flags&~domain});
        notice=session.CanExport?"Đã đổi nhận diện cho lần nhập hoặc dựng lại tiếp theo. Công thức hiện tại được giữ.":session.State.Settings.EnabledDomains==DetectionDomains.None?"Đã tắt tự nhận diện. Cặp riêng vẫn chỉ định môn cho vùng được bọc.":"";
    }
    private async Task Choose(string id){session.Select(session.State.RegionIndex,id);await RenderCurrent();alternatives=true;}
    [JSInvokable] public async Task History(bool redo)
    {debounce?.Cancel();if(redo?session.Redo():session.Undo()){if(session.HistorySelection is {} selection)await module!.InvokeVoidAsync("setSourceAndSelection",sourceElement,session.State.Raw,selection.Start,selection.End);else await module!.InvokeVoidAsync("setSource",sourceElement,session.State.Raw);if(!session.State.HasResult&&!session.IsComposing)await AnalyzeAndRender();else await RenderCurrent();}}
    private async Task FontChanged(ChangeEventArgs e){view=view with{FontSize=double.Parse(e.Value!.ToString()!,System.Globalization.CultureInfo.InvariantCulture)};await RenderCurrent();QueuePersist();}
    private async Task ScaleChanged(ChangeEventArgs e){view=view with{PixelScale=double.Parse(e.Value!.ToString()!,System.Globalization.CultureInfo.InvariantCulture)};await RenderCurrent();QueuePersist();}
    private async Task WhiteChanged(ChangeEventArgs e){view=view with{WhiteBackground=e.Value is true};await RenderCurrent();QueuePersist();}
    private Task SaveDocument()=>SaveDocumentAs(false);
    private async Task SaveDocumentAs(bool pick)
    {
        try{if(session.IsBusy||session.IsComposing)return;var version=session.Version;var options=view;var document=new FormulaDocument(documentId,checked(documentRevision+1),session.State,view);var json=DocumentCodec.Serialize(document);var bytes=System.Text.Encoding.UTF8.GetBytes(json);var result=pick?await Files.SaveAsAsync("cong-thuc.locus","application/json",bytes,()=>!disposed&&version==session.Version&&options==view):await Download("cong-thuc.locus","application/json",bytes);if(result.Status==TransferStatus.Completed){documentRevision=document.Revision;await local!.InvokeVoidAsync("markDownloaded");notice="Đã tạo tệp .locus.";QueuePersist();}else notice=TransferNotice(result);}
        catch(Exception ex) when(ex is FormatException or OverflowException or JSException){notice="Chưa lưu được. Nguồn hiện tại vẫn được giữ.";}
    }
    private async Task OpenFile(InputFileChangeEventArgs e)
    {
        if(session.IsComposing||e.FileCount==0)return;var version=session.Version;
        try{
            using var stream=e.File.OpenReadStream(DocumentCodec.MaxBytes);using var memory=new System.IO.MemoryStream();await stream.CopyToAsync(memory);var bytes=memory.ToArray();
            var json=new System.Text.UTF8Encoding(false,true).GetString(bytes);var opened=DocumentCodec.Open(json);
            if(version!=session.Version||session.IsComposing){notice="Nguồn đã đổi trong lúc mở tệp. Chọn lại tệp khi bạn sẵn sàng.";return;}
            if(opened.Document is not FormulaDocument formula){retainedFile=bytes;retainedName=e.File.Name;notice=opened.IsSupported?"Tệp đồ thị hoặc hình học sẽ dùng ở editor tương ứng. Phiên công thức vẫn giữ nguyên.":opened.UnavailableReason!;return;}
            debounce?.Cancel();session.Load(formula.State);notice="Đã mở nguồn và lựa chọn đã lưu.";UpgradeMarkers();view=formula.View;documentId=formula.Id;documentRevision=formula.Revision;retainedFile=null;
            ClearResultSelection();await module!.InvokeVoidAsync("setSource",sourceElement,session.State.Raw);if(!session.State.HasResult)await AnalyzeAndRender();else await RenderCurrent();QueuePersist();
        }catch(Exception ex) when(ex is System.IO.IOException or FormatException or System.Text.DecoderFallbackException){notice="Chưa mở được tệp; giữ nguyên phiên hiện tại.";}
    }
    private async Task RecoverOriginal(){if(retainedFile!=null)await Download(System.IO.Path.GetFileName(retainedName),"application/octet-stream",retainedFile);}
    private async Task Export(string format,bool copy=false)
    {
        if(!session.CanExport||format is "png" or "svg"&&!CanExportSingle)return;var lease=session.Lease();long ticket=renderVersion,selectionTicket=selectionEpoch;var svg=renderedSvg;var options=view;
        try{
            var candidate=session.Require(lease);byte[] bytes;string type,name;
            if(format=="png"){if(svg.Length==0)return;bytes=await renderer!.InvokeAsync<byte[]>("png",svg,options.PixelScale);type="image/png";name="cong-thuc.png";}
            else if(format=="svg"){if(svg.Length==0)return;bytes=System.Text.Encoding.UTF8.GetBytes(svg);type="image/svg+xml";name="cong-thuc.svg";}
            else {bytes=System.Text.Encoding.UTF8.GetBytes(format switch{"source"=>candidate.Source.Raw,"mathml"=>CandidateExporter.ToMathMl(candidate),"omml"=>CandidateExporter.ToOmml(candidate),_=>CandidateExporter.ToLatex(candidate)});type="text/plain";name="cong-thuc-"+format+".txt";}
            if(!session.IsCurrent(lease)||ticket!=renderVersion||options!=view||selectionTicket!=selectionEpoch){notice="Nguồn hoặc vùng chọn đã đổi; xuất lại từ công thức hiện tại.";return;}
            bool Current()=>!disposed&&session.IsCurrent(lease)&&ticket==renderVersion&&options==view&&selectionTicket==selectionEpoch;
            var result=copy?format=="png"?await Clipboard.WritePngAsync(bytes,Current):format=="svg"?await Clipboard.WriteSvgAsync(svg,Current):await Clipboard.WriteTextAsync(System.Text.Encoding.UTF8.GetString(bytes),Current):await Files.SaveAsync(name,type,bytes,Current);
            if(!Current())return;
            fallbackFormat=copy&&result.Status!=TransferStatus.Completed?format:null;contextMenu=false;
            notice=result.Message.Length>0?result.Message:result.Status==TransferStatus.Completed?copy?"Đã sao chép.":"Đã tạo tệp xuất.":TransferNotice(result);
        }catch(Exception ex) when(ex is JSException or InvalidOperationException){notice="Chưa xuất được. Nội dung của bạn vẫn được giữ.";}
    }
    private async Task Snapshot(){if(session.CanExport){var result=ActiveContentRegion?.ResultOverride?.Result??session.State.Region!;await Download("locus-snapshot.json","application/json",System.Text.Encoding.UTF8.GetBytes(CandidateSetSerializer.Serialize(result.Select(session.State.CandidateId!))));}}
    private Task<TransferResult> Download(string name,string type,byte[] bytes){long version=session.Version;return Files.SaveAsync(name,type,bytes,()=>!disposed&&version==session.Version&&!session.IsComposing);}
    private static string TransferNotice(TransferResult result)=>result.Status==TransferStatus.Cancelled?"Đã hủy. Phiên hiện tại vẫn được giữ.":"Chưa chuyển được dữ liệu. Thử tải tệp hoặc sao chép ở phần văn bản.";
    private static string Kind(Candidate c)=>c.Kind=="direct"?"Theo cú pháp đã gõ":c.Kind=="repair"?"Đề nghị sửa":"Cách hiểu khác";
    public async ValueTask DisposeAsync(){if(DesktopWindow!=null){DesktopWindow.Changed-=DesktopWindowChanged;if(DesktopWindow.PrepareClose==PrepareDesktopClose)DesktopWindow.PrepareClose=null;}debounce?.Cancel();persistDelay?.Cancel();lifetime.Cancel();await Persist();disposed=true;debounce?.Dispose();session.Changed-=SessionChanged;session.AssistanceChanged-=AssistanceChanged;session.BalanceChanged-=BalanceChanged;session.DetachView();if(module!=null)try{await module.InvokeVoidAsync("unwire",sourceElement);await module.DisposeAsync();}catch(JSException){}reference?.Dispose();if(renderer!=null)try{await renderer.DisposeAsync();}catch(JSException){}if(local!=null)try{await local.DisposeAsync();}catch(JSException){}}
}
