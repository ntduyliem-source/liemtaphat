using System;
using System.Linq;
using System.Windows.Forms;
using Office = Microsoft.Office.Core;

namespace Locus.Word;

public sealed partial class ManualConnect
{
    private ScanSession? scan;
    private ScanPanel? scanPanel;
    private BadgeController? scanBadge;
    private string badgeRegion = "";
    private ScanWrite[]? scanPending;
    private string scanPendingIdentity = "";
    private bool scanStale;
    private int scanSelectionStart, scanSelectionEnd;
    private DateTime scanQueuedAt, scanInputAt, scanObservedAt;
    private string scanMessage = "";

    public void ScanSelectionClick(Office.IRibbonControl control) => OpenScan(false, true);
    public void ScanBodyClick(Office.IRibbonControl control) => OpenScan(true, true);
    public string ScanDocument(bool wholeBody)
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(() => ScanDocument(wholeBody)));
        OpenScan(wholeBody, false); return GetScanState();
    }
    public void CancelScan()
    {
        if (OnOtherThread) { dispatcher!.Invoke(new Action(CancelScan)); return; }
        ClearScan(); scanMessage = "closed";
    }
    private void OpenScan(bool wholeBody, bool ribbon)
    {
        if (disposed || app == null || busy) return;
        busy = true;
        try
        {
            ScanSession.RequireDocument(app); IntPtr window = (IntPtr)app.ActiveWindow.Hwnd;
            var observation = input!.Read(window);
            if (!(ribbon ? NativeInputState.FocusWithin(window) : observation.EditorFocus) || observation.MessageComposition) throw new InvalidOperationException("outside-editor");
            long target = ManualSession.Identity(app.ActiveDocument);
            int start = app.Selection.Start, end = app.Selection.End;
            CancelPreview(); ClearScan();
            // Closing a modeless window can activate its owner. Keep the original target
            // of the user's command; never silently scan whichever document gained focus.
            if (ManualSession.Identity(app.ActiveDocument) != target || app.ActiveWindow.Hwnd != window.ToInt32() ||
                !wholeBody && (app.Selection.Start != start || app.Selection.End != end)) throw new InvalidOperationException("target-changed");
            scan = ScanSession.Capture(app, wholeBody, settings); scanStale = false;
            scanPanel = new ScanPanel(); scanPanel.Requested += ScanRequested; scanPanel.Rescan += RescanCurrent; scanPanel.Cancelled += ClearScan;
            scanPanel.Present(scan); scanPanel.Show(new WordOwner(window)); scanMessage = "scanned";
            scanObservedAt = DateTime.UtcNow;
        }
        catch (Exception error)
        {
            ClearScan();
            scanMessage = error.Message; lastFailure = error.ToString();
            if (scanPanel == null) { scanPanel = new ScanPanel(); scanPanel.Cancelled += ClearScan; }
            scanPanel.Report(ExplainScan(error), true);
            if (app.Documents.Count > 0) scanPanel.Show(new WordOwner((IntPtr)app.ActiveWindow.Hwnd));
        }
        finally { busy = false; }
    }
    public string ScanCommand(string expectedSession, string expectedIdentity, string action, string regionId, string argument)
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(() => ScanCommand(expectedSession, expectedIdentity, action, regionId, argument)));
        if (scan == null || scan.Id != expectedSession || scan.Identity != expectedIdentity) throw new InvalidOperationException("scan-stale");
        ScanRequested(action, regionId, argument); return GetScanState();
    }
    private void ScanRequested(string action, string regionId, string argument)
    {
        if (scan == null || scanPanel == null || app == null || busy) return;
        busy = true;
        try
        {
            if (scanStale || scanPending != null) throw new InvalidOperationException("scan-stale");
            var region = scan.Regions.SingleOrDefault(r => r.Id == regionId);
            var observation = input!.Read((IntPtr)scan.Window.Hwnd);
            if (!(NativeInputState.FocusWithin(scanPanel.Handle) || observation.EditorFocus) || observation.MessageComposition || !observation.HookInstalled) throw new InvalidOperationException("outside-editor");
            scan.Validate(app, settings);
            if (action == "select") { scanPanel.SelectRegion(regionId); return; }
            if (action == "preview") { scanPanel.SelectRegion(regionId); scanPanel.PreviewReading(argument); return; }
            if (action == "choose")
            {
                if (region?.Choice == null || scanPanel.RegionId != regionId || scanPanel.PreviewCandidateId != argument) throw new InvalidOperationException("candidate-changed");
                region.Choice.Choose(argument); scanPanel.Present(scan, regionId); scanMessage = "choice-confirmed"; return;
            }
            if (action == "go" || action == "inline")
            {
                scanBadge?.Clear(); badgeRegion = "";
                if (action == "go" && region != null)
                {
                    region.Anchor.Select(); scan.Window.Activate(); scan.Window.ActivePane.Activate();
                    if (scanPanel.InlineEnabled)
                    {
                        if (scanBadge == null) { scanBadge = new BadgeController("Locus fx — Vùng đã quét"); scanBadge.Clicked += ShowScanRegion; }
                        scanBadge.Arm(app, scan.Id); badgeRegion = region.Id;
                        // Leave the document visible while the inline control is in use.
                        // The same panel and selected region return when fx is clicked.
                        scanPanel.Hide();
                    }
                }
                scanMessage = "located"; return;
            }
            if (region != null && scanPanel.RegionId == regionId && region.Choice != null && scanPanel.PreviewCandidateId != region.Choice.Formula.Selected?.Id)
                throw new InvalidOperationException("scan-confirm-reading");
            var targets = action == "convert-all" ? scan.Regions.Where(r => r.Problem.Length == 0 && r.Choice?.Ready == true).ToArray() : region == null ? Array.Empty<ScanRegion>() : new[] { region };
            scanPending = targets.Select(r => new ScanWrite(r, action == "convert-all" ? "convert" : action)).ToArray();
            if (scanPending.Length == 0) throw new InvalidOperationException("scan-empty-plan");
            scanPendingIdentity = scan.Identity;
            scanSelectionStart = app.Selection.Start; scanSelectionEnd = app.Selection.End;
            scanQueuedAt = DateTime.UtcNow; scanInputAt = input.LastInputUtc;
            scanPanel.Hide(); scanBadge?.Clear(); scan.Window.Activate(); scan.Window.ActivePane.Activate(); scanMessage = "pending";
        }
        catch (Exception error) { lastFailure = error.ToString(); InvalidateScan(ExplainScan(error)); }
        finally { busy = false; }
    }
    private void ShowScanRegion()
    {
        if (scan == null || scanPanel == null || scanStale || app == null) return;
        try { scan.Validate(app, settings); scanPanel.Present(scan, badgeRegion); if (!scanPanel.Visible) scanPanel.Show(new WordOwner((IntPtr)scan.Window.Hwnd)); scanPanel.Activate(); }
        catch (Exception error) { InvalidateScan(ExplainScan(error)); }
    }
    private void RescanCurrent()
    {
        if (busy || app == null || scan == null || scanPending != null) return;
        busy = true;
        try
        {
            if (ManualSession.Identity(app.ActiveDocument) != ManualSession.Identity(scan.Document)) throw new InvalidOperationException("target-changed");
            scan = ScanSession.Capture(app, scan.WholeBody, settings, scan.WholeBody ? null : scan.Scope); scanStale = false; scanBadge?.Clear();
            scanPanel!.Present(scan); scanMessage = "scanned";
        }
        catch (Exception error) { InvalidateScan(ExplainScan(error)); }
        finally { busy = false; }
    }
    private void TickScan()
    {
        if (disposed || busy || app == null || scan == null || scanStale) return;
        if (scanPending == null && (DateTime.UtcNow - scanObservedAt).TotalMilliseconds < 800) return;
        busy = true;
        try
        {
            if (app.UndoRecord.IsRecordingCustomRecord) return;
            var observation = input!.Read((IntPtr)scan.Window.Hwnd);
            scan.Validate(app, settings); scanObservedAt = DateTime.UtcNow;
            if (scanPending == null)
            {
                if (scanPanel?.InlineEnabled == true)
                {
                    try { scanBadge?.Update(app, scan.Id, observation); }
                    catch { scanBadge?.Clear(); scanPanel.Report("Chưa định vị được dấu fx; tiếp tục chọn vùng trong bảng.", false); }
                }
                return;
            }
            if (!observation.EditorFocus || observation.MessageComposition || input.LastInputUtc != scanInputAt || app.Selection.Start != scanSelectionStart || app.Selection.End != scanSelectionEnd)
                throw new InvalidOperationException("input-or-focus-changed");
            if ((DateTime.UtcNow - scanQueuedAt).TotalMilliseconds < 350) return;
            var current = scan; var plan = scanPending;
            void InputGuard()
            {
                if (!input.Read((IntPtr)current.Window.Hwnd).SafeExplicitTrigger || input.LastInputUtc != scanInputAt ||
                    ManualSession.Identity(app.ActiveDocument) != ManualSession.Identity(current.Document) || app.ActiveWindow.Hwnd != current.Window.Hwnd || settings.Fingerprint != current.Configuration || current.Identity != scanPendingIdentity)
                    throw new InvalidOperationException("input-or-focus-changed");
            }
            void Before() { InputGuard(); current.Validate(app, settings); if (app.Selection.Start != scanSelectionStart || app.Selection.End != scanSelectionEnd) throw new InvalidOperationException("source-or-selection-changed"); }
            var written = ScanOperations.Execute(app, current, plan, Before, InputGuard);
            scanPending = null; scanBadge?.Clear(); scanMessage = "completed:" + plan.Length;
            scan = ScanSession.Capture(app, current.WholeBody, settings, current.WholeBody ? null : current.ScopeAfterWrite(written));
            scanPanel!.Present(scan, message: "Đã thực hiện " + plan.Length + " vùng · một Undo."); scanPanel.Show(new WordOwner((IntPtr)scan.Window.Hwnd));
        }
        catch (Exception error) { lastFailure = error.ToString(); InvalidateScan(ExplainScan(error)); }
        finally { busy = false; }
    }
    private void InvalidateScan(string reason)
    {
        scanPending = null; scanStale = true; scanMessage = reason; scanBadge?.Clear(); scanPanel?.Report(reason, true);
        if (scan != null && scanPanel != null && !scanPanel.Visible && NativeInputState.FocusWithin((IntPtr)scan.Window.Hwnd))
            scanPanel.Show(new WordOwner((IntPtr)scan.Window.Hwnd));
    }
    private void ClearScan()
    {
        scanPending = null; scan = null; scanStale = false; scanBadge?.Dispose(); scanBadge = null;
        var old = scanPanel; scanPanel = null;
        if (old != null) { old.Cancelled -= ClearScan; old.Close(); old.Dispose(); }
    }
    public string GetScanState()
    {
        if (OnOtherThread) return (string)dispatcher!.Invoke(new Func<string>(GetScanState));
        return json.Serialize(new { sessionId = scan?.Id, identity = scan?.Identity, message = scanMessage, stale = scanStale, pending = scanPending?.Length ?? 0,
            scope = scan?.WholeBody == true ? "body" : "selection", notice = scan?.Notice, visible = scanPanel?.Visible ?? false,
            selectedRegion = scanPanel?.RegionId, previewCandidateId = scanPanel?.PreviewCandidateId, lastFailure,
            badge = scanBadge?.State, regions = scan?.Regions.Select(r => new { r.Id, r.Start, r.End, source = r.Source, r.Status, r.Problem, ready = r.Choice?.Ready ?? false,
                native = r.Choice?.Native ?? false, ignored = r.Choice?.Ignored ?? false, confirmed = r.Choice?.Confirmed ?? false,
                candidateId = r.Choice?.Formula.Selected?.Id, candidates = r.Choice?.Formula.Readings?.Candidates.Select(c => new { c.Id, c.Kind }).ToArray() }).ToArray() });
    }
    private static string ExplainScan(Exception error) => error.Message switch
    {
        "scan-stale" => "Nội dung, định dạng quản lý hoặc vị trí đã đổi. Quét lại trước khi chuyển.",
        "scan-select-scope" => "Chọn một đoạn trong thân tài liệu, hoặc dùng Quét thân tài liệu.",
        "scan-document-limit" => "Bản này hỗ trợ thân tài liệu tối đa 100.000 vị trí Word.",
        "scan-region-limit" => "Phạm vi vượt 64 vùng công thức. Chọn đoạn ngắn hơn rồi quét lại.",
        "scan-confirm-reading" => "Bấm Dùng cách hiểu này trước khi chuyển phương án đang xem.",
        "scan-empty-plan" => "Chưa có vùng đủ điều kiện. Chọn cách hiểu hoặc cho phép chuyển lại vùng giữ text.",
        "scan-rollback-failed" => "Không xác minh được hoàn tác. Dừng thao tác và kiểm tra tài liệu trước khi tiếp tục.",
        _ => Explain(error)
    };
}
