using Locus.Application;
using Locus.Core.Assistance;
using Locus.Core.Export;
using Microsoft.AspNetCore.Components;

namespace Locus.Editor;

public partial class Workspace
{
    private bool showDropProducts;
    private void BalanceChanged(){if(!disposed)_=InvokeAsync(StateHasChanged);}
    private async Task RefreshWand()
    {
        session.SetBalanceSelection(resultSelection);
        await session.RefreshBalanceAsync();
    }
    private async Task BalanceStep(){await session.BalanceStepAsync();await RenderCurrent();QueuePersist();}
    private async Task BalanceBatch(bool cancel){await session.BalanceBatchAsync(cancel);await RenderCurrent();QueuePersist();}
    private async Task AutoBalanceChanged(ChangeEventArgs args)
    {
        session.SetAutoBalance(args.Value is true);await SavePreferences();await RefreshWand();
        notice=session.AutoBalance?"Tự cân bằng cho lần nhập hoặc dán tiếp theo; không tự nhận sản phẩm.":"Đã tắt tự cân bằng; kết quả đang có được giữ.";
    }
    private async Task DropProducts()
    {
        if(ActiveContentRegion is {} region&&session.DropContentProducts(region.Id)){showDropProducts=false;await RenderCurrent();alternatives=true;QueuePersist();}
    }
    private static string BeforeBalanceMathMl(ContentRegion region)=>region.ResultOverride?.BalanceBefore?.Candidate is {} before?CandidateExporter.ToMathMl(before):region.Selected is {} candidate?CandidateExporter.ToMathMl(candidate):"";
}
