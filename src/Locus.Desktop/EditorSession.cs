using Locus.Core;
using Locus.Core.Detection;

namespace Locus.Desktop;

/// <summary>Owned by one editor UI thread. Generation checks also cover debounce and composition intervals.</summary>
public sealed class EditorSession : IDisposable
{
    private readonly LatestRequestGate<AnalysisResult> gate = new();
    private readonly Func<SourceSnapshot, AnalysisOptions, CancellationToken, Task<AnalysisResult>> analyze;
    private bool disposed;
    public long Revision { get; private set; }
    public string Raw { get; private set; } = "";
    public InputMode Mode { get; private set; }
    public MarkerConfiguration Markers { get; private set; } = MarkerConfiguration.Default;
    public bool IsComposing { get; private set; }
    public AnalysisResult? Result { get; private set; }
    public Candidate? SelectedCandidate { get; private set; }
    public bool CanExport => !disposed && !IsComposing && Result != null && SelectedCandidate != null && Result.Source.Revision == Revision;

    public EditorSession(Func<SourceSnapshot, AnalysisOptions, CancellationToken, Task<AnalysisResult>>? analyze = null)
    {
        this.analyze = analyze ?? ((source, options, token) => Task.Run(() => new AnalysisEngine().Analyze(source, options, token), token));
    }

    public void Update(string raw, InputMode mode, MarkerConfiguration markers)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Raw = raw; Mode = mode; Markers = markers; Invalidate();
    }
    private void Invalidate() { Revision++; gate.Invalidate(); Result = null; SelectedCandidate = null; }
    public void BeginComposition() { if (disposed) return; IsComposing = true; Invalidate(); }
    public void EndComposition() { if (disposed) return; IsComposing = false; Invalidate(); }

    public async Task<bool> AnalyzeAsync()
    {
        if (disposed || IsComposing) return false;
        long revision = Revision;
        var source = new SourceSnapshot(Raw, revision);
        var options = new AnalysisOptions(Mode, markers: Markers, maxRegions: 32);
        var result = await gate.RunAsync(token => analyze(source, options, token));
        if (disposed || IsComposing || Revision != revision || !result.IsCurrent || result.Value == null) return false;
        Result = result.Value;
        SelectedCandidate = Result.Regions.FirstOrDefault()?.Candidates.FirstOrDefault(c => c.Kind == "direct");
        return true;
    }

    public bool Select(string id)
    {
        if (disposed || IsComposing || Result == null || Result.Source.Revision != Revision) return false;
        var candidate = Result.Regions.SelectMany(r => r.Candidates).FirstOrDefault(c => c.Id == id);
        if (candidate == null) return false;
        SelectedCandidate = candidate; return true;
    }

    public void ClearSelection() => SelectedCandidate = null;

    public Candidate RequireCurrent(string id, long revision)
    {
        if (!CanExport || Revision != revision || SelectedCandidate!.Id != id)
            throw new InvalidOperationException("Nội dung đã đổi. Hãy chọn lại kết quả hiện tại.");
        return SelectedCandidate;
    }

    public void Dispose() { if (disposed) return; disposed = true; gate.Dispose(); Result = null; SelectedCandidate = null; }
}
