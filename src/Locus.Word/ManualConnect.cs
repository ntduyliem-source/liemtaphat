using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Locus.Core;
using Locus.Core.Detection;
using Office = Microsoft.Office.Core;
using WordApi = Microsoft.Office.Interop.Word;

namespace Locus.Word;

// Separate COM entry point: no research actions, passive source observer, auto mode or diagnostic pipe.
[ComVisible(true), Guid("B118E51E-D934-4805-858B-784B22816CDD"), ProgId("Locus.Word.Manual"), ClassInterface(ClassInterfaceType.AutoDual)]
public sealed partial class ManualConnect : Extensibility.IDTExtensibility2, Office.IRibbonExtensibility
{
    private WordApi.Application? app;
    private Control? dispatcher;
    private NativeInputState? input;
    private Timer? timer;
    private ManualSession? session;
    private ManualPanel? panel;
    private WordDetectionSettings settings = WordDetectionSettings.Default;
    private MarkerConfiguration markers => settings.Common;
    private DetectionDomains domains => settings.Domains;
    private ManagedSnapshot? pendingSnapshot;
    private string pendingIdentity = "";
    private string lastFailure = "";
    private bool busy, disposed;
    private string pendingAction = "", pendingCandidate = "", message = "Sẵn sàng. Chọn vùng văn bản rồi mở Locus.";
    private DateTime pendingAt, inputAtRequest;
    private readonly string hostId = Guid.NewGuid().ToString("N");
    private readonly JavaScriptSerializer json = new() { MaxJsonLength = 1024 * 1024 };
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Locus", "word-manual-settings.json");

    public void OnConnection(object application, Extensibility.ext_ConnectMode connectMode, object addIn, ref Array custom)
    {
        app = (WordApi.Application)application;
        dispatcher = new Control(); dispatcher.CreateControl(); _ = dispatcher.Handle;
        input = new NativeInputState(); ((Office.COMAddIn)addIn).Object = this;
        try
        {
            if (File.Exists(SettingsPath))
            {
                settings = WordDetectionSettings.Deserialize(File.ReadAllText(SettingsPath));
            }
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidOperationException || error is FormatException)
        { message = "Không đọc được cài đặt cặp bọc; đang dùng lc[...]."; }
        app.WindowSelectionChange += SelectionChanged;
        app.WindowActivate += WindowActivated;
        app.DocumentBeforeClose += BeforeClose;
        timer = new Timer { Interval = 150 }; timer.Tick += (_, _) => Tick(); timer.Start();
    }
    public string GetCustomUI(string ribbonId) =>
        "<customUI xmlns=\"http://schemas.microsoft.com/office/2009/07/customui\"><ribbon><tabs><tab id=\"locusManual\" label=\"Locus\" keytip=\"LC\"><group id=\"locusFormula\" label=\"Công thức\"><button id=\"locusPreview\" label=\"Chuyển vùng chọn\" size=\"large\" imageMso=\"EquationInsertNew\" keytip=\"C\" onAction=\"PreviewClick\" screentip=\"Xem trước công thức từ vùng đã chọn\"/><button id=\"locusManaged\" label=\"Mở công thức Locus\" keytip=\"M\" onAction=\"ManagedClick\"/><button id=\"locusMarkers\" label=\"Cặp bọc\" keytip=\"B\" onAction=\"MarkersClick\"/></group><group id=\"locusScan\" label=\"Tài liệu có sẵn\"><button id=\"scanSelection\" label=\"Quét vùng chọn\" onAction=\"ScanSelectionClick\" keytip=\"V\"/><button id=\"scanBody\" label=\"Quét thân tài liệu\" onAction=\"ScanBodyClick\" keytip=\"Q\"/></group></tab></tabs></ribbon></customUI>";
    public void PreviewClick(Office.IRibbonControl control) => Open("source", true);
    public void ManagedClick(Office.IRibbonControl control) => Open("managed", true);
    public void MarkersClick(Office.IRibbonControl control)
    {
        CancelPreview(); ClearScan();
        using var form = new WordSettingsForm(settings, SaveSettings);
        form.ShowDialog(new WordOwner((IntPtr)app!.ActiveWindow.Hwnd));
    }
    public string ConfigureMarkers(string open, string close)
        => ConfigureDetection(open,close,(int)domains);
    public string ConfigureDetection(string open, string close,int enabledDomains)
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(() => ConfigureDetection(open, close,enabledDomains)));
        SaveSettings(settings.WithCommon(open, close, (DetectionDomains)enabledDomains)); return GetManualState();
    }
    public string ConfigureProfiles(string configuration)
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(() => ConfigureProfiles(configuration)));
        SaveSettings(WordDetectionSettings.Deserialize(configuration)); return GetManualState();
    }
    private void SaveSettings(WordDetectionSettings proposed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        string temporary = SettingsPath + ".tmp";
        File.WriteAllText(temporary, proposed.Serialize());
        if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null); else File.Move(temporary, SettingsPath);
        settings = proposed; Invalidate("settings-changed"); InvalidateScan("Cặp bọc hoặc checkbox đã đổi. Quét lại để dùng cài đặt mới.");
    }
    private bool OnOtherThread => dispatcher != null && !dispatcher.IsDisposed && dispatcher.InvokeRequired;
    public string OpenSelectionPreview() { if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(OpenSelectionPreview)); Open("source", false); return GetManualState(); }
    public string OpenManagedFormula() { if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(OpenManagedFormula)); Open("managed", false); return GetManualState(); }
    private void Open(string mode, bool fromRibbon)
    {
        if (disposed || app == null) return;
        CancelPreview(); ClearScan(); busy = true;
        try
        {
            if (app.Documents.Count == 0) throw new InvalidOperationException("select-source");
            IntPtr window = (IntPtr)app.ActiveWindow.Hwnd;
            // Observe focus before querying Selection: some Word queries can refocus the editor.
            var observation = input!.Read(window);
            if (!(fromRibbon ? NativeInputState.FocusWithin(window) : observation.EditorFocus) || observation.MessageComposition)
                throw new InvalidOperationException("outside-editor");
            session = mode == "source" ? ManualSession.OpenSource(app, settings) : ManualSession.OpenManaged(app, settings);
            panel = new ManualPanel(); panel.Present(session.Formula, session.Mode, session.Id, settings);
            panel.Requested += QueueAction; panel.Cancelled += CancelPreview;
            panel.Show(new WordOwner(window)); panel.Activate();
            message = "preview";
        }
        catch (Exception error) { Fail(error, true); }
        finally { busy = false; }
    }
    public string ChooseCandidate(string expectedSession, string candidateId)
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(() => ChooseCandidate(expectedSession, candidateId)));
        if (session == null || session.Id != expectedSession || pendingAction.Length != 0) throw new InvalidOperationException("preview-expired");
        session.Validate(app!, settings); panel!.SelectCandidate(candidateId); return GetManualState();
    }
    public string ChooseAssistance(string expectedSession, string expectedPreview, string action, string argument)
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(() => ChooseAssistance(expectedSession, expectedPreview, action, argument)));
        if (session == null || panel == null || session.Id != expectedSession || pendingAction.Length != 0 || panel.PreviewIdentity != expectedPreview)
            throw new InvalidOperationException("preview-expired");
        session.Validate(app!, settings); panel.AssistanceAction(action, argument); return GetManualState();
    }
    public string ConfirmPreview(string expectedSession, string action, string candidateId)
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(() => ConfirmPreview(expectedSession, action, candidateId)));
        QueueAction(expectedSession, action, candidateId); return GetManualState();
    }
    private void QueueAction(string expectedSession, string action, string candidateId)
    {
        busy = true;
        try
        {
            if (session == null || panel == null || session.Id != expectedSession || pendingAction.Length != 0) throw new InvalidOperationException("preview-expired");
            var entry = input!.Read((IntPtr)session.Window.Hwnd);
            if (!(NativeInputState.FocusWithin(panel.Handle) || entry.EditorFocus) || !entry.HookInstalled || entry.MessageComposition)
                throw new InvalidOperationException("outside-editor");
            session.Validate(app!, settings);
            if (panel.Formula == null || panel.PreviewIdentity != panel.Formula.Identity || candidateId != panel.CandidateId || candidateId != panel.PreviewCandidateId) throw new InvalidOperationException("candidate-changed");
            if (session.Mode == "source" ? action != "convert" : action != "restore" && action != "detach" && action != "replace") throw new InvalidOperationException("invalid-action");
            if ((action == "convert" || action == "replace") && string.IsNullOrEmpty(candidateId)) throw new InvalidOperationException("candidate-changed");
            pendingIdentity = panel.PreviewIdentity;
            pendingSnapshot = action == "convert" || action == "replace" ? new ManagedSnapshot(panel.Formula, session.Mode == "managed" ? session.EntryId : null) : null;
            _ = pendingSnapshot?.Encode();
            pendingAction = action; pendingCandidate = candidateId; message = "pending";
            panel.Hide(); session.Window.Activate(); session.Window.ActivePane.Activate();
            pendingAt = DateTime.UtcNow; inputAtRequest = input.LastInputUtc;
        }
        catch (Exception error) { Fail(error); }
        finally { busy = false; }
    }
    private void Tick()
    {
        if (!busy) TickScan();
        if (busy || session == null || disposed || app == null) return;
        busy = true;
        try
        {
            // Read input before COM target checks. Any new input after the click cancels the request.
            var observation = input!.Read((IntPtr)session.Window.Hwnd);
            session.Validate(app, settings);
            if (pendingAction.Length == 0) return;
            if (!observation.EditorFocus || observation.MessageComposition || input.LastInputUtc != inputAtRequest)
                throw new InvalidOperationException("input-or-focus-changed");
            if ((DateTime.UtcNow - pendingAt).TotalMilliseconds < 350) return;
            if (!observation.SafeExplicitTrigger) throw new InvalidOperationException("input-not-ready");
            string action = pendingAction; pendingAction = "";
            var current = session;
            void Revalidate()
            {
                if (!input.Read((IntPtr)current.Window.Hwnd).SafeExplicitTrigger || input.LastInputUtc != inputAtRequest)
                    throw new InvalidOperationException("input-or-focus-changed");
                current.Validate(app, settings);
                if (panel == null || panel.PreviewIdentity != pendingIdentity || panel.Formula?.Identity != pendingIdentity || panel.CandidateId != pendingCandidate || panel.PreviewCandidateId != pendingCandidate ||
                    pendingSnapshot != null && pendingSnapshot.State.Identity != pendingIdentity) throw new InvalidOperationException("candidate-changed");
            }
            Revalidate();
            if (action == "convert")
                WordOperations.ConvertSelected(app, pendingSnapshot!, Revalidate);
            else if (action == "replace") WordOperations.ReplaceManaged(app, current.FindControl(), pendingSnapshot!, Revalidate);
            else if (action == "restore") WordOperations.Restore(app, current.FindControl(), revalidate: Revalidate);
            else WordOperations.Detach(app, current.FindControl(), Revalidate);
            message = action == "convert" ? "converted" : action == "replace" ? "updated" : action == "restore" ? "restored" : "detached";
            ClearSession();
        }
        catch (Exception error) { Fail(error); }
        finally { busy = false; }
    }
    private void SelectionChanged(WordApi.Selection selection) { if (!busy) Tick(); }
    private void WindowActivated(WordApi.Document document, WordApi.Window window) { if (!busy) Tick(); }
    private void BeforeClose(WordApi.Document document, ref bool cancel)
    { if (session != null && ManualSession.Identity(session.Document) == ManualSession.Identity(document)) Invalidate("document-closing"); if (scan != null && ManualSession.Identity(scan.Document) == ManualSession.Identity(document)) ClearScan(); }
    private void Invalidate(string reason)
    { pendingAction = ""; pendingSnapshot = null; pendingIdentity = ""; session = null; message = reason; panel?.Report(Explain(new InvalidOperationException(reason)), true); }
    private void Fail(Exception error, bool show = false)
    {
        lastFailure = error.ToString();
        Invalidate(error is COMException ? "word-unavailable" : error.Message);
        if (show)
        {
            if (panel == null || panel.IsDisposed) panel = new ManualPanel();
            panel.Report(Explain(error), true);
            if (app != null && app.Documents.Count > 0) panel.Show(new WordOwner((IntPtr)app.ActiveWindow.Hwnd));
        }
        // Do not reactivate a panel after a stale/focus refusal; the user may be working elsewhere.
    }
    public void CancelPreview()
    {
        if (OnOtherThread) { dispatcher!.Invoke(new Action(CancelPreview)); return; }
        ClearSession(); message = "cancelled";
    }
    private void ClearSession()
    {
        pendingAction = ""; pendingSnapshot = null; pendingIdentity = ""; session = null;
        var previous = panel; panel = null;
        if (previous != null) { previous.Cancelled -= CancelPreview; previous.Close(); previous.Dispose(); }
    }
    public string GetManualState()
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(GetManualState));
        return json.Serialize(new { connected = app != null && !disposed, hostId, message, lastFailure, autoAllowed = false,
            sessionId = session?.Id, mode = session?.Mode, source = session?.Formula.OriginalSource,
            candidateId = panel?.CandidateId, previewCandidateId = panel?.PreviewCandidateId,
            candidates = session?.Candidates?.Candidates.Select(c => new { c.Id, c.Kind }).ToArray(),
            selectedCandidateId = panel?.Formula?.Readings?.SelectedCandidateId, panelVisible = panel?.Visible ?? false,
            previewIdentity = panel?.PreviewIdentity, canCancelBalance = panel?.Formula?.CanCancelBalance ?? false, hasProducts = panel?.Formula?.Products != null,
            keepFromAuto = panel?.Formula?.KeepFromAuto ?? false, proposals = panel?.Proposals.Select(p => new { p.Id, p.Kind }).ToArray(), configuration = settings.Serialize(),
            markerOpen = markers.Open, markerClose = markers.Close,enabledDomains=(int)domains,
            input = app != null && app.Documents.Count > 0 ? input?.Read((IntPtr)app.ActiveWindow.Hwnd) : null });
    }
    internal static string Explain(Exception error)
    {
        string key = error is COMException ? "word-unavailable" : error.Message;
        switch (key)
        {
            case "select-source": return "Chọn một vùng văn bản công thức trong thân tài liệu rồi mở Locus.";
            case "select-one-complete-formula": return "Vùng chọn chưa có một công thức đầy đủ được hỗ trợ. Chọn riêng công thức hoặc đúng một cặp bọc.";
            case "select-managed-formula": return "Đặt con trỏ bên trong một công thức do Locus tạo rồi mở lại.";
            case "read-only": case "protected": case "track-changes": case "managed-context-not-writable": return "Tài liệu/vùng này đang chỉ đọc, được bảo vệ hoặc bật Track Changes. Locus chưa hỗ trợ chuyển tại đây.";
            case "source-shape": case "non-main-story": case "table": case "existing-structure": case "existing-content-control": return "Bản này hỗ trợ văn bản một đoạn trong thân tài liệu, ngoài bảng, field và cấu trúc có sẵn.";
            case "invalid-markers": return "Hai dấu phải khác nhau, không rỗng, không là tiền tố của nhau và không chứa xuống dòng hoặc dấu gạch chéo ngược.";
            case "document-size-limit": return "Bản thử hỗ trợ thân tài liệu tối đa 500.000 vị trí Word.";
            case "native-content-changed": case "metadata-changed": case "duplicate-entry-id": case "native-shape-changed": case "managed-range-expanded": return "Công thức hoặc metadata đã đổi. Locus giữ nội dung hiện tại; chưa thể khôi phục từ nguồn cũ.";
            default: return "Phiên xem trước không còn hợp lệ hoặc Word chưa sẵn sàng. Chọn lại vùng và mở Locus. (" + key + ")";
        }
    }
    public void OnDisconnection(Extensibility.ext_DisconnectMode removeMode, ref Array custom) => Dispose();
    public void OnAddInsUpdate(ref Array custom) { }
    public void OnStartupComplete(ref Array custom) { }
    public void OnBeginShutdown(ref Array custom) => Dispose();
    private void Dispose()
    {
        if (disposed) return; disposed = true;
        timer?.Stop(); timer?.Dispose(); ClearSession(); ClearScan(); input?.Dispose();
        if (app != null) try { app.WindowSelectionChange -= SelectionChanged; app.WindowActivate -= WindowActivated; app.DocumentBeforeClose -= BeforeClose; } catch (COMException) { }
        dispatcher?.Dispose(); app = null;
    }
    private sealed class WordOwner : IWin32Window { public IntPtr Handle { get; } public WordOwner(IntPtr handle) { Handle = handle; } }
}
