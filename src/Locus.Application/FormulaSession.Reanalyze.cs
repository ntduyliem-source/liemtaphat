using Locus.Core.Detection;

namespace Locus.Application;

public sealed partial class FormulaSession
{
    /// <summary>Commit the user's mode immediately; attach its result only while the request remains current.</summary>
    public async Task<bool> ReanalyzeWithSettingsAsync(FormulaSettings settings)
    {
        if (disposed || IsComposing) return false;
        if (!settings.HasValidDomains || !settings.HasValidMarkerProfiles) throw new ArgumentException("Cặp bọc hoặc môn nhận diện chưa hợp lệ.", nameof(settings));
        if (!IsBusy && State.Settings == settings && State.HasResult) return true;
        var before = State;
        var previous = before.Content ?? reconciliation;
        ClearAutoBalanceInput(); Invalidate();
        var version = Version;
        var revision = NextSourceRevision();
        // A product snapshot may survive only while its original draft still routes to Chemistry.
        if (previous != null)
            previous = new(previous.Raw, previous.Revision, previous.Regions.Select(r =>
                r.ResultOverride?.ProductProposal != null && !ContentReactionDrafts.IsDraft(r.Raw, settings, revision)
                    ? r with { ResultOverride = null } : r), previous.Notices, previous.Commands);
        // New input must inherit the clicked mode even if it arrives before this worker finishes.
        // Clear stale results and record one mode action now, so cancellation and Undo are independent.
        Push(undo, before); redo.Clear(); reconciliation = previous;
        State = new(before.Raw, revision, settings, Transformations: before.Transformations);
        var input = State;
        using var cancellation = new CancellationTokenSource(); pending = cancellation;
        IsBusy = true; Changed?.Invoke();
        try
        {
            var content = await ContentAnalyzer.AnalyzeAsync(new(before.Raw, revision, settings), scheduler, previous, cancellation.Token);
            if (!Current(version, cancellation)) return false;
            var first = content.Regions.FirstOrDefault();
            AnalysisResult? analysis = content.Regions.Count == 1 && first?.Readings is {} set && set.Source.Raw == before.Raw
                ? new(set.Source, DetectionStatus.Accept, [set], set.Diagnostics) : null;
            reconciliation = null;
            State = input with { Content = content, Analysis = analysis, RegionIndex = 0, CandidateId = first?.Display?.Id };
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception)
        {
            if (Current(version, cancellation)) Error = "Chưa nhận diện lại được. Môn đã chọn và nguồn được giữ; có thể thử lại.";
            return false;
        }
        finally
        {
            if (pending == cancellation) { pending = null; IsBusy = false; Changed?.Invoke(); }
        }
    }
}
