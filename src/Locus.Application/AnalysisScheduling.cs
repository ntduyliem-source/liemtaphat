using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Serialization;
using System.Text.Json;

namespace Locus.Application;

public sealed record FormulaSettings(InputMode Mode = InputMode.Explicit, string Open = "lc[", string Close = "]", DetectionDomains EnabledDomains = DetectionDomains.Math,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] MarkerPreferences? MarkerProfiles = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasValidDomains => (EnabledDomains & ~DetectionDomains.All) == 0;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasValidMarkerProfiles
    {
        get { try { return MarkerProfiles == null || MarkerProfiles.ToProfiles(Open, Close).IsValid; } catch (ArgumentException) { return false; } }
    }
    public AnalysisOptions ToOptions() => new(Mode, markers: new(Open, Close), maxSourceLength: FormulaSession.MaxSourceLength, maxRegions: 32,
        enabledDomains: EnabledDomains, markerProfiles: MarkerProfiles?.ToProfiles(Open, Close));
}

public sealed record AnalysisRequest(string Raw, long SourceRevision, FormulaSettings Settings);

/// <summary>The host owns scheduling; a session still discards outdated replies even if cancellation is ignored.</summary>
public interface IAnalysisScheduler
{
    Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken);
}
public interface IChemistryAssistanceScheduler
{
    Task<ChemistryAssistanceResponse> AssistAsync(ChemistryAssistanceRequest request,CancellationToken cancellationToken);
}

public sealed class NativeAnalysisScheduler : IAnalysisScheduler, IChemistryAssistanceScheduler, IContentBalanceScheduler, IPlotScheduler
{
    public async Task<PlotResult> PlotAsync(PlotRequest request, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(5));
        return await Task.Run(() => PlotWire.Analyze(request, deadline.Token), deadline.Token).WaitAsync(deadline.Token);
    }
    public async Task<ContentBalanceResponse> BalanceAsync(ContentBalanceRequest request,CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(5));
        return await Task.Run(()=>ContentBalanceWire.Analyze(request,deadline.Token),deadline.Token).WaitAsync(deadline.Token);
    }
    public async Task<ChemistryAssistanceResponse> AssistAsync(ChemistryAssistanceRequest request,CancellationToken cancellationToken)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try{return await Task.Run(()=>ChemistryAssistanceWire.Analyze(request,deadline.Token),deadline.Token).WaitAsync(deadline.Token);}
        catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){throw new TimeoutException("Assistance deadline.");}
    }
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        // This adapter is registered only by native hosts, never by the WASM UI.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        var work = Task.Run(() => AnalysisWire.Analyze(request, deadline.Token), deadline.Token);
        try { return await work.WaitAsync(deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new TimeoutException("Analysis deadline."); }
    }
}

/// <summary>Worker messages preserve core snapshots rather than round-tripping ASTs through JavaScript numbers.</summary>
public static class AnalysisWire
{
    private sealed record DiagnosticData(string Code, string Severity, int Start, int End, string Message);
    private sealed record Response(string Raw, long Revision, DetectionStatus Detection, bool Incomplete, string[] Regions, DiagnosticData[] Diagnostics);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };
    public static AnalysisResult Analyze(AnalysisRequest request, CancellationToken token = default)
    {
        if (request.Raw == null || request.Settings == null || !Enum.IsDefined(request.Settings.Mode) || request.Raw.Length > FormulaSession.MaxSourceLength) throw new ArgumentException("Invalid analysis request.");
        return new AnalysisEngine().Analyze(new SourceSnapshot(request.Raw, request.SourceRevision), request.Settings.ToOptions(), token);
    }
    public static string Run(string json)
    {
        using var envelope=JsonDocument.Parse(json);
        if(envelope.RootElement.TryGetProperty("Operation",out var operation))return operation.GetString()==PlotWire.Operation?PlotWire.Run(json):operation.GetString()==ContentBalanceWire.Operation?ContentBalanceWire.Run(json):ChemistryAssistanceWire.Run(json);
        return Serialize(Analyze(JsonSerializer.Deserialize<AnalysisRequest>(json, JsonOptions) ?? throw new FormatException("Missing request.")));
    }
    public static string Request(AnalysisRequest request) => JsonSerializer.Serialize(request, JsonOptions);
    public static string Serialize(AnalysisResult result) => JsonSerializer.Serialize(new Response(result.Source.Raw, result.Source.Revision,
        result.Detection, result.IsIncomplete, result.Regions.Select(CandidateSetSerializer.Serialize).ToArray(),
        result.Diagnostics.Select(d => new DiagnosticData(d.Code, d.Severity, d.Span.Start, d.Span.End, d.Message)).ToArray()), JsonOptions);
    public static AnalysisResult Deserialize(string json)
    {
        if (json.Length > 8_000_000) throw new FormatException("Worker result too large.");
        var data = JsonSerializer.Deserialize<Response>(json, JsonOptions) ?? throw new FormatException("Missing result.");
        if (data.Raw == null || data.Regions == null || data.Diagnostics == null || data.Raw.Length > FormulaSession.MaxSourceLength || data.Regions.Length > 32 || data.Diagnostics.Length > 4096 || data.Diagnostics.Any(d => d == null) || !Enum.IsDefined(data.Detection))
            throw new FormatException("Invalid worker result.");
        return new AnalysisResult(new SourceSnapshot(data.Raw, data.Revision), data.Detection, data.Regions.Select(CandidateSetSerializer.Deserialize),
            data.Diagnostics.Select(d => new Diagnostic(d.Code, d.Severity, new TextSpan(d.Start, d.End), d.Message)), data.Incomplete);
    }
}
