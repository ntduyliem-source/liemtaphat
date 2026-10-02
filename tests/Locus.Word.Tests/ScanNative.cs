using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Locus.Word;
using WordApi = Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static int RunScanDemo(string[] args)
    {
        directory = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/phase-f/demo"); Directory.CreateDirectory(directory);
        app = new WordApi.Application(); app.Visible = true; app.DisplayAlerts = WordApi.WdAlertLevel.wdAlertsNone;
        var doc = app.Documents.Add();
        doc.Content.Text = "Locus F — Tài liệu dùng thử\rChọn Quét thân tài liệu trong tab Locus.\r\rToán: lc[x mũ 2] và lc[1/2x].\rVật lý: ly-[v=10 m/s].\rHóa: hoa-[3H2+O2=H2O].\rVăn bản, emoji 👩‍🏫 và email a@example.test giữ nguyên.\r";
        string file = Path.Combine(directory, "Locus-F-demo.docx"); doc.SaveAs2(file, WordApi.WdSaveFormat.wdFormatXMLDocument, AddToRecentFiles: false); app.Selection.SetRange(0, 0); doc.UndoClear();
        var process = System.Diagnostics.Process.GetProcessesByName("WINWORD").Single();
        Write("ownership.json", new { pid = process.Id, startTimeUtc = process.StartTime.ToUniversalTime().ToString("o"), file, hwnd = app.ActiveWindow.Hwnd });
        Stage("scan-demo/editor-prerequisite");
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (!(bool)((Dictionary<string, object>)ManualState()["input"])["EditorFocus"] && wait.Elapsed.TotalSeconds < 40) Thread.Sleep(200);
        try { FocusManual(); var state = json.Deserialize<Dictionary<string, object>>((string)Manual.ScanDocument(true)); Write("scan.json", state); Check(state["sessionId"] != null, "Scan unavailable."); }
        catch (Exception error) { Write("preview-unavailable.json", new { error = error.Message }); }
        Console.WriteLine("Opened Word demo: " + file); return 0;
    }
    private static Dictionary<string, object> ScanState() => json.Deserialize<Dictionary<string, object>>((string)Manual.GetScanState());
    private static Dictionary<string, object>[] ScanRows(Dictionary<string, object> s) => ((ArrayList)s["regions"]).Cast<Dictionary<string, object>>().ToArray();
    private static Dictionary<string, object> Scan(bool body = true)
    {
        Manual.CancelScan();
        // Closing the previous owned modeless panel can queue a Word window activation.
        // Explicitly activate our fixture and check it; never alter the add-in focus guard.
        var target = documents.Last();
        for (int attempt = 0; attempt < 3; attempt++)
        {
            target.Activate(); target.Windows[1].Activate(); FocusManual();
            if (ManualSession.Identity(app.ActiveDocument) == ManualSession.Identity(target)) break;
        }
        Check(ManualSession.Identity(app.ActiveDocument) == ManualSession.Identity(target), "Fixture activation changed before scanning.");
        var s = json.Deserialize<Dictionary<string, object>>((string)Manual.ScanDocument(body));
        Check(s["sessionId"] != null && !(bool)s["stale"], "Scan refused: " + json.Serialize(s)); return s;
    }
    private static Dictionary<string, object> ScanAction(Dictionary<string, object> s, string action, string id = "", string argument = "", bool complete = true)
    {
        s = json.Deserialize<Dictionary<string, object>>((string)Manual.ScanCommand((string)s["sessionId"], (string)s["identity"], action, id, argument));
        if (!complete) return s;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (System.Convert.ToInt32(s["pending"]) > 0 && timer.Elapsed.TotalSeconds < 8) { Thread.Sleep(250); s = ScanState(); }
        if ((bool)s["stale"]) Write("failure-" + action + ".json", new { state = s, document = State(app.ActiveDocument) });
        Check(!(bool)s["stale"] && System.Convert.ToInt32(s["pending"]) == 0, "Scan action failed: " + json.Serialize(s)); return s;
    }
    private static WordApi.Document ScanDocument(string raw)
    {
        Manual.CancelScan(); Manual.CancelPreview(); var doc = app.Documents.Add(); documents.Add(doc);
        doc.Content.Text = raw; doc.Activate(); app.Selection.SetRange(0, 0); doc.UndoClear(); FocusManual(); return doc;
    }
    private static void RunScanNative()
    {
        app.Visible = true; var anchor = app.Documents.Add(); anchor.Content.Text = "Locus F — native acceptance fixture.\r";
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Locus", "word-manual-settings.json");
        byte[]? original = File.Exists(path) ? File.ReadAllBytes(path) : null;
        string configuration = (string)ManualState()["configuration"];
        try
        {
            Stage("scan-native/editor-prerequisite");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!(bool)((Dictionary<string, object>)ManualState()["input"])["EditorFocus"] && timer.Elapsed.TotalSeconds < 40) Thread.Sleep(200);
            FocusManual(); Manual.ConfigureProfiles(smartSettings.Serialize());
            Test("scan-native/read-only-unicode-mixed-duplicates", () => {
                var doc = ScanDocument("Trước 👩‍🏫 e\u0301 lc[x mu\u0303 2] giữa lc[x^2] và lc[x^2] sau.\rVật lý: ly-[v=10 m/s].\rHóa: hoa-[3H2+O2=H2O].\rhttps://example.test/a a@example.test\r");
                doc.Range(0, 5).Bold = 1; doc.Range(0, 5).HighlightColorIndex = WordApi.WdColorIndex.wdYellow;
                string before = State(doc); int start = app.Selection.Start, end = app.Selection.End;
                var s = Scan(); var rows = ScanRows(s); Write("mixed-scan.json", s);
                Write("mixed-readonly.json", new { before, after = State(doc), start, end, actualStart = app.Selection.Start, actualEnd = app.Selection.End, bold = doc.Range(0, 5).Bold, highlight = doc.Range(0, 5).HighlightColorIndex });
                Check(rows.Length == 5, "Expected five detected regions.");
                Check(State(doc) == before && app.Selection.Start == start && app.Selection.End == end && doc.Range(0, 5).Bold == -1 && doc.Range(0, 5).HighlightColorIndex == WordApi.WdColorIndex.wdYellow, "Scanning wrote or changed selection/format.");
                foreach (var r in rows) Check(doc.Range((int)r["Start"], (int)r["End"]).Text == (string)r["source"], "Word CP/UTF16 mapping differs.");
                Check(rows.Where(r => (string)r["source"] == "lc[x^2]").Select(r => (string)r["Id"]).Distinct().Count() == 2, "Duplicate source lost identity.");
                foreach (var r in rows.Where(r => !(bool)r["ready"])) { s = ScanAction(s, "select", (string)r["Id"]); s = ScanAction(s, "choose", (string)r["Id"], (string)r["candidateId"]); }
                s = ScanAction(s, "convert-all"); Check(doc.OMaths.Count == 5 && doc.ContentControls.Count == 5, "Batch omitted native equations.");
                Check(doc.Range(0, 5).Bold == -1 && doc.Range(0, 5).HighlightColorIndex == WordApi.WdColorIndex.wdYellow, "Batch changed outside formatting.");
                var chemistry = doc.ContentControls.Cast<WordApi.ContentControl>().Select(c => WordOperations.ReadUnique(doc, c)).Single(c => c.OriginalSource.StartsWith("hoa-", StringComparison.Ordinal));
                Check(!chemistry.State.CanCancelBalance && chemistry.OriginalSource == "hoa-[3H2+O2=H2O]", "Batch applied chemistry assistance.");
                string after = State(doc); Manual.CancelScan(); Check(doc.Undo(1) && State(doc) == before, "Batch is not one Undo."); Check(doc.Redo(1) && State(doc) == after, "Batch Redo differs.");
            });
            Test("scan-native/selection-and-excluded-contexts", () => {
                var doc = ScanDocument("A lc[x^2] B lc[y^3] C\r"); int offset = doc.Content.Text.IndexOf("lc[y^3]", StringComparison.Ordinal); Check(offset >= 0, "Fixture missing."); var range = doc.Range(offset, offset + 7); range.Select();
                var s = Scan(false); Check(ScanRows(s).Length == 1 && (string)ScanRows(s)[0]["source"] == "lc[y^3]", "Selection scope escaped.");
                Manual.CancelScan(); range.SetRange(range.Start + 3, range.End - 1); range.Select(); s = Scan(false); Check(ScanRows(s).Length == 0, "Partial marker created a formula.");
                Manual.CancelScan(); var table = doc.Tables.Add(doc.Range(0, 0), 1, 1); table.Cell(1, 1).Range.Text = "lc[z^4]";
                doc.Fields.Add(doc.Range(doc.Content.End - 1, doc.Content.End - 1), WordApi.WdFieldType.wdFieldQuote, "\"lc[t^5]\"", false);
                s = Scan(); Check(ScanRows(s).Length == 2, "Table/field admitted or ordinary formulas lost.");
            });
            Test("scan-native/plain-text-selection-batch-and-ambiguity", () => {
                var doc = ScanDocument("Đầu: x mũ 2; v=10 m/s; H2SO4. Cuối.\rlc[1/2x]\r");
                var s = Scan(); Write("plain-scan.json", s); var rows = ScanRows(s);
                Check(rows.Length == 4 && rows.Count(r => !(bool)r["ready"]) == 2, "Plain mixed/ambiguous detection differs.");
                var first = rows[0]; int begin = (int)first["Start"], end = (int)rows[2]["End"];
                Manual.CancelScan(); doc.Range(begin, end).Select(); s = Scan(false);
                Check(ScanRows(s).Length == 3, "Selected scope lost formulas.");
                var selected = ScanRows(s)[0]; s = ScanAction(s, "go", (string)selected["Id"]);
                Check(app.Selection.Start == (int)selected["Start"] && app.Selection.End == (int)selected["End"], "Navigation did not select the detected source.");
                s = ScanAction(s, "convert-all"); Write("plain-after-batch.json", new { state = s, math = doc.OMaths.Count, document = State(doc), controls = doc.ContentControls.Cast<WordApi.ContentControl>().Select(c => new { c.Range.Start, c.Range.End }).ToArray() }); Check(doc.OMaths.Count == 2 && ScanRows(s).Length == 3 && ScanRows(s).Count(r => (bool)r["native"]) == 2, "Selection scope changed or ambiguous region converted.");
                var physics = ScanRows(s).Single(r => !(bool)r["native"]);
                string reading = Smart("v=10 m/s").Readings!.Candidates.Single(c => c.Document.Domain == "physics").Id;
                s = ScanAction(s, "preview", (string)physics["Id"], reading); s = ScanAction(s, "choose", (string)physics["Id"], reading);
                s = ScanAction(s, "convert", (string)physics["Id"]);
                Check(doc.OMaths.Count == 3 && ScanRows(s).All(r => (bool)r["native"]), "Confirmed physics did not convert in scope.");
                Check(doc.Content.Text.Contains("lc[1/2x]"), "Outside selection changed.");
                s = Scan(); var ambiguous = ScanRows(s).Single(r => !(bool)r["native"]);
                Check(!(bool)ambiguous["ready"], "Ambiguity entered batch after rescan.");
                string before = State(doc); ScanAction(s, "convert-all", complete: false); Check(State(doc) == before, "Empty eligible set wrote.");
            });
            Test("scan-native/ignore-save-reopen-restore-include", () => {
                var doc = ScanDocument("Đầu lc[x^2] giữa lc[y^3] cuối.\r"); string originalText = doc.Content.Text;
                var s = Scan(); Write("persistent-initial.json", new { state = s, document = State(doc), active = app.ActiveDocument.Name, expected = doc.Name }); Check(ScanRows(s).Length == 2, "Expected two source regions: " + json.Serialize(s)); var first = ScanRows(s)[0]; s = ScanAction(s, "ignore", (string)first["Id"]);
                Check(doc.OMaths.Count == 0 && doc.Content.Text == originalText && ScanSession.ReadText(doc, doc.ContentControls[1]).Ignored, "Ignore changed text or lost choice.");
                string ignoredState = State(doc); Manual.CancelScan(); Check(doc.Undo(1) && doc.ContentControls.Count == 0 && doc.Content.Text == originalText, "Ignore is not one Undo."); Check(doc.Redo(1) && State(doc) == ignoredState, "Ignore Redo differs."); s = Scan();
                s = ScanAction(s, "convert-all"); Check(doc.OMaths.Count == 1, "Ignored region converted.");
                string saved = State(doc); string file = Path.Combine(directory, "persistent-decisions.docx");
                Manual.CancelScan(); doc.SaveAs2(file, WordApi.WdSaveFormat.wdFormatXMLDocument, AddToRecentFiles: false); Close(doc);
                doc = app.Documents.Open(file); documents.Add(doc); s = Scan();
                Check(State(doc) == saved && ScanRows(s).Count(r => (bool)r["ignored"]) == 1 && ScanRows(s).Count(r => (bool)r["native"]) == 1, "Save/reopen lost decisions.");
                var native = ScanRows(s).Single(r => (bool)r["native"]); s = ScanAction(s, "restore", (string)native["Id"]);
                Check(doc.OMaths.Count == 0 && doc.Content.Text == originalText && ScanRows(s).Count(r => (bool)r["ready"]) == 1, "Restore did not retain detection.");
                var ignored = ScanRows(s).Single(r => (bool)r["ignored"]); s = ScanAction(s, "include", (string)ignored["Id"]);
                s = ScanAction(s, "convert-all"); Check(doc.OMaths.Count == 2, "Included text could not reconvert.");
                Write("persistent-final.json", s);
                var nativeRegion = ScanRows(s).First(r => (bool)r["native"]); s = ScanAction(s, "ignore", (string)nativeRegion["Id"]);
                var kept = doc.ContentControls.Cast<WordApi.ContentControl>().Single(c => c.Tag.StartsWith(ScanTextSnapshot.Prefix, StringComparison.Ordinal));
                Manual.CancelScan(); kept.Range.Text = "changed"; string changed = State(doc); s = Scan();
                Check(ScanRows(s).Any(r => (string)r["Problem"] == "scan-text-changed") && State(doc) == changed, "Edited managed text was trusted or replaced.");
            });
            Test("scan-native/repair-preview-must-be-confirmed", () => {
                var doc = ScanDocument("lc[x+1/2]\r"); string before = State(doc); var s = Scan(); Write("repair-initial.json", new { before, after = State(doc), state = s }); var row = ScanRows(s)[0];
                var repair = ((ArrayList)row["candidates"]).Cast<Dictionary<string, object>>().First(c => (string)c["Kind"] == "repair");
                s = ScanAction(s, "preview", (string)row["Id"], (string)repair["Id"]);
                var refused = ScanAction(s, "convert", (string)row["Id"], complete: false); Check((bool)refused["stale"] && State(doc) == before, "Unconfirmed preview wrote.");
                s = Scan(); row = ScanRows(s)[0]; s = ScanAction(s, "preview", (string)row["Id"], (string)repair["Id"]);
                s = ScanAction(s, "choose", (string)row["Id"], (string)repair["Id"]); ScanAction(s, "convert", (string)row["Id"]);
                Check(WordOperations.ReadUnique(doc, doc.ContentControls[1]).Selected.Id == (string)repair["Id"], "Confirmed preview differs from native.");
            });
            Test("scan-native/stale-pending-selection-source-settings", () => {
                foreach (string kind in new[] { "selection", "source", "settings" })
                {
                    var doc = ScanDocument("lc[x^2] puis lc[y^3]\r"); var s = Scan(); s = ScanAction(s, "convert-all", complete: false);
                    Check(System.Convert.ToInt32(s["pending"]) == 2, "No pending batch.");
                    if (kind == "selection") app.Selection.SetRange(1, 1);
                    if (kind == "source") doc.Range(doc.Content.End - 1, doc.Content.End - 1).Text = "changed";
                    if (kind == "settings") Manual.ConfigureDetection("lc[", "]", 1);
                    string expected = State(doc); Thread.Sleep(900); var refused = ScanState();
                    Check((bool)refused["stale"] && State(doc) == expected && doc.OMaths.Count == 0, "Stale " + kind + " wrote.");
                    Manual.CancelScan(); Close(doc); Manual.ConfigureProfiles(smartSettings.Serialize());
                }
            });
            Test("scan-native/native-drift-and-duplicate-ids", () => {
                var doc = ScanDocument("lc[x^2] et lc[y^3]\r"); ScanAction(Scan(), "convert-all"); Manual.CancelScan();
                doc.ContentControls[2].Tag = doc.ContentControls[1].Tag; string before = State(doc); var s = Scan();
                Check(ScanRows(s).All(r => !(bool)r["ready"] && ((string)r["Problem"]).Length > 0) && State(doc) == before, "Duplicate ID trusted.");
                Manual.CancelScan(); Close(doc); doc = ScanDocument("lc[x^2]\r"); ScanAction(Scan(), "convert-all"); Manual.CancelScan();
                doc.ContentControls[1].Range.OMaths[1].Range.Text = "x+3"; before = State(doc); s = Scan();
                Check(ScanRows(s).All(r => !(bool)r["ready"] && ((string)r["Problem"]).Length > 0) && State(doc) == before, "Native drift trusted.");
            });
            Test("scan-native/limits-and-tracking", () => {
                foreach (string kind in new[] { "regions", "document", "tracking" })
                {
                    var doc = ScanDocument(kind == "regions" ? string.Concat(Enumerable.Repeat("lc[x^2]\r", 65)) : kind == "document" ? new string('a', 100001) : "lc[x^2]\r");
                    if (kind == "tracking") doc.TrackRevisions = true;
                    string before = State(doc); var s = json.Deserialize<Dictionary<string, object>>((string)Manual.ScanDocument(true));
                    Check(s["sessionId"] == null && State(doc) == before, "Limit/track changes mutated or admitted: " + kind);
                    Manual.CancelScan(); Close(doc);
                }
            });
        }
        finally
        {
            try { Manual.CancelScan(); Manual.ConfigureProfiles(configuration); }
            finally { if (original != null) File.WriteAllBytes(path, original); else if (File.Exists(path)) File.Delete(path); anchor.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges); }
        }
    }
    private static void RunScanTransactions()
    {
        dynamic research = app.COMAddIns.Item("Locus.Word.W0").Object;
        foreach (string fault in new[] { "native:0", "step:0", "native:1", "step:1", "before-end", "verified", "cancel:1" })
            Test("scan-transaction/rollback/" + fault, () => {
                research.CreateSandbox("Đầu 👩‍🏫 e\u0301 lc[x^2] giữa lc[y^3] cuối lc[z^4]."); var doc = app.ActiveDocument; documents.Add(doc);
                doc.Range(0, 5).Bold = 1; doc.UndoClear(); string before = State(doc); bool failed = false;
                try { research.RunScanResearchOperation("convert", fault); } catch (Exception e) { failed = true; Check(e.Message.Contains("F_FAULT") || e.Message.Contains("F_CANCEL"), "Unexpected rollback failure: " + e); }
                Check(failed && State(doc) == before && doc.Range(0, 5).Bold == -1, "Failure left a partial batch.");
            });
        Test("scan-transaction/restore-rollback-and-one-undo", () => {
            research.CreateSandbox("Đầu lc[x^2] giữa lc[y^3] cuối."); var doc = app.ActiveDocument; documents.Add(doc);
            string text = doc.Content.Text; research.RunScanResearchOperation("convert", ""); string native = State(doc);
            bool failed = false; try { research.RunScanResearchOperation("restore", "step:1"); } catch (Exception e) { failed = true; Write("restore-error.json", new { error = e.ToString() }); Check(e.Message.Contains("F_FAULT"), "Restore failed unexpectedly: " + e); }
            Write("restore-rollback.json", new { failed, before = native, after = State(doc), count = doc.ContentControls.Count });
            Check(failed && State(doc) == native, "Restore rollback differs.");
            research.RunScanResearchOperation("restore", ""); string restored = State(doc);
            Check(doc.Content.Text == text && doc.OMaths.Count == 0 && doc.ContentControls.Count == 2, "Restore batch lost original source.");
            Check(doc.Undo(1) && State(doc) == native && doc.Redo(1) && State(doc) == restored, "Restore batch Undo/Redo differs.");
        });
    }
}
