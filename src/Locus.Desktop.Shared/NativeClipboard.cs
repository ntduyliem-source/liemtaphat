using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Locus.Application;

namespace Locus.Desktop.Shared;

/// <summary>PNG preserves alpha, plus a white CF_BITMAP fallback for Windows applications.</summary>
public sealed class NativeClipboard : IEditorClipboard
{
    public Task<ClipboardCapabilities> CapabilitiesAsync() => Task.FromResult(new ClipboardCapabilities(true, true, true, true));
    public Task<TransferResult> WriteSvgAsync(string svg,Func<bool> isCurrent,CancellationToken token=default) => Write(()=>{var data=new DataObject();data.SetData("image/svg+xml",new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg)),false);data.SetText(svg,TextDataFormat.UnicodeText);Clipboard.SetDataObject(data,true);},isCurrent,token);
    public Task<TransferResult> WriteTextAsync(string text,Func<bool> isCurrent,CancellationToken token=default) => Write(()=>Clipboard.SetText(text),isCurrent,token);
    public Task<TransferResult> WritePngAsync(byte[] bytes,Func<bool> isCurrent,CancellationToken token=default) => Write(()=>Clipboard.SetDataObject(CreatePngData(bytes),true),isCurrent,token);
    public static DataObject CreatePngData(byte[] bytes)
    {
        using var stream=new MemoryStream(bytes);
        var image=BitmapFrame.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,image.PixelWidth,image.PixelHeight));drawing.DrawImage(image,new Rect(0,0,image.PixelWidth,image.PixelHeight));}
        var white=new RenderTargetBitmap(image.PixelWidth,image.PixelHeight,96,96,PixelFormats.Pbgra32);white.Render(visual);white.Freeze();
        var data=new DataObject();data.SetData("PNG",new MemoryStream(bytes),false);data.SetImage(white);return data;
    }
    private static async Task<TransferResult> Write(Action action,Func<bool> isCurrent,CancellationToken token)
    {
        if(token.IsCancellationRequested)return new(TransferStatus.Cancelled);
        try{return await System.Windows.Application.Current.Dispatcher.InvokeAsync(()=>{if(token.IsCancellationRequested||!isCurrent())return new TransferResult(TransferStatus.Cancelled);action();return new TransferResult(TransferStatus.Completed);});}
        catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or IOException or ArgumentException){return new(TransferStatus.Unavailable);}
    }
}
