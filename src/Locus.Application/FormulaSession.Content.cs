using Locus.Core.Detection;

namespace Locus.Application;

public sealed partial class FormulaSession
{
    private ContentDocument? reconciliation;
    public void PromoteSnapshot()
    {
        if (State.Content != null || State.Analysis == null || State.Raw.Length > MaxSourceLength) return;
        var regions = State.Analysis.Regions.Select(r => ContentDocument.FromSet(r, 0)).ToArray();
        if (State.CandidateId != null && State.RegionIndex < regions.Length) regions[State.RegionIndex] = regions[State.RegionIndex] with { SelectedId = State.CandidateId };
        State = State with { Content = new(State.Raw, State.SourceRevision, regions) };
    }
    public async Task<bool> AnalyzeContentAsync()
    {
        if (disposed || IsComposing) return false;
        var previous = State.Content ?? reconciliation;
        Invalidate(); var version = Version; var input = State;
        State = input with { Analysis = null, CandidateId = null, RegionIndex = 0, Content = null };
        var cancellation = new CancellationTokenSource(); pending = cancellation;
        IsBusy = true; Changed?.Invoke();
        try
        {
            var content = await ContentAnalyzer.AnalyzeAsync(new(input.Raw, input.SourceRevision, input.Settings), scheduler, previous, cancellation.Token);
            if (!Current(version, cancellation)) return false;
            var first = content.Regions.FirstOrDefault();
            // Keep the existing SC1 assistance adapter usable for a single complete source window.
            AnalysisResult? analysis = null;
            if (content.Regions.Count == 1 && first?.Readings is {} set && set.Source.Raw == input.Raw)
                analysis = new(set.Source, DetectionStatus.Accept, [set], set.Diagnostics);
            State = input with { Analysis = analysis, Content = content, RegionIndex = 0, CandidateId = first?.Display?.Id };
            autoRegions=autoInputRevision==input.SourceRevision?content.Regions.Where(r=>previous?.Regions.Any(old=>old.Id==r.Id)!=true).Select(r=>r.Id).ToArray():[];
            reconciliation = null; return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception)
        {
            if (Current(version, cancellation)) Error = "Chưa xử lý được đoạn này. Toàn bộ nguồn vẫn được giữ; có thể thử lại hoặc tách đoạn.";
            return false;
        }
        finally
        {
            if (pending == cancellation) { pending = null; IsBusy = false; Changed?.Invoke(); }
            cancellation.Dispose();
        }
    }
    public bool FocusContentRegion(Guid id)
    {
        if (disposed || IsBusy || IsComposing || State.Content == null) return false;
        int index = State.Content.Regions.ToList().FindIndex(r => r.Id == id);
        if (index < 0) return false;
        if(State.RegionIndex==index&&State.CandidateId==State.Content.Regions[index].Display?.Id)return true;
        Invalidate(); State = State with { RegionIndex = index, CandidateId = State.Content.Regions[index].Display?.Id }; Changed?.Invoke(); return true;
    }
    private bool SelectContentReading(int index, string candidateId)
    {
        if (disposed || IsBusy || IsComposing || State.Content == null || index < 0 || index >= State.Content.Regions.Count) return false;
        var region = State.Content.Regions[index];
        if (region.Readings?.Candidates.Any(c => c.Id == candidateId) != true) return false;
        if(region.SelectedId==candidateId&&!region.KeepText&&region.ResultOverride==null)return true;
        Push(undo, State); redo.Clear(); Invalidate();
        var updated = region with { SelectedId = candidateId, KeepText = false, Problem = null, ResultOverride = null };
        State = State with { Content = State.Content.Change(updated,"reading"), RegionIndex = index, CandidateId = candidateId }; Changed?.Invoke(); return true;
    }
    public bool KeepContentText(Guid id, bool keep)
    {
        if (disposed || IsBusy || IsComposing || State.Content == null) return false;
        var region = State.Content.Regions.FirstOrDefault(r => r.Id == id);
        if (region == null || region.KeepText == keep) return false;
        Push(undo, State); redo.Clear(); Invalidate();
        var updated = region with { KeepText = keep };
        int index = State.Content.Regions.ToList().FindIndex(r => r.Id == id);
        State = State with { Content = State.Content.Change(updated,"keep-text"), RegionIndex = index, CandidateId = updated.Display?.Id }; Changed?.Invoke(); return true;
    }
    public void CancelContentAnalysis()
    {
        if (disposed || !IsBusy) return;
        Invalidate(); Error = "Đã dừng xử lý. Nguồn được giữ nguyên."; Changed?.Invoke();
    }
}
