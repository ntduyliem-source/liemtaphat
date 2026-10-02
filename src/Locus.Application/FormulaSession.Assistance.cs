using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;
using Locus.Core.Domains;

namespace Locus.Application;

public readonly record struct AssistanceLease(Guid SessionId,long Version,long Epoch,string ProposalId,string ContextId);
public sealed record EditorInputStamp(string Raw,int SelectionStart,int SelectionEnd,bool Composing,bool Pending,bool Focused);
public sealed class PreparedAssistance
{
    public Guid Token { get; }=Guid.NewGuid();
    public FormulaState After { get; }
    public EditorSelection Caret { get; }
    internal FormulaState Before { get; }
    internal AssistanceLease Lease { get; }
    internal PreparedAssistance(FormulaState before,FormulaState after,EditorSelection caret,AssistanceLease lease)
    {Before=before;After=after;Caret=caret;Lease=lease;}
}

public sealed partial class FormulaSession
{
    private CancellationTokenSource? assistancePending;
    private long assistanceEpoch;
    private string? assistanceContextId;
    public ChemistryAssistanceResponse? Assistance { get; private set; }
    public IReadOnlyList<AssistanceProposal> AssistanceProposals { get; private set; }=[];
    public bool IsAssisting { get; private set; }
    public event Action? AssistanceChanged;
    public void DismissAssistance()
    {
        assistanceEpoch++;var old=assistancePending;assistancePending=null;
        try{old?.Cancel();}catch(ObjectDisposedException){}
        Assistance=null;AssistanceProposals=[];assistanceContextId=null;IsAssisting=false;AssistanceChanged?.Invoke();
    }
    public async Task<bool> RequestAssistanceAsync(int regionStart,IEnumerable<AssistanceCondition>? conditions=null)
    {
        if(disposed||IsBusy||IsComposing||scheduler is not IChemistryAssistanceScheduler executor)return false;
        DismissAssistance();var version=Version;var epoch=assistanceEpoch;var state=State;
        var facts=(conditions??[]).ToArray();var context=AssistanceHistory.Context(state.Settings,facts.Select(f=>new ConditionFact(f.Key,f.Value)));
        var cancellation=new CancellationTokenSource();assistancePending=cancellation;assistanceContextId=context.Id;IsAssisting=true;AssistanceChanged?.Invoke();
        try
        {
            var response=await executor.AssistAsync(new(new(state.Raw,state.SourceRevision,state.Settings),regionStart,facts),cancellation.Token);
            if(disposed||IsComposing||version!=Version||epoch!=assistanceEpoch||cancellation.IsCancellationRequested)return false;
            if(response.SourceId!=new SourceSnapshot(state.Raw,state.SourceRevision).Id||response.SourceRevision!=state.SourceRevision||response.ContextId!=context.Id)throw new FormatException("Assistance response does not match this source/context.");
            var proposals=response.Proposals.Select(AssistanceSerializer.Deserialize).ToArray();
            if(proposals.Any(p=>p.Region.Source.Id!=response.SourceId||p.Context.Id!=context.Id))throw new FormatException("Proposal context mismatch.");
            Assistance=response;AssistanceProposals=Array.AsReadOnly(proposals);return true;
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){return false;}
        catch(Exception)
        {
            if(!disposed&&version==Version&&epoch==assistanceEpoch)Assistance=new(new SourceSnapshot(state.Raw,state.SourceRevision).Id,state.SourceRevision,context.Id,"limit","Chưa xử lý được hỗ trợ Hóa. Nguồn vẫn được giữ; có thể thử lại.",[],null);
            return false;
        }
        finally{if(assistancePending==cancellation){assistancePending=null;IsAssisting=false;AssistanceChanged?.Invoke();}cancellation.Dispose();}
    }
    public AssistanceLease LeaseAssistance(string id)
    {
        if(disposed||IsBusy||IsAssisting||IsComposing||!AssistanceProposals.Any(p=>p.Id==id)||assistanceContextId==null)throw new InvalidOperationException("No current assistance proposal.");
        return new(Id,Version,assistanceEpoch,id,assistanceContextId);
    }
    public bool AcceptAssistance(AssistanceLease lease,EditorInputStamp input,out EditorSelection caret,bool appendSpace=false)
    {
        caret=default;if(!TryPrepareAssistance(lease,input,out var plan,appendSpace)||!CommitAssistance(plan!))return false;
        caret=plan!.Caret;return true;
    }
    // Preparation has no effect on source, history or clipboard. The UI rechecks live input before committing.
    public bool TryPrepareAssistance(AssistanceLease lease,EditorInputStamp input,out PreparedAssistance? plan,bool appendSpace=false)
    {
        plan=null;
        if(disposed||IsBusy||IsComposing||IsAssisting||input.Raw!=State.Raw||input.Composing||input.Pending||!input.Focused||lease.SessionId!=Id||lease.Version!=Version||lease.Epoch!=assistanceEpoch||lease.ContextId!=assistanceContextId)return false;
        var proposal=AssistanceProposals.FirstOrDefault(p=>p.Id==lease.ProposalId);
        if(proposal==null||proposal.Region.Source.Id!=new SourceSnapshot(State.Raw,State.SourceRevision).Id||proposal.Context.SettingsFingerprint!=AssistanceHistory.SettingsFingerprint(State.Settings)||(State.Transformations?.Entries.Count??0)>=AssistanceHistory.Limit)return false;
        var selection=new EditorSelection(input.SelectionStart,input.SelectionEnd);
        try{selection.Validate(proposal.Region.Source);}catch(ArgumentException){return false;}
        var before=State;string raw=proposal.AfterRaw;
        int afterCaret=proposal.Region.ContentSpan.Start+proposal.ReplacementText.Length+(proposal.Region.IsClosed?proposal.Region.Markers?.Close.Length??0:0);
        if(appendSpace)raw=raw.Insert(afterCaret++," ");if(raw.Length>MaxSourceLength)return false;
        var source=new SourceSnapshot(raw,NextSourceRevision());var caret=new EditorSelection(afterCaret,afterCaret);
        var accepted=new AcceptedTransformation(proposal,before with{Transformations=null},source,selection,caret,appendSpace);
        var history=new AssistanceHistory((before.Transformations?.Entries??[]).Append(accepted));
        var analysis=RebaseAfterAssistance(before,proposal,source,appendSpace);
        int selected=analysis.Regions.ToList().FindIndex(r=>r.ContentSpan.Start==proposal.Region.ContentSpan.Start);
        var state=new FormulaState(raw,source.Revision,before.Settings,analysis,Math.Max(0,selected),selected<0?null:analysis.Regions[selected].Candidates[0].Id,history);
        DocumentCodec.ValidateFormula(state,new());
        plan=new(before,state,caret,lease);return true;
    }
    public bool CommitAssistance(PreparedAssistance plan)
    {
        ArgumentNullException.ThrowIfNull(plan);var lease=plan.Lease;
        if(disposed||IsBusy||IsComposing||IsAssisting||!ReferenceEquals(State,plan.Before)||lease.SessionId!=Id||lease.Version!=Version||lease.Epoch!=assistanceEpoch||lease.ContextId!=assistanceContextId||!AssistanceProposals.Any(p=>p.Id==lease.ProposalId))return false;
        ClearAutoBalanceInput();Push(undo,State);redo.Clear();Invalidate();State=plan.After;sourceCounter=Math.Max(sourceCounter,State.SourceRevision);Changed?.Invoke();return true;
    }
    public bool RestoreBeforeAssistance(out EditorSelection selection)
    {
        selection=default;if(disposed||IsBusy||IsComposing||State.Transformations==null)return false;
        var entries=State.Transformations.Entries;var last=entries[^1];
        var restored=last.Before with{Transformations=entries.Count>1?new AssistanceHistory(entries.Take(entries.Count-1)):null};
        ClearAutoBalanceInput();Push(undo,State);redo.Clear();Invalidate();State=restored;selection=last.SelectionBefore;Changed?.Invoke();return true;
    }
    private static AnalysisResult RebaseAfterAssistance(FormulaState before,AssistanceProposal proposal,SourceSnapshot after,bool appendSpace)
    {
        int delta=proposal.ReplacementText.Length-proposal.Region.ContentSpan.Length+(appendSpace?1:0);var replaced=proposal.Region.ReplacementSpan;
        var regions=new List<CandidateSet>();var diagnostics=new List<Diagnostic>();
        if(before.Analysis!=null)
        {
            foreach(var region in before.Analysis.Regions)
            {
                if(region.ReplacementSpan.Start<replaced.End&&region.ReplacementSpan.End>replaced.Start)continue;
                int shift=region.ReplacementSpan.Start>=replaced.End?delta:0;
                TextSpan Span(TextSpan s)=>new(s.Start+shift,s.End+shift);
                Diagnostic Diagnostic(Diagnostic d)=>new(d.Code,d.Severity,Span(d.Span),d.Message);
                var candidates=region.Candidates.Select(c=>new Candidate(c.Kind,ScientificDocument.Create(c.Document.Domain,ChemistryProjection.Rebase(c.Document.Root,shift),c.GrammarVersion),after,Span(c.ContentSpan),Span(c.ReplacementSpan),c.Diagnostics.Select(Diagnostic),c.Edits.Select(e=>new SourceEdit(Span(e.Span),e.Text)),c.Provenance)).ToArray();
                int selection=region.Candidates.ToList().FindIndex(c=>c.Id==region.SelectedCandidateId);
                regions.Add(new(after,Span(region.ContentSpan),Span(region.ReplacementSpan),candidates,region.Diagnostics.Select(Diagnostic),selection<0?null:candidates[selection].Id,region.Markers,region.Intent));
            }
            foreach(var d in before.Analysis.Diagnostics)
            {
                if(d.Span.Start<replaced.End&&d.Span.End>replaced.Start)continue;int shift=d.Span.Start>=replaced.End?delta:0;
                diagnostics.Add(new(d.Code,d.Severity,new(d.Span.Start+shift,d.Span.End+shift),d.Message));
            }
        }
        if(proposal.Region.IsClosed)
        {
            var content=new TextSpan(proposal.Region.ContentSpan.Start,proposal.Region.ContentSpan.Start+proposal.ReplacementText.Length);
            var replacement=new TextSpan(replaced.Start,replaced.End+delta-(appendSpace?1:0));
            var candidate=new Candidate("direct",new ChemistryDocument(ChemistryProjection.Rebase(proposal.Result.Candidates[0].Document.Root,content.Start)),after,content,replacement,provenance:"accepted/"+proposal.Id);
            regions.Add(new(after,content,replacement,new[]{candidate},markers:proposal.Region.Markers,intent:proposal.Region.Intent));
        }
        return new(after,regions.Count>0?DetectionStatus.Accept:DetectionStatus.Reject,regions.OrderBy(r=>r.ReplacementSpan.Start),diagnostics,!proposal.Region.IsClosed||diagnostics.Any(d=>d.Code.Contains("UNCLOSED",StringComparison.Ordinal)));
    }
}
