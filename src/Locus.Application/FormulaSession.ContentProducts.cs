using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Serialization;

namespace Locus.Application;

public sealed partial class FormulaSession
{
    /// <summary>Accept a preview into the result, preserving the typed draft and separate pre-balance coefficients.</summary>
    public bool TryPrepareContentAssistance(AssistanceLease lease,EditorInputStamp input,out PreparedAssistance? plan,bool appendSpace=false)
    {
        plan=null;
        if(disposed||IsBusy||IsComposing||IsAssisting||IsBalancing||input.Raw!=State.Raw||input.Composing||input.Pending||!input.Focused||lease.SessionId!=Id||lease.Version!=Version||lease.Epoch!=assistanceEpoch||lease.ContextId!=assistanceContextId)return false;
        var proposal=AssistanceProposals.FirstOrDefault(p=>p.Id==lease.ProposalId);
        if(proposal==null||proposal.Region.Source.Id!=new SourceSnapshot(State.Raw,State.SourceRevision).Id||proposal.Context.SettingsFingerprint!=AssistanceHistory.SettingsFingerprint(State.Settings))return false;
        try{new EditorSelection(input.SelectionStart,input.SelectionEnd).Validate(proposal.Region.Source);}catch(ArgumentException){return false;}
        var before=State;var content=State.Content??new(State.Raw,State.SourceRevision,[]);
        var region=content.Regions.SingleOrDefault(r=>r.Start==proposal.Region.ReplacementSpan.Start&&r.End==proposal.Region.ReplacementSpan.End);
        if(region==null)
        {
            if(content.Regions.Any(r=>r.Start<proposal.Region.ReplacementSpan.End&&r.End>proposal.Region.ReplacementSpan.Start)||content.Regions.Count>=ContentDocument.RegionLimit)return false;
            region=new(Guid.NewGuid(),0,0,proposal.Region.ReplacementSpan.Start,proposal.Region.ReplacementSpan.End,State.Raw,null,null);
            content=new(content.Raw,content.Revision,content.Regions.Append(region).OrderBy(r=>r.Start),content.Notices,content.Commands);
        }
        var baseId=region.SelectedId??"draft:"+region.Id;ContentResultOverride? original=region.ResultOverride;
        string? productSnapshot=null;
        if(proposal.Kind=="complete-reaction")
        {
            var draft=ReactionDraftParser.Parse(proposal.Region).Draft;if(draft==null)return false;
            productSnapshot=AssistanceSerializer.Serialize(proposal);
            var intermediate=ChemistryProjection.ProductsBeforeBalance(draft,proposal);
            original=new(baseId,intermediate,intermediate.Candidates[0].Id,"product/"+proposal.Provenance.RuleId,ProductProposal:productSnapshot);
        }
        else if(proposal.Kind!="balance"||region.Display==null)return false;
        var result=proposal.Result;var resultCandidate=result.Candidates[0];var priorCandidate=original?.Candidate??region.Selected;
        if(priorCandidate==null||!ContentBalanceWire.SameSpecies(priorCandidate,resultCandidate))return false;
        bool changed=ChemistryProjection.Create(priorCandidate.Document.Root).Source.Raw!=ChemistryProjection.Create(resultCandidate.Document.Root).Source.Raw;
        var accepted=changed?new ContentResultOverride(baseId,result,resultCandidate.Id,"balance/"+AssistanceVersions.Solver,true,original,productSnapshot):original;
        if(accepted==null)return false;
        var updated=region with{ResultOverride=accepted,KeepText=false,KeepFromAuto=false,Problem=null};
        var replacement=content.Change(updated,proposal.Kind=="complete-reaction"?"product":"balance");
        string raw=State.Raw;long revision=State.SourceRevision;int caret=proposal.Region.ReplacementSpan.End;
        if(appendSpace)
        {
            // Shared product assistance is single-input. A Space is appended outside the source region.
            if(caret!=raw.Length||raw.Length>=MaxSourceLength)return false;
            raw+=" ";caret++;revision=sourceCounter>=long.MaxValue-1?0:sourceCounter+1;
            replacement=new(raw,revision,replacement.Regions.Select(r=>RebaseContentSnapshot(r,revision)),replacement.Notices,replacement.Commands);
        }
        int index=replacement.Regions.ToList().FindIndex(r=>r.Id==region.Id);
        var after=State with{Raw=raw,SourceRevision=revision,Analysis=appendSpace?null:State.Analysis,Content=replacement,RegionIndex=index,CandidateId=replacement.Regions[index].Display?.Id};
        DocumentCodec.ValidateFormula(after,new());plan=new(before,after,new(caret,caret),lease);return true;
    }
    private static ContentRegion RebaseContentSnapshot(ContentRegion r,long revision)
    {
        if(r.Readings is not {} set)return r;
        var source=new SourceSnapshot(set.Source.Raw,revision);
        var candidates=set.Candidates.Select(c=>new Candidate(c.Kind,c.Document,source,c.ContentSpan,c.ReplacementSpan,c.Diagnostics,c.Edits,c.Provenance)).ToArray();
        var readings=new CandidateSet(source,set.ContentSpan,set.ReplacementSpan,candidates,set.Diagnostics,markers:set.Markers,intent:set.Intent);
        var selected=r.SelectedId==null?null:candidates[set.Candidates.ToList().FindIndex(c=>c.Id==r.SelectedId)].Id;
        return r with{Readings=readings,SelectedId=selected,ResultOverride=r.ResultOverride?.Reanchor(selected??"draft:"+r.Id)};
    }
    public bool DropContentProducts(Guid id)
    {
        if(disposed||IsBusy||IsComposing||IsBalancing||State.Content is not {} content)return false;
        var region=content.Regions.SingleOrDefault(r=>r.Id==id);if(region?.ResultOverride?.ProductProposal==null)return false;
        cycle=[];quickBatch=null;
        return CommitContentChanges(State,[new(region,region with{ResultOverride=null,KeepFromAuto=true})],"drop-product");
    }
}
