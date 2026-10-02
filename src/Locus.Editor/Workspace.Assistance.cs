using Locus.Application;
using Locus.Core;
using Locus.Core.Detection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private readonly Dictionary<string,string> chemistryConditions=new(StringComparer.Ordinal);
    private int chemistryRegionStart;
    private string ChemistryConditionValue(string key)=>chemistryConditions.GetValueOrDefault(key,"");
    private void ResetChemistryContext()=>chemistryConditions.Clear();
    private IEnumerable<Locus.Core.Diagnostic> ChemistryInputNotices=>(session.State.Analysis?.Diagnostics??[]).Concat(session.State.Candidate?.Diagnostics??[])
        .Where(d=>d.Code is "AMBIGUOUS_CHEMISTRY_CASE" or "CHEMISTRY_ZERO_IS_DIGIT" or "CHEMISTRY_CASE_BUDGET").GroupBy(d=>d.Code).Select(g=>g.First());
    private bool SingleSourceAssistance=>session.State.Raw.Length<=FormulaSession.MaxSourceLength&&(session.State.Content==null||session.State.Content.Regions.Count<=1)&&!session.State.Raw.Contains('\n');
    private bool ShowChemistryAssistance=>SingleSourceAssistance&&session.State.Raw.Length>0&&(Detects(DetectionDomains.Chemistry)||session.State.Candidate?.Document.Domain=="chemistry"||
        session.State.Settings.MarkerProfiles is {} p&&p.Chemistry.Enabled&&session.State.Raw.Contains(p.Chemistry.Open,StringComparison.Ordinal));
    private void AssistanceChanged(){if(!disposed)_=InvokeAsync(StateHasChanged);}
    private async Task RequestChemistryAssistance()
    {
        if(module==null||session.IsComposing)return;
        var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
        if(stamp.Composing||stamp.Pending||stamp.Raw!=session.State.Raw||!stamp.Focused)return;
        ghostDismissedVersion=-1;
        chemistryRegionStart=ChemistryRegionAt(stamp);
        await session.RequestAssistanceAsync(chemistryRegionStart,chemistryConditions.Select(p=>new AssistanceCondition(p.Key,p.Value)));
    }
    private int ChemistryRegionAt(EditorInputStamp stamp)
    {
        var settings=session.State.Settings;
        if(settings.MarkerProfiles is {} profiles)
        {
            var source=new SourceSnapshot(session.State.Raw,session.State.SourceRevision);
            var scan=ProfileMarkerScanner.Scan(source,profiles.ToProfiles(settings.Open,settings.Close),maxRegions:32);
            var atCaret=scan.Regions.Concat(scan.Drafts).FirstOrDefault(r=>r.ReplacementSpan.Start<=stamp.SelectionStart&&r.ReplacementSpan.End>=stamp.SelectionEnd);
            if(atCaret!=null)return atCaret.ReplacementSpan.Start;
        }
        return session.State.Region?.ReplacementSpan.Start??0;
    }
    private async Task ChemistryConditionChanged(string key,ChangeEventArgs args)
    {
        if(session.IsComposing||module==null)return;
        string value=args.Value?.ToString()??"";
        var choice=session.Assistance?.ConditionChoices?.SingleOrDefault(d=>d.Key==key);
        if(choice==null||value.Length>0&&!choice.Options.Any(o=>o.Value==value))return;
        preparedGhost=null;ghostDismissedVersion=-1;session.DismissAssistance();
        if(value.Length==0)chemistryConditions.Remove(key);else chemistryConditions[key]=value;
        var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
        if(stamp.Composing||stamp.Pending||stamp.Raw!=session.State.Raw||!stamp.Focused)return;
        await session.RequestAssistanceAsync(chemistryRegionStart,chemistryConditions.Select(p=>new AssistanceCondition(p.Key,p.Value)));
    }
    private async Task AcceptChemistryAssistance(string id)
    {
        if(module==null||session.IsComposing)return;
        try
        {
            var lease=session.LeaseAssistance(id);
            var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
            if(!session.TryPrepareContentAssistance(lease,stamp,out var plan)||!session.CommitAssistance(plan!)){notice="Nguồn hoặc lựa chọn đã đổi. Mở lại hỗ trợ cho nội dung hiện tại.";return;}
            debounce?.Cancel();await module.InvokeVoidAsync("setSourceAndSelection",sourceElement,session.State.Raw,plan!.Caret.Start,plan.Caret.End);
            notice="Đã dùng kết quả Hóa; nguồn gốc được giữ. Chọn phương trình để hủy riêng cân bằng.";await RenderCurrent();QueuePersist();
        }
        catch(Exception e)when(e is InvalidOperationException or FormatException or JSException){notice="Đề xuất không còn khớp phiên nhập. Nguồn hiện tại vẫn được giữ.";}
    }
    private async Task RestoreAssistanceSource()
    {
        if(module==null)return;var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
        if(stamp.Composing||stamp.Pending||stamp.Raw!=session.State.Raw||!stamp.Focused)return;
        if(session.RestoreBeforeAssistance(out var selection))
        {
            debounce?.Cancel();await module.InvokeVoidAsync("setSourceAndSelection",sourceElement,session.State.Raw,selection.Start,selection.End);
            notice="Đã khôi phục nguồn trước hỗ trợ.";await RenderCurrent();QueuePersist();
        }
    }
}
