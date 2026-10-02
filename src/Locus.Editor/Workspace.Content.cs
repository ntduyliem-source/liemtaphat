using Locus.Application;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Text;

namespace Locus.Editor;

public partial class Workspace
{
    private const string ParagraphSample = "Ôn tập hôm nay 🧪\nCho lc[x mũ 2 + 1]. Vận tốc ly-[v=10 m/s].\nXét hoa-[H2+O2=H2O].\nTài liệu: https://example.org/bai-1\nGiữ nguyên câu này.";
    private ElementReference resultElement;
    private ContentSelection? resultSelection;
    private long selectionRevision = -1;
    private long selectionEpoch;
    private ContentRegion? ActiveContentRegion => session.State.Content?.Regions.ElementAtOrDefault(session.State.RegionIndex);
    private bool SelectedRegion(Guid id) => resultSelection?.Whole.Contains(id) == true;
    private string SelectionLabel => resultSelection is {} s ? $"Đã chọn {s.Whole.Count} công thức" + (s.Partial.Count>0?$" · {s.Partial.Count} công thức chọn dở":"") : "Chọn công thức hoặc bôi chọn một đoạn trong kết quả.";
    private string ExportScopeLabel => resultSelection is {} s ? CanExportSingle ? "Xuất: một công thức đang chọn" : $"Xuất: vùng chọn · {s.Whole.Count} công thức" : "Xuất: toàn bộ đoạn kết quả";
    private bool CanExportSingle => resultSelection is { Whole.Count:1, Partial.Count:0 } s && ActiveContentRegion is {} r && s.Whole[0] == r.Id && s.Start == r.Start && s.End == r.End && r.Display != null;
    private bool CanExportImage => CanExportSingle && session.CanExport && renderedSvg.Length>0;
    private bool HasClippedFormula => resultSelection is {} s && session.State.Content?.Regions.Any(r=>s.Partial.Contains(r.Id)&&r.Display!=null)==true;
    private bool CanCopyResult => !session.IsBusy&&!session.IsComposing&&session.State.Raw.Length>0&&!HasClippedFormula;
    private void ResetContentSelection()
    {
        if (selectionRevision == session.State.SourceRevision) return;
        resultSelection = null; selectionRevision = session.State.SourceRevision; selectionEpoch++;session.SetBalanceSelection(null);
    }
    private async Task SelectResultRegion(Guid id)
    {
        if (session.IsBusy || session.IsComposing) return;
        var region = session.State.Content?.Regions.FirstOrDefault(r=>r.Id==id);
        if (region == null || !session.FocusContentRegion(id)) return;
        resultSelection = session.State.Content!.Select(region.Start,region.End);selectionRevision=session.State.SourceRevision;selectionEpoch++;
        await RenderCurrent();
    }
    private async Task OpenRegionDetails(Guid id){await SelectResultRegion(id);alternatives=true;}
    private async Task KeepRegionText(Guid id,bool keep)
    {
        if(session.KeepContentText(id,keep)){await RenderCurrent();alternatives=true;}
    }
    private Task EditRegionSource(ContentRegion region)=>module!.InvokeVoidAsync("setSourceAndSelection",sourceElement,session.State.Raw,region.Start,region.End).AsTask();
    private async Task SelectAllResult()
    {
        if(session.State.Content is not {} content || session.IsBusy || session.IsComposing)return;
        await SetResultSelection(content.Select(0,content.Raw.Length));
    }
    private void ClearResultSelection(){resultSelection=null;selectionEpoch++;contextMenu=false;session.SetBalanceSelection(null);_=session.RefreshBalanceAsync();}
    private async Task SelectWholeFormulas()
    {
        if(resultSelection is not {} selection||session.State.Content is not {} content||session.IsBusy||session.IsComposing)return;
        var partial=content.Regions.Where(r=>selection.Partial.Contains(r.Id)).ToArray();
        if(partial.Length==0)return;
        await SetResultSelection(content.Select(Math.Min(selection.Start,partial.Min(r=>r.Start)),Math.Max(selection.End,partial.Max(r=>r.End))));
    }
    private void CancelAnalysis(){debounce?.Cancel();session.CancelContentAnalysis();}
    [JSInvokable] public async Task ResultSelectionChanged(string version,int start,int end,string[] partial)
    {
        if(version!=session.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)||session.IsBusy||session.IsComposing||session.State.Content is not {} content)return;
        try
        {
            var selected=content.Select(start,end);
            var clipped=partial.Select(Guid.Parse).Where(id=>selected.Whole.Contains(id)||selected.Partial.Contains(id)).Distinct().ToArray();
            selected=selected with { Whole=selected.Whole.Where(id=>!clipped.Contains(id)).ToArray(), Partial=selected.Partial.Concat(clipped).Distinct().ToArray() };
            await SetResultSelection(selected);
        }
        catch(ArgumentException){}catch(FormatException){}
    }
    private async Task SetResultSelection(ContentSelection selected)
    {
        if(selected.Start==selected.End){ClearResultSelection();StateHasChanged();return;}
        if(selected.Whole.Count==1)session.FocusContentRegion(selected.Whole[0]);
        resultSelection=selected;selectionRevision=session.State.SourceRevision;selectionEpoch++;contextMenu=false;
        await RenderCurrent();StateHasChanged();
    }
    private async Task CopyResultText()
    {
        if(!CanCopyResult)return;
        var version=session.Version;var epoch=selectionEpoch;var content=session.State.Content;
        int start=resultSelection?.Start??0,end=resultSelection?.End??session.State.Raw.Length;
        // Partial raw text has exact source offsets. A clipped rendered formula needs an explicit whole selection.
        string text=content?.ResultText(start,end)??session.State.Raw[start..end];
        var transfer=await Clipboard.WriteTextAsync(text,()=>!disposed&&version==session.Version&&epoch==selectionEpoch&&!session.IsComposing);
        notice=transfer.Status==TransferStatus.Completed?"Đã copy đoạn; công thức ở dạng LaTeX, phần văn bản giữ nguyên.":TransferNotice(transfer);
    }
    private async Task CopyWholeSource()
    {
        var version=session.Version;
        var transfer=await Clipboard.WriteTextAsync(session.State.Raw,()=>!disposed&&version==session.Version&&!session.IsComposing);
        notice=transfer.Status==TransferStatus.Completed?"Đã copy nguyên nguồn gốc.":TransferNotice(transfer);
    }
    private async Task DownloadSource(){var result=await Download("nguon-locus.txt","text/plain;charset=utf-8",Encoding.UTF8.GetBytes(session.State.Raw));notice=result.Status==TransferStatus.Completed?"Đã tải nguyên nguồn.":TransferNotice(result);}
}
