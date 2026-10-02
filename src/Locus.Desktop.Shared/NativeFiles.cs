using System.IO;
using Locus.Application;
using Microsoft.Win32;

namespace Locus.Desktop.Shared;

public sealed class NativeFiles:IEditorFiles
{
    public Task<TransferResult> SaveAsync(string name,string mediaType,byte[] content,Func<bool> isCurrent,CancellationToken token=default)=>SaveAsAsync(name,mediaType,content,isCurrent,token);
    public async Task<TransferResult> SaveAsAsync(string name,string mediaType,byte[] content,Func<bool> isCurrent,CancellationToken token=default)
    {
        if(token.IsCancellationRequested||!isCurrent())return new(TransferStatus.Cancelled);
        try
        {
            return await System.Windows.Application.Current.Dispatcher.InvokeAsync(()=>
            {
                if(token.IsCancellationRequested||!isCurrent())return new TransferResult(TransferStatus.Cancelled);
                string extension=Path.GetExtension(name);
                var dialog=new SaveFileDialog{FileName=Path.GetFileName(name),DefaultExt=extension,AddExtension=true,OverwritePrompt=true,Filter=$"Tệp {extension}|*{extension}|Tất cả tệp|*.*"};
                if(dialog.ShowDialog(System.Windows.Application.Current.MainWindow)!=true||token.IsCancellationRequested||!isCurrent())return new TransferResult(TransferStatus.Cancelled);
                // Write beside the destination, then atomically replace only after the last source/selection check.
                string target=Path.GetFullPath(dialog.FileName),temporary=target+".locus-"+Guid.NewGuid().ToString("N")+".tmp";
                try
                {
                    File.WriteAllBytes(temporary,content);
                    if(token.IsCancellationRequested||!isCurrent())return new TransferResult(TransferStatus.Cancelled);
                    File.Move(temporary,target,true);return new TransferResult(TransferStatus.Completed);
                }
                finally{if(File.Exists(temporary))File.Delete(temporary);}
            });
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Runtime.InteropServices.COMException){return new(TransferStatus.Unavailable);}
    }
}
