using Locus.Application;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Export;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private long ghostDismissedVersion=-1;
    private PreparedAssistance? preparedGhost;
    private sealed record GhostPresentation(string Id,string Raw,long Version,long Epoch,int Start,int End,int ClosedEnd,string Products,string MathMl,string LeftChange,bool Space);
    public sealed record GhostPlan(string Token,string Raw,int Start,int End);
    private async Task TryAutomaticChemistry(CancellationToken token)
    {
        if(disposed||!SingleSourceAssistance||session.State.Content?.Regions.Any(r=>r.ResultOverride?.ProductProposal!=null)==true||token.IsCancellationRequested||session.IsBusy||session.IsComposing||module==null||ghostDismissedVersion==session.Version)return;
        var state=session.State;long version=session.Version;
        var stamp=await module.InvokeAsync<EditorInputStamp>("ghostInputStamp",sourceElement);
        if(disposed||token.IsCancellationRequested||version!=session.Version||!stamp.Focused||stamp.Composing||stamp.Pending||stamp.Raw!=state.Raw||stamp.SelectionStart!=stamp.SelectionEnd)return;
        var source=new SourceSnapshot(state.Raw,state.SourceRevision);AssistanceRegion? region=null;int caret=stamp.SelectionStart;
        if(state.Settings.MarkerProfiles is {} profiles)
        {
            var scan=ProfileMarkerScanner.Scan(source,profiles.ToProfiles(state.Settings.Open,state.Settings.Close),maxRegions:32,cancellationToken:token);
            var found=scan.Regions.Concat(scan.Drafts).FirstOrDefault(r=>r.ReplacementSpan.Start<=caret&&r.ReplacementSpan.End>=caret);
            if(found!=null&&(found.Profile.Resolve(state.Settings.EnabledDomains)&DetectionDomains.Chemistry)!=0&&found.Profile.Domain is "chemistry" or "auto"&&
                (state.Settings.Mode!=InputMode.Explicit||found.ReplacementSpan.Start==0&&found.ReplacementSpan.End==source.Raw.Length))
                region=new(source,found.ContentSpan,found.ReplacementSpan,found.Profile.Markers,new(found.Profile.Id,found.Profile.Domain),found.IsClosed);
            else if(scan.ReservedSpans.Count>0)return;
        }
        if(region==null&&state.Settings.Mode==InputMode.Explicit&&source.Raw.Length>0&&(state.Settings.EnabledDomains&DetectionDomains.Chemistry)!=0)region=new(source,new(0,source.Raw.Length),new(0,source.Raw.Length));
        if(region==null)return;var draft=ReactionDraftParser.Parse(region,token).Draft;
        if(draft==null||!GhostCaret(draft,stamp))return;
        chemistryRegionStart=region.ReplacementSpan.Start;
        await session.RequestAssistanceAsync(chemistryRegionStart,chemistryConditions.Select(p=>new AssistanceCondition(p.Key,p.Value)));
    }
    private static bool GhostCaret(ReactionDraft draft,EditorInputStamp stamp)=>stamp.Focused&&!stamp.Composing&&!stamp.Pending&&stamp.SelectionStart==stamp.SelectionEnd&&
        (stamp.SelectionStart>=draft.SeparatorSpan.End&&stamp.SelectionStart<=draft.Region.ContentSpan.End||draft.Region.IsClosed&&stamp.SelectionStart==draft.Region.ReplacementSpan.End);
    private async Task SyncGhost()
    {
        if(module==null||disposed)return;GhostPresentation? ghost=null;
        if(ghostDismissedVersion!=session.Version&&!session.IsBusy&&!session.IsAssisting&&!session.IsComposing&&session.Assistance?.Draft is {} snapshot&&session.AssistanceProposals is {Count:1})
        {
            var proposal=session.AssistanceProposals[0];
            if(proposal.Kind=="complete-reaction")
            {
                var draft=AssistanceSerializer.DeserializeDraft(snapshot);
                var products=ChemistryProjection.Create(proposal.Result.Candidates[0].Document.Root.Children[1]).Source.Raw;
                var oldLeft=ChemistryProjection.Create(draft.Reactants.Candidates[0].Document.Root).Source.Raw;
                var newLeft=ChemistryProjection.Create(proposal.Result.Candidates[0].Document.Root.Children[0]).Source.Raw;
                ghost=new(proposal.Id,session.State.Raw,session.Version,session.LeaseAssistance(proposal.Id).Epoch,draft.SeparatorSpan.End,draft.Region.ContentSpan.End,draft.Region.IsClosed?draft.Region.ReplacementSpan.End:-1,products,
                    CandidateExporter.ToMathMl(proposal.Result.Candidates[0]),oldLeft==newLeft?"":"Vế trái sẽ đổi: "+oldLeft+" → "+newLeft,Model.AcceptChemistrySpace);
            }
        }
        try{await module.InvokeVoidAsync("setGhost",sourceElement,ghost);}catch(JSException){}
    }
    [JSInvokable] public GhostPlan? PrepareGhost(string id,EditorInputStamp stamp,bool space)
    {
        preparedGhost=null;
        if(disposed||session.Assistance?.Draft is not {} snapshot||session.AssistanceProposals.Count!=1||space&&!Model.AcceptChemistrySpace||ghostDismissedVersion==session.Version)return null;
        var draft=AssistanceSerializer.DeserializeDraft(snapshot);
        if(!GhostCaret(draft,stamp))return null;
        try
        {
            if(!session.TryPrepareContentAssistance(session.LeaseAssistance(id),stamp,out var plan,space))return null;
            preparedGhost=plan;return new(plan!.Token.ToString(),plan.After.Raw,plan.Caret.Start,plan.Caret.End);
        }
        catch(InvalidOperationException){return null;}
    }
    [JSInvokable] public bool CommitGhost(string token)
    {
        var plan=preparedGhost;preparedGhost=null;
        if(plan==null||plan.Token.ToString()!=token||!session.CommitAssistance(plan))return false;
        debounce?.Cancel();notice="Đã nhận sản phẩm vào kết quả; nguồn gốc được giữ. Hủy cân bằng giữ lại sản phẩm đã nhận.";_=RenderCurrent();QueuePersist();return true;
    }
    [JSInvokable] public void CancelGhost(string token){if(preparedGhost?.Token.ToString()==token)preparedGhost=null;}
    [JSInvokable] public Task GhostCommitted(){QueuePersist();return Task.CompletedTask;}
    private async Task ChangeChemistrySpace(ChangeEventArgs args)
    {
        Model.AcceptChemistrySpace=args.Value is true;preparedGhost=null;session.DismissAssistance();
        await SavePreferences();StateHasChanged();
    }
}
