using Locus.Application;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    public async Task<bool> CanLeaveAsync()
    {
        if(module==null||disposed)return false;
        try
        {
            await module.InvokeVoidAsync("settleInput",sourceElement);
            var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
            if(stamp.Composing||stamp.Pending||session.IsComposing||!MatchesDesktopSource(stamp)){notice="Hoàn tất ghép dấu trước khi đổi tab.";StateHasChanged();return false;}
            return true;
        }
        catch(JSException){return false;}
    }
    private bool lastDesktopVisible=true;
    // A textarea exposes CR and CRLF as LF; compare its display value without rewriting imported raw text.
    private bool MatchesDesktopSource(EditorInputStamp stamp)=>stamp.Raw==session.State.Raw.Replace("\r\n","\n").Replace('\r','\n');
    private void DesktopWindowChanged()
    {
        if(disposed||DesktopWindow==null)return;
        bool resumed=DesktopWindow.Visible&&!lastDesktopVisible;lastDesktopVisible=DesktopWindow.Visible;
        _=InvokeAsync(async()=>
        {
            if(resumed&&!session.IsComposing)
            {
                if(!session.State.HasResult)await AnalyzeAndRender();
                else await RenderCurrent();
            }
            StateHasChanged();
        });
    }
    private async Task<DesktopClosePreparation> PrepareDesktopClose(bool exit)
    {
        DesktopClosePreparation result=new(false);
        await InvokeAsync(async()=>result=await PrepareDesktopCloseCore(exit));return result;
    }
    private async Task<DesktopClosePreparation> PrepareDesktopCloseCore(bool exit)
    {
        if(!ready||disposed||module==null)return new(false);
        try
        {
            await module.InvokeVoidAsync("settleInput",sourceElement);
            var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
            if(stamp.Composing||stamp.Pending||session.IsComposing||!MatchesDesktopSource(stamp))
            {notice="Hoàn tất ghép dấu trước khi ẩn hoặc thoát.";StateHasChanged();return new(false);}
            debounce?.Cancel();session.DetachView();
            if(!session.State.HasResult)await AnalyzeAndRender();else await RenderCurrent();
            long preparedVersion=session.Version;
            bool saved=!Model.AutoSave||await Persist();
            // Input can resume while persistence awaits JavaScript; never hide an active composition.
            await module.InvokeVoidAsync("settleInput",sourceElement);
            stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
            if(stamp.Composing||stamp.Pending||session.IsComposing||!MatchesDesktopSource(stamp)||preparedVersion!=session.Version)
            {notice="Nội dung đang thay đổi. Hoàn tất nhập trước khi ẩn hoặc thoát.";StateHasChanged();return new(false);}
            if(!saved)
            {notice="Chưa lưu được nháp đầy đủ. Hãy lưu tệp .locus trước khi thoát.";StateHasChanged();return new(!exit);}
            return new(true,exit&&!Model.AutoSave);
        }
        catch(Exception e)when(e is JSException or InvalidOperationException or FormatException){notice="Chưa hoàn tất lưu phiên. Cửa sổ vẫn được giữ.";StateHasChanged();return new(false);}
    }
}
