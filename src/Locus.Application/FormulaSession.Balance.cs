using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Serialization;

namespace Locus.Application;

public sealed record BalanceAssessment(Guid RegionId,string CandidateId,string Status,string Message,CandidateSet? Result=null);

public sealed partial class FormulaSession
{
    private ContentSelection? balanceSelection;
    private long balanceEpoch;
    private CancellationTokenSource? balancePending;
    private readonly Dictionary<Guid,BalanceAssessment> balanceCache=[];
    private Guid[] cycle=[];
    private int cycleIndex;
    private bool cancelCycle, balanceCommitting;
    private (FormulaState After,string Selection,bool Cancel,ContentChange[] Changes)? quickBatch;
    private long autoInputRevision=-1;
    private Guid[] autoRegions=[];
    public bool AutoBalance { get; private set; }
    public bool IsBalancing { get; private set; }
    public string BalanceMessage { get; private set; }="";
    public IReadOnlyList<BalanceAssessment> BalanceAssessments { get; private set; }=[];
    public event Action? BalanceChanged;
    public int SelectedReactionCount=>BalanceAssessments.Count(a=>a.Status is "Balanced" or "cancel" or "AlreadyBalanced" or "NoSolution" or "NonUnique" or "Limit");
    public bool CanBalanceStep=>!IsBusy&&!IsComposing&&!IsBalancing&&cycleIndex<cycle.Length;
    public Guid? NextBalanceRegion=>CanBalanceStep?cycle[cycleIndex]:null;
    public string BalanceStepLabel=>IsBalancing?"Đang xét…":!CanBalanceStep?(BalanceAssessments.Count==1&&BalanceAssessments[0].Status=="AlreadyBalanced"?"Đã cân bằng":"Cân bằng"):
        (cancelCycle?"Hủy cân bằng":"Cân bằng")+(SelectedReactionCount>1?$" tiếp · {cycleIndex+1}/{cycle.Length}":"");
    public bool CanQuickUndoBatch(bool cancel)=>quickBatch is {} q&&q.Cancel==cancel&&ReferenceEquals(q.After,State)&&q.Selection==SelectionKey();
    public bool CanBalanceBatch(bool cancel)=>!IsBusy&&!IsComposing&&!IsBalancing&&(CanQuickUndoBatch(cancel)||BalanceAssessments.Any(a=>a.Status==(cancel?"cancel":"Balanced")));
    public void SetAutoBalance(bool enabled)
    {
        if(AutoBalance==enabled)return;
        AutoBalance=enabled;autoInputRevision=-1;autoRegions=[];InvalidateBalance();BalanceChanged?.Invoke();
    }
    private void ClearAutoBalanceInput(){autoInputRevision=-1;autoRegions=[];}
    private void InvalidateBalance()
    {
        balanceEpoch++;var old=balancePending;balancePending=null;try{old?.Cancel();}catch(ObjectDisposedException){}
        IsBalancing=false;BalanceAssessments=[];BalanceMessage="";
        if(!balanceCommitting){cycle=[];cycleIndex=0;quickBatch=null;}
    }
    public void CancelBalance()
    {InvalidateBalance();autoInputRevision=-1;autoRegions=[];BalanceMessage="Đã dừng; chưa thay đổi công thức nào trong lệnh đang chờ.";BalanceChanged?.Invoke();}
    private string SelectionKey()=>balanceSelection==null?"":$"{State.SourceRevision}:{balanceSelection.Start}:{balanceSelection.End}:"+string.Join(',',balanceSelection.Whole)+"/"+string.Join(',',balanceSelection.Partial);
    public void SetBalanceSelection(ContentSelection? selected)
    {
        if(selected!=null)
        {
            if(State.Content is not {} content)selected=null;
            else
            {
                var bounds=content.Select(selected.Start,selected.End);
                if(selected.Whole.Distinct().Count()!=selected.Whole.Count||selected.Whole.Any(id=>!bounds.Whole.Contains(id))||selected.Partial.Any(id=>!bounds.Whole.Contains(id)&&!bounds.Partial.Contains(id))||selected.Whole.Intersect(selected.Partial).Any())throw new ArgumentException("Invalid result selection.");
                selected=selected with{Whole=content.Regions.Where(r=>selected.Whole.Contains(r.Id)).Select(r=>r.Id).ToArray()};
            }
        }
        var oldKey=SelectionKey();balanceSelection=selected;
        if(oldKey==SelectionKey())return;
        InvalidateBalance();BalanceChanged?.Invoke();
    }
    public async Task<bool> RefreshBalanceAsync()
    {
        if(disposed||IsBusy||IsComposing||IsBalancing)return false;
        var selected=balanceSelection;
        if(State.Content is not {} content||selected==null||selected.Whole.Count==0)
        {BalanceAssessments=[];cycle=[];BalanceMessage=selected?.Partial.Count>0?"Chọn trọn phương trình để cân bằng.":"Chọn phương trình trong ô kết quả.";BalanceChanged?.Invoke();return true;}
        var state=State;long version=Version,epoch=balanceEpoch;using var cancellation=new CancellationTokenSource();balancePending=cancellation;IsBalancing=true;BalanceChanged?.Invoke();
        try
        {
            var results=new List<BalanceAssessment>();var regions=content.Regions.Where(r=>selected.Whole.Contains(r.Id)).ToArray();
            foreach(var region in regions)
            {
                BalanceMessage=$"Đang xét {results.Count+1}/{regions.Length}";BalanceChanged?.Invoke();
                results.Add(await AssessBalance(region,cancellation.Token));
                if(!BalanceCurrent(state,version,epoch,cancellation.Token))return false;
            }
            BalanceAssessments=results.ToArray();
            if(cycleIndex>=cycle.Length||cycle.Skip(cycleIndex).Any(id=>!results.Any(a=>a.RegionId==id&&a.Status==(cancelCycle?"cancel":"Balanced"))))BuildCycle();
            BalanceMessage=results.Count==1?results[0].Message:$"{results.Count(a=>a.Status=="Balanced")} có thể cân bằng · {results.Count(a=>a.Status=="cancel")} có thể hủy · {results.Count(a=>a.Status is not ("Balanced" or "cancel"))} giữ nguyên";
            return true;
        }
        catch(OperationCanceledException){return false;}
        catch(Exception){if(BalanceCurrent(state,version,epoch,CancellationToken.None)){BalanceMessage="Chưa xét được vùng chọn. Nội dung được giữ nguyên.";BalanceAssessments=[];cycle=[];}return false;}
        finally{if(ReferenceEquals(balancePending,cancellation)){balancePending=null;IsBalancing=false;BalanceChanged?.Invoke();}}
    }
    private void BuildCycle(bool? preferCancel=null)
    {
        bool hasBalance=BalanceAssessments.Any(a=>a.Status=="Balanced"),hasCancel=BalanceAssessments.Any(a=>a.Status=="cancel");
        cancelCycle=preferCancel==true?hasCancel:preferCancel==false?!hasBalance&&hasCancel:!hasBalance&&hasCancel;
        cycle=BalanceAssessments.Where(a=>a.Status==(cancelCycle?"cancel":"Balanced")).Select(a=>a.RegionId).ToArray();cycleIndex=0;
    }
    private bool BalanceCurrent(FormulaState state,long version,long epoch,CancellationToken token)=>!disposed&&!token.IsCancellationRequested&&!IsComposing&&!IsBusy&&ReferenceEquals(State,state)&&Version==version&&balanceEpoch==epoch;
    private async Task<BalanceAssessment> AssessBalance(ContentRegion region,CancellationToken token)
    {
        var candidate=region.Display;
        if(candidate==null)return new(region.Id,"","invalid",region.KeepText?"Vùng này đang được giữ là text; chọn cách đọc trong Chi tiết công thức.":"Cần chốt một cách đọc trước khi cân bằng.");
        if(candidate.Document.Domain!="chemistry")return new(region.Id,candidate.Id,"invalid","Cân bằng chỉ áp dụng cho phương trình Hóa.");
        if(candidate.Document.Root.Type!="ChemReaction")return new(region.Id,candidate.Id,"invalid","Cần phương trình có đủ hai vế.");
        if(HasManagedCoefficientChange(region))return new(region.Id,candidate.Id,"cancel","Có thể trả đúng hệ số trước khi Locus cân bằng.");
        if(balanceCache.TryGetValue(region.Id,out var cached)&&cached.CandidateId==candidate.Id)return cached;
        if(scheduler is not IContentBalanceScheduler executor)return new(region.Id,candidate.Id,"Limit","Host chưa hỗ trợ cân bằng vùng.");
        var set=region.ResultOverride?.Result??region.Readings!;
        var reply=await executor.BalanceAsync(new(CandidateSetSerializer.Serialize(set),candidate.Id),token);token.ThrowIfCancellationRequested();
        if(reply.CandidateId!=candidate.Id)throw new FormatException("Wrong balance candidate.");
        CandidateSet? result=reply.Result==null?null:CandidateSetSerializer.Deserialize(reply.Result);
        if(result!=null&&(result.Candidates.Count!=1||result.Candidates[0].Kind!="direct"||!ContentBalanceWire.SameSpecies(candidate,result.Candidates[0])||!ReactionBalancer.VerifyConservation(result.Candidates[0].Document.Root,token)))throw new FormatException("Balance result changed species.");
        if((reply.Status=="Balanced")!=(result!=null))throw new FormatException("Invalid balance status.");
        var assessment=new BalanceAssessment(region.Id,candidate.Id,reply.Status,reply.Message,result);
        if(balanceCache.Count>ContentDocument.RegionLimit)balanceCache.Clear();balanceCache[region.Id]=assessment;return assessment;
    }
    private ContentRegion BalancedRegion(ContentRegion region,CandidateSet result)=>region with{KeepFromAuto=false,
        ResultOverride=new(region.SelectedId??"draft:"+region.Id,result,result.Candidates[0].Id,"balance/"+AssistanceVersions.Solver,true,region.ResultOverride,region.ResultOverride?.ProductProposal)};
    private static ContentRegion CancelledRegion(ContentRegion region)=>region with{ResultOverride=region.ResultOverride!.BalanceBefore,KeepFromAuto=true};
    private bool CommitContentChanges(FormulaState before,IReadOnlyList<ContentChange> changes,string kind,bool mergeInput=false)
    {
        if(!ReferenceEquals(before,State)||changes.Count==0||State.Content is not {} content)return false;
        var map=changes.ToDictionary(c=>c.Before.Id);if(map.Any(p=>!ReferenceEquals(content.Regions.SingleOrDefault(r=>r.Id==p.Key),p.Value.Before)))return false;
        var command=new ContentCommand(Guid.NewGuid(),kind,changes);
        var updated=new ContentDocument(content.Raw,content.Revision,content.Regions.Select(r=>map.TryGetValue(r.Id,out var c)?c.After:r),content.Notices,content.Commands.Append(command).TakeLast(HistoryLimit));
        var after=State with{Content=updated,CandidateId=updated.Regions.ElementAtOrDefault(State.RegionIndex)?.Display?.Id};
        DocumentCodec.ValidateFormula(after,new());
        if(!mergeInput){ClearAutoBalanceInput();Push(undo,State);}redo.Clear();balanceCommitting=true;try{Invalidate();State=after;Changed?.Invoke();}finally{balanceCommitting=false;}
        return true;
    }
    public async Task<bool> BalanceStepAsync()
    {
        if(!CanBalanceStep||State.Content==null)return false;
        var id=cycle[cycleIndex];var region=State.Content.Regions.Single(r=>r.Id==id);var assessment=BalanceAssessments.SingleOrDefault(a=>a.RegionId==id);
        if(assessment==null||assessment.CandidateId!=region.Display?.Id)return false;
        var after=cancelCycle?CancelledRegion(region):BalancedRegion(region,assessment.Result!);var oldCycle=cycle;int oldIndex=cycleIndex;bool oldCancel=cancelCycle;
        quickBatch=null;if(!CommitContentChanges(State,[new(region,after)],cancelCycle?"cancel-balance":"balance"))return false;
        cycle=oldCycle;cycleIndex=oldIndex+1;cancelCycle=oldCancel;
        bool ended=cycleIndex>=oldCycle.Length;await RefreshBalanceAsync();if(ended){BuildCycle(!oldCancel);BalanceChanged?.Invoke();}return true;
    }
    public async Task<bool> BalanceBatchAsync(bool cancel)
    {
        if(!CanBalanceBatch(cancel)||State.Content==null)return false;
        if(CanQuickUndoBatch(cancel))
        {
            var changes=quickBatch!.Value.Changes.Select(c=>new ContentChange(c.After,c.Before)).ToArray();quickBatch=null;cycle=[];
            bool restored=CommitContentChanges(State,changes,"undo-batch");await RefreshBalanceAsync();return restored;
        }
        var state=State;long version=Version,epoch=balanceEpoch;var selection=SelectionKey();var assessments=BalanceAssessments.ToArray();
        using var cancellation=new CancellationTokenSource();balancePending=cancellation;IsBalancing=true;BalanceChanged?.Invoke();
        try
        {
            var changes=new List<ContentChange>();int i=0;
            foreach(var assessment in assessments)
            {
                BalanceMessage=$"Đang xét {++i}/{assessments.Length}";BalanceChanged?.Invoke();await Task.Delay(1,cancellation.Token);
                if(!BalanceCurrent(state,version,epoch,cancellation.Token))return false;
                if(assessment.Status!=(cancel?"cancel":"Balanced"))continue;
                var region=state.Content!.Regions.Single(r=>r.Id==assessment.RegionId);
                if(region.Display?.Id!=assessment.CandidateId)return false;
                changes.Add(new(region,cancel?CancelledRegion(region):BalancedRegion(region,assessment.Result!)));
            }
            if(!BalanceCurrent(state,version,epoch,cancellation.Token))return false;
            balancePending=null;IsBalancing=false;cycle=[];
            if(!CommitContentChanges(state,changes,cancel?"cancel-balance-all":"balance-all"))return false;
            quickBatch=(State,selection,cancel,changes.ToArray());await RefreshBalanceAsync();
            BalanceMessage=$"Đã {(cancel?"hủy cân bằng":"cân bằng")} {changes.Count} vùng · giữ nguyên {assessments.Length-changes.Count} vùng.";BalanceChanged?.Invoke();return true;
        }
        catch(OperationCanceledException){return false;}
        catch(Exception){if(ReferenceEquals(state,State))BalanceMessage="Lệnh chưa hoàn tất; chưa thay đổi vùng nào.";return false;}
        finally{if(ReferenceEquals(balancePending,cancellation)){balancePending=null;IsBalancing=false;BalanceChanged?.Invoke();}}
    }
    public async Task<bool> ApplyAutoBalanceAsync()
    {
        if(!AutoBalance||disposed||IsBusy||IsComposing||IsBalancing||autoInputRevision!=State.SourceRevision||State.Content is not {} content||autoRegions.Length==0)return false;
        var ids=autoRegions;autoRegions=[];autoInputRevision=-1;var state=State;long version=Version,epoch=balanceEpoch;
        using var cancellation=new CancellationTokenSource();balancePending=cancellation;IsBalancing=true;BalanceChanged?.Invoke();
        try
        {
            var changes=new List<ContentChange>();
            foreach(var region in content.Regions.Where(r=>ids.Contains(r.Id)&&!r.KeepText&&!r.KeepFromAuto&&r.Display?.Kind=="direct"&&r.ResultOverride==null&&r.Display.Diagnostics.Any(d=>d.Severity is "warning" or "error")!=true&&r.Readings?.Diagnostics.Any(d=>d.Severity is "warning" or "error")!=true))
            {
                var assessment=await AssessBalance(region,cancellation.Token);
                if(!AutoBalance||!BalanceCurrent(state,version,epoch,cancellation.Token))return false;
                if(assessment.Status=="Balanced")changes.Add(new(region,BalancedRegion(region,assessment.Result!)));
            }
            if(!AutoBalance||!BalanceCurrent(state,version,epoch,cancellation.Token)||changes.Count==0)return false;
            balancePending=null;IsBalancing=false;cycle=[];quickBatch=null;
            return CommitContentChanges(state,changes,"auto-balance",mergeInput:true);
        }
        catch(OperationCanceledException){return false;}
        catch(Exception){if(ReferenceEquals(state,State))BalanceMessage="Chưa tự cân bằng được; giữ nguyên nội dung đã dán.";return false;}
        finally{if(ReferenceEquals(balancePending,cancellation)){balancePending=null;IsBalancing=false;BalanceChanged?.Invoke();}}
    }
}
