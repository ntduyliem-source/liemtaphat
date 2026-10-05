using Locus.Application;
using Locus.Core.Assistance;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private void BalanceChanged(){if(!disposed)_=InvokeAsync(StateHasChanged);}
    private Task RefreshWand()
    {
        session.SetBalanceSelection(resultSelection);
        return Task.CompletedTask;
    }
    private async Task BalanceStep()
    {
        if(module==null||!ready)return;
        await module.InvokeVoidAsync("settleInput",sourceElement);
        if(session.IsComposing||session.IsBalancing)return;
        debounce?.Cancel();
        if(!session.State.HasResult)await AnalyzeAndRender();
        if(!CanStudioBalance)return;
        var selectionTicket=selectionEpoch;
        var command=session.StudioCommand(StudioHasBalance?StudioChemistryAction.CancelBalance:StudioChemistryAction.Balance,StudioTargetIds);
        await session.RunStudioChemistryAsync(command,async()=>
        {
            var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
            return selectionTicket==selectionEpoch&&!stamp.Composing&&!stamp.Pending&&stamp.Raw==session.State.Raw;
        });
        await RenderCurrent();QueuePersist();
    }
    private async Task BalanceBatch(bool cancel){await session.BalanceBatchAsync(cancel);await RenderCurrent();QueuePersist();}
    private async Task AutoBalanceChanged(ChangeEventArgs args)
    {
        session.SetAutoBalance(args.Value is true);await SavePreferences();await RefreshWand();
        notice=session.AutoBalance?"Tự cân bằng cho lần nhập hoặc dán tiếp theo; không tự nhận sản phẩm.":"Đã tắt tự cân bằng; kết quả đang có được giữ.";
    }
    private async Task DropProducts()
    {
        if(module==null)return;
        await module.InvokeVoidAsync("settleInput",sourceElement);
        if(!CanDropProducts||SelectedSingleRegion is not {} region)return;
        var ticket=selectionEpoch;
        await session.RunStudioChemistryAsync(session.StudioCommand(StudioChemistryAction.DropProducts,[region.Id]),async()=>
        {
            var stamp=await module.InvokeAsync<EditorInputStamp>("inputStamp",sourceElement);
            return ticket==selectionEpoch&&!stamp.Composing&&!stamp.Pending&&stamp.Raw==session.State.Raw;
        });
        await RenderCurrent();QueuePersist();
    }
}
