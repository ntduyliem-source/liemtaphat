using Locus.Application;
using Microsoft.JSInterop;

namespace Locus.Editor;

/// <summary>The file input remains a shared Blazor component; host side effects go through these ports.</summary>
public sealed class BrowserTransfers(IJSRuntime js) : IEditorFiles, IEditorClipboard, IAsyncDisposable
{
    private IJSObjectReference? module;
    private async Task<TransferResult> Send(string method, Func<bool> isCurrent, CancellationToken token, params object?[] values)
    {
        try
        {
            module ??= await js.InvokeAsync<IJSObjectReference>("import", token, "./_content/Locus.Editor/editor.js");
            if (!isCurrent()) return new(TransferStatus.Cancelled);
            await module.InvokeVoidAsync(method, token, values);
            return new(TransferStatus.Completed);
        }
        catch (OperationCanceledException) { return new(TransferStatus.Cancelled); }
        catch (JSException) { return new(TransferStatus.Unavailable); }
    }
    public Task<TransferResult> SaveAsync(string name, string mediaType, byte[] content, Func<bool> isCurrent, CancellationToken token = default) => Send("download", isCurrent, token, name, mediaType, content);
    public Task<TransferResult> WriteTextAsync(string text, Func<bool> isCurrent, CancellationToken token = default) => Send("clipboardText", isCurrent, token, text);
    public Task<TransferResult> WritePngAsync(byte[] bytes, Func<bool> isCurrent, CancellationToken token = default) => Send("clipboardPng", isCurrent, token, bytes);
    public async Task<ClipboardCapabilities> CapabilitiesAsync()
    {
        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/Locus.Editor/editor.js");
        return await module.InvokeAsync<ClipboardCapabilities>("capabilities");
    }
    public async Task<TransferResult> WriteSvgAsync(string svg, Func<bool> isCurrent, CancellationToken token = default)
    {
        var capabilities = await CapabilitiesAsync();
        var result = await Send(capabilities.Svg ? "clipboardSvg" : "clipboardText", isCurrent, token, svg);
        return result.Status == TransferStatus.Completed && !capabilities.Svg ? result with { Message = "Đã sao chép mã SVG. Dùng Tải SVG để lấy tệp ảnh vector." } : result;
    }
    public async Task<TransferResult> SaveAsAsync(string name, string mediaType, byte[] content, Func<bool> isCurrent, CancellationToken token = default)
    {
        try
        {
            module ??= await js.InvokeAsync<IJSObjectReference>("import", token, "./_content/Locus.Editor/editor.js");
            if (!isCurrent()) return new(TransferStatus.Cancelled);
            return await module.InvokeAsync<TransferResult>("saveAs", token, name, mediaType, content);
        }
        catch (OperationCanceledException) { return new(TransferStatus.Cancelled); }
        catch (JSException) { return new(TransferStatus.Failed); }
    }
    public async ValueTask DisposeAsync() { if (module != null) try { await module.DisposeAsync(); } catch (JSException) { } }
}
