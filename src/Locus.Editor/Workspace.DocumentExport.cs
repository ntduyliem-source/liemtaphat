using System.Text;
using System.Xml;
using Locus.Application;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private bool documentExporting;
    private async Task ExportDocument(string format)
    {
        if(!CanCopyResult||documentExporting)return;
        var state=session.State;long version=session.Version,selected=selectionEpoch;
        bool Current()=>!disposed&&version==session.Version&&selected==selectionEpoch&&!session.IsComposing&&!session.IsBusy;
        try
        {
            var projection=ContentExport.Capture(state,resultSelection);documentExporting=true;
            // Yield before packaging so newer input/selection has an opportunity to invalidate this snapshot.
            await Task.Yield();if(!Current())return;
            byte[] bytes=format=="docx"?projection.ToDocx(lifetime.Token):Encoding.UTF8.GetBytes(projection.ToHtml(lifetime.Token));
            if(!Current())return;
            var result=await Files.SaveAsync("noi-dung-locus."+format,format=="docx"?ContentExport.DocxMediaType:"text/html;charset=utf-8",bytes,Current,lifetime.Token);
            if(Current())notice=result.Status==TransferStatus.Completed?format=="docx"?"Đã xuất DOCX theo phạm vi đã chọn; công thức chỉnh sửa được trong Word.":"Đã xuất HTML theo phạm vi đã chọn; mở bằng trình duyệt để xem công thức.":TransferNotice(result);
        }
        catch(OperationCanceledException){}
        catch(Exception e)when(e is InvalidOperationException or ArgumentException or FormatException or XmlException or JSException or IOException){notice="Chưa xuất được tệp. Nguồn và kết quả hiện tại vẫn được giữ.";}
        finally{documentExporting=false;if(!disposed)StateHasChanged();}
    }
}
