using System.Text;
using Locus.Application;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Locus.Editor;

public partial class Workspace
{
    private IJSObjectReference? local;
    private ClipboardCapabilities capabilities = new(false, false, false, false);
    private CancellationTokenSource? persistDelay;
    private readonly CancellationTokenSource lifetime = new();
    private DraftItem[] drafts = [];
    private bool contextMenu, updateAvailable;
    private string appStatus = "Đang chuẩn bị bản offline…";
    private string? fallbackFormat;
    private sealed record DraftItem(string Id, string Label);
    private sealed record LocalStartup(string Id, string? Preferences, string? Document, string? Raw, string Error, DraftItem[] Drafts);
    private sealed record DraftRead(string? Document, string? Raw);
    private sealed record DraftSaved(bool Saved, string Message);

    private async Task InitializeLocal()
    {
        local = await Js.InvokeAsync<IJSObjectReference>("import", "./_content/Locus.Editor/local-state.js");
        try
        {
            var state = await local.InvokeAsync<LocalStartup>("start", Host);
            drafts = state.Drafts;
            if (!Model.Initialized)
            {
                if (EditorPreferences.Read(state.Preferences) is { } preferences)
                {
                    session.Configure(preferences.Settings); view = preferences.View; Model.AutoSave = preferences.AutoSave;Model.AcceptChemistrySpace=preferences.AcceptChemistrySpace??false;session.SetAutoBalance(preferences.AutoBalance??false);
                }
                if (state.Document != null)
                {
                    if (DocumentCodec.Open(state.Document).Document is FormulaDocument document) Model.Open(document);
                    else
                    {
                        retainedFile = Encoding.UTF8.GetBytes(state.Document); retainedName = "nhap-chua-ho-tro.locus";
                        notice = "Nháp cũ chưa mở được. Đã giữ bản gốc để tải lại; nội dung mới dùng nháp riêng.";
                        await local.InvokeVoidAsync("fork");
                    }
                }
                if (state.Raw != null && state.Raw != session.State.Raw) session.UpdateSource(state.Raw);
                Model.DraftStatus = state.Error.Length > 0 ? state.Error : state.Document != null || state.Raw != null ? "Đã phục hồi nháp trên thiết bị." : "Nháp lưu riêng cho tab này.";
                Model.Initialized = true;
            }
            UpgradeMarkers();
            await SavePreferences();
            await module!.InvokeVoidAsync("setSource", sourceElement, session.State.Raw);
        }
        catch (JSException) { Model.Initialized = true; Model.DraftStatus = "Bộ nhớ nháp không khả dụng. Hãy lưu tệp .locus."; UpgradeMarkers(); }
        try { capabilities = await Clipboard.CapabilitiesAsync(); } catch (JSException) { }
        if (Host == "browser") _ = PollAppStatus();
    }

    private void UpgradeMarkers()
    {
        try { var message = session.UpgradeMarkerProfiles(); if (message.Length > 0) notice = message; }
        catch (ArgumentException ex) { notice = ex.Message; }
    }

    private void QueuePersist()
    {
        if (!Model.Initialized || disposed) return;
        persistDelay?.Cancel(); persistDelay?.Dispose(); persistDelay = new();
        _ = PersistAfterPause(persistDelay.Token);
    }
    private async Task PersistAfterPause(CancellationToken token)
    {
        try { await Task.Delay(250, token); if (!token.IsCancellationRequested && !disposed) await Persist(); }
        catch (OperationCanceledException) { }
    }
    private async Task<bool> SavePreferences() => local != null && await local.InvokeAsync<bool>("configure", Model.AutoSave, new EditorPreferences(5, session.State.Settings, view, Model.AutoSave,Model.AcceptChemistrySpace,session.AutoBalance).Serialize());
    private async Task<bool> Persist(bool force = false)
    {
        if (!Model.Initialized || disposed || local == null || session.IsComposing || session.IsBusy) return false;
        var version = session.Version; var options = view;
        try
        {
            var json = Model.SerializeDocument(); var raw = session.State.Raw;
            bool preferencesSaved = await SavePreferences();
            var result = await local.InvokeAsync<DraftSaved>("save", json, raw, force);
            if (!disposed && version == session.Version && options == view)
            {
                Model.DraftStatus = result.Message + (preferencesSaved ? "" : " Chưa lưu được tùy chọn.");
                StateHasChanged();
            }
            return result.Saved && version == session.Version && options == view;
        }
        catch (Exception e) when (e is JSException or FormatException)
        {
            Model.DraftStatus = "Chưa lưu được nháp đầy đủ. Giữ lại nguồn hoặc tải tệp trước khi đóng.";
            if (!disposed) StateHasChanged(); return false;
        }
    }
    private async Task AutoSaveChanged(ChangeEventArgs e)
    {
        Model.AutoSave = e.Value is true; await SavePreferences();
        await local!.InvokeVoidAsync("journal", session.State.Raw); await Persist();
    }
    private async Task RefreshDrafts()
    {
        if (local == null) return;
        try { drafts = await local.InvokeAsync<DraftItem[]>("list"); } catch (JSException) { }
    }
    private async Task OpenDraft(string id)
    {
        if (session.IsBusy || session.IsComposing || local == null) return;
        var version = session.Version;
        try
        {
            var draft = await local.InvokeAsync<DraftRead>("read", id);
            if (version != session.Version || draft.Document == null) return;
            if (DocumentCodec.Open(draft.Document).Document is not FormulaDocument document)
            {
                retainedFile = Encoding.UTF8.GetBytes(draft.Document); retainedName = "nhap-chua-ho-tro.locus";
                notice = "Nháp chưa được hỗ trợ; bạn có thể tải nguyên tệp. Phiên hiện tại vẫn được giữ."; return;
            }
            await Persist(); if (version != session.Version || session.IsComposing) return;
            await local.InvokeVoidAsync("fork"); // A different live tab may still own the selected draft.
            Model.Open(document);
            notice = "Đã mở nháp thành một bản riêng trong tab này.";
            UpgradeMarkers();
            if (draft.Raw != null && draft.Raw != session.State.Raw) session.UpdateSource(draft.Raw);
            await module!.InvokeVoidAsync("setSource", sourceElement, session.State.Raw);
            ClearResultSelection();if (!session.State.HasResult) await AnalyzeAndRender(); else await RenderCurrent();
        }
        catch (JSException) { notice = "Chưa đọc được nháp; nguồn hiện tại vẫn được giữ."; }
    }
    [JSInvokable] public async Task Command(string command)
    {
        if (!ready || disposed || session.IsComposing) return;
        switch (command)
        {
            case "undo": await History(false); break;
            case "redo": await History(true); break;
            case "analyze": debounce?.Cancel(); await AnalyzeAndRender(); break;
            case "save": await SaveDocument(); break;
            case "dismiss": contextMenu = false; alternatives = false;ghostDismissedVersion=session.Version;preparedGhost=null; session.DismissAssistance(); StateHasChanged(); break;
            default:
                if (command.StartsWith("candidate") && int.TryParse(command[9..], out var index) && !session.IsBusy && session.State.Region is {} region && index >= 1 && index <= region.Candidates.Count)
                    await Choose(session.State.Region.Candidates[index - 1].Id);
                break;
        }
    }
    private async Task PollAppStatus()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                appStatus = await module!.InvokeAsync<string>("appStatus");
                updateAvailable = await module!.InvokeAsync<bool>("updateAvailable");
                if (!disposed) StateHasChanged(); await Task.Delay(2000, lifetime.Token);
            }
        }
        catch (OperationCanceledException) { } catch (JSException) { }
    }
    private async Task UpdateApp()
    {
        var version = session.Version;
        if (await Persist(true) && version == session.Version && !session.IsComposing)
        {
            try { await module!.InvokeVoidAsync("applyUpdate"); } catch (JSException) { notice = "Nguồn vừa thay đổi. Hãy cập nhật sau khi gõ xong."; }
        }
        else notice = "Chưa lưu được nháp, nên chưa cập nhật. Hãy tải tệp .locus trước.";
    }
}
