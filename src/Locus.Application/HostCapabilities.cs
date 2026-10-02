namespace Locus.Application;

public enum TransferStatus { Completed, Cancelled, Unavailable, Failed }
public sealed record TransferResult(TransferStatus Status, string Message = "");
public sealed record ClipboardCapabilities(bool Text, bool Png, bool Svg, bool SavePicker);

public sealed record DesktopClosePreparation(bool Ready,bool ConfirmDiscard=false);
public interface IEditorWindow
{
    bool Expanded { get; }
    bool Visible { get; }
    event Action? Changed;
    Func<bool,Task<DesktopClosePreparation>>? PrepareClose { get; set; }
    Task ToggleSizeAsync();
    Task HideAsync();
    Task ExitAsync();
}

/// <summary>Adapters report cancellation/failure; the application never clears a session merely because a picker was opened.</summary>
public interface IEditorFiles
{
    Task<TransferResult> SaveAsync(string name, string mediaType, byte[] content, Func<bool> isCurrent, CancellationToken token = default);
    Task<TransferResult> SaveAsAsync(string name, string mediaType, byte[] content, Func<bool> isCurrent, CancellationToken token = default);
}

public interface IEditorClipboard
{
    Task<ClipboardCapabilities> CapabilitiesAsync();
    Task<TransferResult> WriteTextAsync(string text, Func<bool> isCurrent, CancellationToken token = default);
    Task<TransferResult> WritePngAsync(byte[] bytes, Func<bool> isCurrent, CancellationToken token = default);
    Task<TransferResult> WriteSvgAsync(string svg, Func<bool> isCurrent, CancellationToken token = default);
}
