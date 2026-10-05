using Locus.Core.Detection;
using Locus.Application;
using Locus.Editor.Formula.Presentation;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private const DetectionDomains AllStudioDomains = DetectionDomains.Math | DetectionDomains.Physics | DetectionDomains.Chemistry;
    private bool IsDetectMode(string mode)=>mode=="manual"?session.State.Settings.Mode==InputMode.Markers:session.State.Settings.Mode!=InputMode.Markers&&session.State.Settings.EnabledDomains==ModeDomains(mode);
    private static DetectionDomains ModeDomains(string mode)=>mode switch{"math"=>DetectionDomains.Math,"physics"=>DetectionDomains.Physics,"chemistry"=>DetectionDomains.Chemistry,_=>AllStudioDomains};
    private string ResultStatus => session.IsComposing ? "Đang ghép dấu" : session.IsBusy ? "Đang nhận diện" : session.State.Raw.Length == 0 ? "Chờ nội dung" : $"{session.State.Content?.Regions.Count(r => r.Display != null) ?? 0} công thức";
    private bool WholeArticleSelected=>selectionState.Kind==ResultSelectionKind.All;
    private int WordCount=>session.State.Raw.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Length;
    private ContentRegion? SelectedSingleRegion=>selectionState.Kind!=ResultSelectionKind.All&&resultSelection is { Whole.Count:1,Partial.Count:0 } selected?
        session.State.Content?.Regions.FirstOrDefault(r=>r.Id==selected.Whole[0]&&r.Start==selected.Start&&r.End==selected.End):null;
    private ContentRegion? SuggestionRegion=>resultSelection==null&&session.State.Content?.Regions.Count==1?session.State.Content.Regions[0]:SelectedSingleRegion;
    private IEnumerable<Guid>? StudioTargetIds=>resultSelection?.Whole;
    private bool StudioHasBalance=>session.State.Content?.Regions.Any(r=>(StudioTargetIds==null||StudioTargetIds.Contains(r.Id))&&FormulaSession.HasManagedCoefficientChange(r))==true;
    private bool CanStudioBalance=>session.CanRunStudioBalance&&resultSelection?.Partial.Count is not >0;
    private bool CanDropProducts=>CanStudioBalance&&SelectedSingleRegion is {} r&&r.ResultOverride?.ProductProposal!=null&&!FormulaSession.HasManagedCoefficientChange(r);
    private string StudioBalanceLabel=>session.IsBalancing?"Đang xử lý…":StudioHasBalance?"Hủy cân bằng":"Cân bằng phương trình hóa học";
    private const string StudioBalanceHint="Nhập a+b= rồi bấm Cân bằng để tự điền sản phẩm phản ứng và cân bằng phương trình.";
    private async Task ChooseSuggestion(string id){if(SuggestionRegion is {} r)session.FocusContentRegion(r.Id);await Choose(id);}
    private Task KeepSuggestionText(Guid id,bool keep)=>KeepRegionText(id,keep);
    private async Task SetDetectMode(string mode)
    {
        if(session.IsComposing)return;
        await Configure(session.State.Settings with {Mode=mode=="manual"?InputMode.Markers:InputMode.Explicit,EnabledDomains=mode=="manual"?AllStudioDomains:ModeDomains(mode)});
    }
    public async Task StudioHistory(bool redo)
    {
        if(!ready||disposed||module==null)return;
        await module.InvokeVoidAsync("settleInput",sourceElement);
        if(!session.IsComposing)await History(redo);
    }
}
