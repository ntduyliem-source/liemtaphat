using Locus.Core.Detection;

namespace Locus.Application;

public sealed partial class FormulaSession
{
    /// <summary>An explicit user command. Settings and the new result enter history together.</summary>
    public async Task<bool> ReanalyzeWithSettingsAsync(FormulaSettings settings)
    {
        if (disposed || IsComposing) return false;
        if (!settings.HasValidDomains || !settings.HasValidMarkerProfiles) throw new ArgumentException("Cặp bọc hoặc môn nhận diện chưa hợp lệ.", nameof(settings));
        if (!IsBusy && State.Settings == settings && State.HasResult) return true;
        var before = State;
        ClearAutoBalanceInput(); Invalidate();
        var version = Version;
        using var cancellation = new CancellationTokenSource(); pending = cancellation;
        IsBusy = true; Changed?.Invoke();
        try
        {
            var revision = NextSourceRevision();
            // A product snapshot may survive only while its original draft still routes to Chemistry.
            var previous = before.Content;
            if (previous != null)
                previous = new(previous.Raw, previous.Revision, previous.Regions.Select(r =>
                    r.ResultOverride?.ProductProposal != null && !ContentReactionDrafts.IsDraft(r.Raw, settings, revision)
                        ? r with { ResultOverride = null } : r), previous.Notices, previous.Commands);
            var content = await ContentAnalyzer.AnalyzeAsync(new(before.Raw, revision, settings), scheduler, previous, cancellation.Token);
            if (!Current(version, cancellation)) return false;
            var first = content.Regions.FirstOrDefault();
            AnalysisResult? analysis = content.Regions.Count == 1 && first?.Readings is {} set && set.Source.Raw == before.Raw
                ? new(set.Source, DetectionStatus.Accept, [set], set.Diagnostics) : null;
            Push(undo, before); redo.Clear(); reconciliation = null;
            State = before with { SourceRevision = revision, Settings = settings, Content = content, Analysis = analysis, RegionIndex = 0, CandidateId = first?.Display?.Id };
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception)
        {
            if (Current(version, cancellation)) Error = "Chưa nhận diện lại được. Môn và kết quả trước đó được giữ; có thể thử lại.";
            return false;
        }
        finally
        {
            if (pending == cancellation) { pending = null; IsBusy = false; Changed?.Invoke(); }
        }
    }
}
