using Locus.Core;
using Locus.Core.Detection;

namespace Locus.Application;

public sealed record FormulaState(string Raw, long SourceRevision, FormulaSettings Settings, AnalysisResult? Analysis = null, int RegionIndex = 0, string? CandidateId = null, AssistanceHistory? Transformations = null, ContentDocument? Content = null)
{
    public CandidateSet? Region => Content != null ? RegionIndex >= 0 && RegionIndex < Content.Regions.Count ? Content.Regions[RegionIndex].Readings : null : Analysis != null && RegionIndex >= 0 && RegionIndex < Analysis.Regions.Count ? Analysis.Regions[RegionIndex] : null;
    public Candidate? Candidate => Content != null ? Content.Regions.ElementAtOrDefault(RegionIndex)?.Display is {} display && display.Id==CandidateId ? display : null : Region?.Candidates.FirstOrDefault(c => c.Id == CandidateId);
    public bool HasResult => Analysis != null || Content != null;
}

public readonly record struct ExportLease(Guid SessionId, long Version, string CandidateId);

/// <summary>One session per editor, called on its host UI dispatcher. History retains the exact immutable result.</summary>
public sealed partial class FormulaSession : IDisposable
{
    public const int MaxSourceLength = 4096;
    public const int HistoryLimit = 100;
    private readonly IAnalysisScheduler scheduler;
    private readonly List<FormulaState> undo = [], redo = [];
    private CancellationTokenSource? pending;
    private FormulaState? compositionStart;
    private long sourceCounter;
    private bool disposed;
    public Guid Id { get; } = Guid.NewGuid();
    public long Version { get; private set; }
    public FormulaState State { get; private set; } = new("", 0, new());
    public bool IsComposing { get; private set; }
    public bool IsBusy { get; private set; }
    public string Error { get; private set; } = "";
    public bool CanUndo => !disposed && !IsComposing && undo.Count > 0;
    public bool CanRedo => !disposed && !IsComposing && redo.Count > 0;
    public EditorSelection? HistorySelection { get; private set; }
    public bool CanExport => !disposed && !IsComposing && !IsBusy && State.Candidate != null;
    public event Action? Changed;
    public FormulaSession(IAnalysisScheduler scheduler) => this.scheduler = scheduler;

    /// <summary>Startup migration preserves completed historical snapshots; only future analysis uses the profiles.</summary>
    public string UpgradeMarkerProfiles()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (State.Settings.MarkerProfiles != null) return "";
        if (IsComposing) throw new InvalidOperationException("Hoàn tất ghép dấu trước khi nâng cài đặt.");
        var migration = MarkerPreferences.Migrate(State.Settings.Open, State.Settings.Close);
        Invalidate(); State = State with { Settings = State.Settings with { MarkerProfiles = migration.Profiles } };
        Changed?.Invoke(); return migration.Notice;
    }

    public void UpdateSource(string raw, bool composing = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(raw);
        if (State.Raw == raw && IsComposing == composing) return;
        if (composing && !IsComposing) compositionStart = State;
        var previous = State;
        reconciliation ??= previous.Content;
        if (!IsComposing && !composing) Push(undo, previous);
        if (IsComposing && !composing && compositionStart != null)
        {
            if (compositionStart.Raw != raw) Push(undo, compositionStart);
            compositionStart = null;
        }
        IsComposing = composing;
        redo.Clear();
        Invalidate();
        State = new(raw, NextSourceRevision(), previous.Settings, Transformations: previous.Transformations);
        autoInputRevision=AutoBalance&&!composing?State.SourceRevision:-1;autoRegions=[];
        Changed?.Invoke();
    }

    public void Configure(FormulaSettings settings)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.HasValidDomains) throw new ArgumentException("Unknown detection option.", nameof(settings));
        if (!settings.HasValidMarkerProfiles) throw new ArgumentException("Cặp bọc chưa hợp lệ; cài đặt trước đó được giữ.", nameof(settings));
        if (settings == State.Settings) return;
        autoInputRevision=-1;autoRegions=[];
        if (IsComposing) throw new InvalidOperationException("Hoàn tất ghép dấu trước khi đổi cài đặt.");
        bool keepSnapshot = State.HasResult && State.Settings with { EnabledDomains = settings.EnabledDomains } == settings;
        if (!keepSnapshot) reconciliation = null;
        Push(undo, State); redo.Clear(); Invalidate();
        // A detector preference does not reinterpret a completed snapshot. Editing or an explicit rebuild does.
        State = keepSnapshot ? State with { Settings = settings } : new(State.Raw, NextSourceRevision(), settings, Transformations: State.Transformations);
        Changed?.Invoke();
    }

    public async Task<bool> AnalyzeAsync()
    {
        if (disposed || IsComposing) return false;
        Invalidate();
        var version = Version;
        var input = State;
        State = input with { Analysis = null, CandidateId = null };
        if (input.Raw.Length > MaxSourceLength) { Error = "Mỗi công thức hỗ trợ tối đa 4.096 ký tự. Nguồn vẫn được giữ nguyên."; Changed?.Invoke(); return false; }
        if (string.IsNullOrWhiteSpace(input.Raw)) { Changed?.Invoke(); return false; }
        if (input.Settings.Mode == InputMode.Markers && !new MarkerConfiguration(input.Settings.Open, input.Settings.Close).IsValid) { Error = "Cặp bọc chưa hợp lệ."; Changed?.Invoke(); return false; }
        var cancellation = new CancellationTokenSource(); pending = cancellation;
        IsBusy = true; Changed?.Invoke();
        try
        {
            var result = await scheduler.AnalyzeAsync(new(input.Raw, input.SourceRevision, input.Settings), cancellation.Token);
            if (!Current(version, cancellation)) return false;
            if (result.Source.Raw != input.Raw || result.Source.Revision != input.SourceRevision) throw new InvalidOperationException("Kết quả không thuộc nguồn hiện tại.");
            State = input with { Analysis = result, RegionIndex = 0, CandidateId = result.Regions.FirstOrDefault()?.Candidates.FirstOrDefault()?.Id };
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
        catch (Exception ex)
        {
            if (Current(version, cancellation)) Error = ex is TimeoutException ? "Phân tích quá thời gian. Nguồn vẫn được giữ; thử lại hoặc tách công thức." : "Chưa xử lý được nguồn này. Nội dung của bạn vẫn được giữ.";
            return false;
        }
        finally
        {
            if (pending == cancellation) { pending = null; IsBusy = false; Changed?.Invoke(); }
            cancellation.Dispose();
        }
    }

    public bool Select(int regionIndex, string candidateId)
    {
        if (State.Content != null) return SelectContentReading(regionIndex, candidateId);
        if (!CanExport || State.Analysis == null || regionIndex < 0 || regionIndex >= State.Analysis.Regions.Count) return false;
        if (!State.Analysis.Regions[regionIndex].Candidates.Any(c => c.Id == candidateId)) return false;
        if (regionIndex == State.RegionIndex && candidateId == State.CandidateId) return true;
        Push(undo, State); redo.Clear(); Invalidate(); State = State with { RegionIndex = regionIndex, CandidateId = candidateId }; Changed?.Invoke(); return true;
    }

    public bool Undo() => RestoreHistory(undo, redo);
    public bool Redo() => RestoreHistory(redo, undo);
    private bool RestoreHistory(List<FormulaState> from, List<FormulaState> to)
    {
        if (disposed || IsComposing || from.Count == 0) return false;
        var restored = from[^1]; from.RemoveAt(from.Count - 1); Push(to, State);
        var leaving = State.Transformations?.Entries.LastOrDefault();var returning = restored.Transformations?.Entries.LastOrDefault();
        EditorSelection? selection = leaving?.Before.Raw == restored.Raw && leaving.Before.SourceRevision == restored.SourceRevision ? leaving.SelectionBefore :
            returning?.After.Raw == restored.Raw && returning.After.Revision == restored.SourceRevision ? returning.SelectionAfter : null;
        ClearAutoBalanceInput(); Invalidate(); State = restored; reconciliation = null; HistorySelection=selection; Changed?.Invoke(); return true;
    }

    public ExportLease Lease() => CanExport ? new(Id, Version, State.CandidateId!) : throw new InvalidOperationException("Chưa có công thức hiện tại để xuất.");
    public bool IsCurrent(ExportLease lease) => CanExport && lease.SessionId == Id && lease.Version == Version && lease.CandidateId == State.CandidateId;
    public Candidate Require(ExportLease lease) => IsCurrent(lease) ? State.Candidate! : throw new InvalidOperationException("Nguồn hoặc lựa chọn đã đổi. Hãy xuất lại từ kết quả hiện tại.");

    // Validate imported state before touching this session. Unsupported files never reach this method.
    public void Load(FormulaState state)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsComposing) throw new InvalidOperationException("Hoàn tất ghép dấu trước khi mở tài liệu.");
        if (state.Analysis != null && (state.Analysis.Source.Raw != state.Raw || state.Analysis.Source.Revision != state.SourceRevision)) throw new FormatException("Source mismatch.");
        if (state.CandidateId != null && state.Candidate == null) throw new FormatException("Unknown candidate.");
        DocumentCodec.ValidateFormula(state, new());
        ClearAutoBalanceInput(); undo.Clear(); redo.Clear(); Invalidate(); State = state; reconciliation = null; sourceCounter = Math.Max(sourceCounter, state.SourceRevision); Changed?.Invoke();
    }
    // Detaching a view cancels work without discarding a completed result or the session history.
    public void DetachView()
    {
        if (disposed) return;
        if (IsComposing) UpdateSource(State.Raw, false);
        ClearAutoBalanceInput(); Invalidate(); Changed?.Invoke();
    }
    private bool Current(long version, CancellationTokenSource cancellation) => !disposed && !IsComposing && version == Version && !cancellation.IsCancellationRequested;
    // Imported Int64 revisions may be near the limit; the session epoch still invalidates every older operation.
    private long NextSourceRevision() => sourceCounter = sourceCounter >= long.MaxValue - 1 ? 0 : sourceCounter + 1;
    private void Invalidate() { Version++; Error = ""; IsBusy = false; HistorySelection=null; DismissAssistance(); InvalidateBalance(); var old = pending; pending = null; try { old?.Cancel(); } catch (ObjectDisposedException) { } }
    private static void Push(List<FormulaState> history, FormulaState state) { history.Add(state); if (history.Count > HistoryLimit) history.RemoveAt(0); }
    public void Dispose() { if (disposed) return; disposed = true; Invalidate(); undo.Clear(); redo.Clear(); compositionStart = null; Changed = null; AssistanceChanged=null; }
}
