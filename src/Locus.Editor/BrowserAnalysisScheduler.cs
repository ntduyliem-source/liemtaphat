using Locus.Application;
using Locus.Core.Detection;
using Microsoft.JSInterop;

namespace Locus.Editor;

public sealed class BrowserAnalysisScheduler(IJSRuntime js) : IAnalysisScheduler, IChemistryAssistanceScheduler, IContentBalanceScheduler, IPlotScheduler, IAsyncDisposable
{
    public async Task<PlotResult> PlotAsync(PlotRequest request, CancellationToken token)
    {
        await InitializeAsync(); token.ThrowIfCancellationRequested();
        var id = (++counter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var registration = token.Register(() => _ = CancelAsync(id));
        var result = await manager!.InvokeAsync<string>("run", token, id, PlotWire.Request(request));
        token.ThrowIfCancellationRequested(); return PlotWire.Deserialize(result);
    }
    private IJSObjectReference? module, manager;
    private Task? initialization;
    private long counter;
    public Task InitializeAsync() => initialization ??= InitializeCoreAsync();
    private async Task InitializeCoreAsync()
    {
        module = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Locus.Editor/worker-client.js");
        manager = await module.InvokeAsync<IJSObjectReference>("create", "./worker/worker.js");
    }
    public async Task<AnalysisResult> AnalyzeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        await InitializeAsync(); cancellationToken.ThrowIfCancellationRequested();
        var id = (++counter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var registration = cancellationToken.Register(() => _ = CancelAsync(id));
        try
        {
            string result = await manager!.InvokeAsync<string>("run", cancellationToken, id, AnalysisWire.Request(request));
            cancellationToken.ThrowIfCancellationRequested();
            return AnalysisWire.Deserialize(result);
        }
        catch (JSException ex) when (ex.Message.Contains("TIMEOUT", StringComparison.Ordinal)) { throw new TimeoutException("Worker deadline.", ex); }
    }
    private async Task CancelAsync(string id) { try { if (manager != null) await manager.InvokeVoidAsync("cancel", id); } catch (JSException) { } catch (ObjectDisposedException) { } }
    public async Task<ChemistryAssistanceResponse> AssistAsync(ChemistryAssistanceRequest request,CancellationToken cancellationToken)
    {
        await InitializeAsync();cancellationToken.ThrowIfCancellationRequested();
        var id=(++counter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var registration=cancellationToken.Register(()=>_=CancelAsync(id));
        try
        {
            string result=await manager!.InvokeAsync<string>("run",cancellationToken,id,ChemistryAssistanceWire.Request(request));cancellationToken.ThrowIfCancellationRequested();
            return ChemistryAssistanceWire.Deserialize(result);
        }
        catch(JSException ex)when(ex.Message.Contains("TIMEOUT",StringComparison.Ordinal)){throw new TimeoutException("Worker assistance deadline.",ex);}
    }
    public async ValueTask DisposeAsync()
    {
        if (manager != null) { try { await manager.InvokeVoidAsync("dispose"); await manager.DisposeAsync(); } catch (JSException) { } }
        if (module != null) { try { await module.DisposeAsync(); } catch (JSException) { } }
    }
    public async Task<ContentBalanceResponse> BalanceAsync(ContentBalanceRequest request,CancellationToken token)
    {
        await InitializeAsync();token.ThrowIfCancellationRequested();
        var id=(++counter).ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var registration=token.Register(()=>_=CancelAsync(id));
        var json=await manager!.InvokeAsync<string>("run",token,id,ContentBalanceWire.Request(request));token.ThrowIfCancellationRequested();
        return ContentBalanceWire.Deserialize(json);
    }
}
