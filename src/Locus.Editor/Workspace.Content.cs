using Locus.Application;
using Locus.Editor.Formula.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private Formula.Components.FormulaResult resultPanel = default!;
    private ElementReference resultElement => resultPanel.Element;
    private ResultSelectionState selectionState = ResultSelectionState.None;
    private ContentSelection? resultSelection => selectionState.Range;
    private long selectionRevision = -1;
    private long selectionEpoch;
    private ContentRegion? ActiveContentRegion => session.State.Content?.Regions.ElementAtOrDefault(session.State.RegionIndex);
    private bool SelectedRegion(Guid id) => resultSelection?.Whole.Contains(id) == true;
    private string ExportScopeLabel => resultSelection is {} s ? CanExportSingle ? "Xuất: một công thức đang chọn" : $"Xuất: vùng chọn · {s.Whole.Count} công thức" : "Xuất: toàn bộ đoạn kết quả";
    private bool CanExportSingle => resultSelection is { Whole.Count:1, Partial.Count:0 } s && ActiveContentRegion is {} r && s.Whole[0] == r.Id && s.Start == r.Start && s.End == r.End && r.Display != null;
    private bool CanExportImage => ready && !imageExporting && !session.IsBalancing && session.State.HasResult && CanCopyResult;
    private bool HasClippedFormula => resultSelection is {} s && session.State.Content?.Regions.Any(r=>s.Partial.Contains(r.Id)&&r.Display!=null)==true;
    private bool CanCopyResult => !session.IsBusy&&!session.IsComposing&&session.State.Raw.Length>0&&!HasClippedFormula;
    private void ResetContentSelection()
    {
        if (selectionRevision == session.State.SourceRevision) return;
        selectionState = ResultSelectionState.None; selectionRevision = session.State.SourceRevision; selectionEpoch++;session.SetBalanceSelection(null);
    }
    private async Task SelectResultRegion(Guid id)
    {
        if (session.IsBusy || session.IsComposing) return;
        var region = session.State.Content?.Regions.FirstOrDefault(r=>r.Id==id);
        if (region == null || !session.FocusContentRegion(id)) return;
        selectionState=new(ResultSelectionKind.Region,session.State.Content!.Select(region.Start,region.End));selectionRevision=session.State.SourceRevision;selectionEpoch++;
        await RenderCurrent();
    }
    private async Task KeepRegionText(Guid id,bool keep)
    {
        if(session.KeepContentText(id,keep))await RenderCurrent();
    }
    private async Task SelectAllResult()
    {
        if(session.State.Content is not {} content || session.IsBusy || session.IsComposing)return;
        if(selectionState.Kind==ResultSelectionKind.All){ClearResultSelection();StateHasChanged();return;}
        await SetResultSelection(content.Select(0,content.Raw.Length),ResultSelectionKind.All);
    }
    private void ClearResultSelection(){selectionState=ResultSelectionState.None;selectionEpoch++;session.SetBalanceSelection(null);}
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
    [JSInvokable] public async Task ResultSelectAll(string version)
    {
        if(version!=session.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)||session.IsBusy||session.IsComposing||session.State.Content is not {} content)return;
        await SetResultSelection(content.Select(0,content.Raw.Length),ResultSelectionKind.All);
    }
    private async Task SetResultSelection(ContentSelection selected,ResultSelectionKind kind=ResultSelectionKind.Range)
    {
        if(selected.Start==selected.End){ClearResultSelection();StateHasChanged();return;}
        if(selected.Whole.Count==1)session.FocusContentRegion(selected.Whole[0]);
        selectionState=new(kind,selected);selectionRevision=session.State.SourceRevision;selectionEpoch++;
        await RenderCurrent();StateHasChanged();
    }
}
