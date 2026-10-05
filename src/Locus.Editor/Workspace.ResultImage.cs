using System.Text;
using Locus.Application;
using Locus.Core.Export;
using Locus.Editor.Formula.Presentation;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private bool imageExporting;
    private async Task ExportResultImage(FormulaImageRequest request)
    {
        if(module==null||renderer==null||imageExporting)return;
        await module.InvokeVoidAsync("settleInput",sourceElement);
        if(!CanExportImage)return;
        var version=session.Version;var selection=selectionEpoch;var options=view;
        bool Current()=>!disposed&&version==session.Version&&selection==selectionEpoch&&options==view&&!session.IsComposing&&!session.IsBusy;
        imageExporting=true;notice="Đang dựng ảnh…";
        try
        {
            var projection=ContentExport.Capture(session.State,resultSelection);
            var parts=projection.Parts.Select(p=>new {text=p.Text,mathMl=p.Formula is {} c?CandidateExporter.ToMathMl(c):null,candidateId=p.Formula?.Id}).ToArray();
            await using var composer=await Js.InvokeAsync<IJSObjectReference>("import","./_content/Locus.Editor/formula/result-image.js");
            var scene=await composer.InvokeAsync<ResultImage>("compose",parts,options,resultElement);
            if(!Current()){notice="Nguồn hoặc vùng chọn đã đổi; bấm xuất lại để lấy kết quả hiện tại.";return;}
            byte[] bytes=request.Format=="svg"?Encoding.UTF8.GetBytes(scene.Svg):await renderer.InvokeAsync<byte[]>("png",scene.Svg,request.Scale);
            if(!Current()){notice="Nguồn hoặc vùng chọn đã đổi; bấm xuất lại để lấy kết quả hiện tại.";return;}
            var transfer=request.Copy?await Clipboard.WritePngAsync(bytes,Current):await Files.SaveAsync("locus-ket-qua."+request.Format,request.Format=="svg"?"image/svg+xml":"image/png",bytes,Current);
            if(!Current())return;
            notice=transfer.Status==TransferStatus.Completed?(request.Copy?"Đã copy ảnh.":"Đã tải ảnh."):
                request.Copy?"Chưa copy được ảnh. Có thể dùng nút PNG 2X hoặc PNG 4X để tải ảnh.":TransferNotice(transfer);
        }
        catch(Exception ex) when(ex is JSException or InvalidOperationException or ArgumentException)
        {
            notice=ex.Message.Contains("PNG_LIMIT")?"Ảnh PNG vượt giới hạn 24 triệu pixel hoặc 32.767 px mỗi cạnh. Chọn SVG hoặc một đoạn ngắn hơn.":
                "Chưa dựng được ảnh. Nội dung vẫn được giữ; thử lại khi ảnh và font đã tải xong.";
        }
        finally{imageExporting=false;if(!disposed)StateHasChanged();}
    }
    private sealed record ResultImage(string Svg,double Width,double Height);
}
