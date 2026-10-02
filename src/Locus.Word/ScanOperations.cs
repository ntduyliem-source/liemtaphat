using System;
using System.Collections.Generic;
using System.Linq;
using Locus.Core.Export;
using WordApi = Microsoft.Office.Interop.Word;

namespace Locus.Word;

internal sealed class ScanWrite
{
    public ScanRegion Region { get; }
    public string Action { get; }
    public ManagedSnapshot Snapshot { get; }
    public string Payload { get; }
    public string Package { get; }
    public ScanWrite(ScanRegion region, string action)
    {
        if (region.Problem.Length != 0 || region.Choice == null) throw new InvalidOperationException("scan-stale");
        if (action == "convert" && !region.Choice.Ready || action == "restore" && !region.Choice.Native || action == "include" && region.Choice.Native ||
            action != "convert" && action != "restore" && action != "ignore" && action != "include") throw new InvalidOperationException("scan-action-unavailable");
        Region = region; Action = action;
        Snapshot = new ManagedSnapshot(region.Choice.Formula, region.EntryId.Length == 0 ? null : region.EntryId);
        Payload = action == "convert" ? Snapshot.Encode() : new ScanTextSnapshot(Snapshot, action == "ignore", region.Choice.Confirmed || region.Choice.Native).Encode();
        Package = action == "convert" ? WordOperations.Package(CandidateExporter.ToOmml(Snapshot.Selected)) : "";
    }
}

internal static class ScanOperations
{
    public static IReadOnlyList<string> Execute(WordApi.Application app, ScanSession scan, IReadOnlyList<ScanWrite> writes,
        Action beforeWrite, Action betweenWrites, string fault = "", Action<string>? trace = null)
    {
        NativeInputState.RequireWordThread((IntPtr)app.ActiveWindow.Hwnd);
        if (writes.Count == 0 || writes.Count > ScanSession.MaxRegions || writes.Select(w => w.Region.Id).Distinct().Count() != writes.Count || writes.Any(w => !scan.Regions.Contains(w.Region))) throw new InvalidOperationException("scan-empty-plan");
        if (app.UndoRecord.IsRecordingCustomRecord) throw new InvalidOperationException("another-transaction");
        var doc = scan.Document; string before = ScanSession.DocumentStamp(doc, false);
        var inserted = new List<Tuple<string, bool>>(); bool attempted = false;
        beforeWrite();
        // Range.WordOpenXML may end Word's custom Undo record. Validate every native
        // signature before starting it; inside, only compare live ranges/tags/text.
        foreach (var write in writes) scan.ValidateRegion(write.Region);
        app.UndoRecord.StartCustomRecord(writes.Count > 1 ? "Locus: convert detected formulas" : "Locus: formula decision");
        trace?.Invoke("started");
        try
        {
            int index = 0;
            foreach (var write in writes.OrderByDescending(w => w.Region.Start))
            {
                betweenWrites(); ScanSession.RequireDocument(app);
                var region = write.Region;
                // Earlier source ranges retain their Word coordinates while later ranges change.
                scan.ValidateRegion(region, false);
                var range = region.Anchor.Duplicate;
                if (range.Information[WordApi.WdInformation.wdWithInTable]) throw new InvalidOperationException("table");
                if (!app.UndoRecord.IsRecordingCustomRecord || app.UndoRecord.CustomRecordLevel != 1) throw new InvalidOperationException("scan-transaction-interrupted");
                attempted = true;
                if (write.Action == "convert")
                {
                    if (region.ControlId.Length != 0) scan.FindControl(region).Delete(false);
                    if (range.Text != write.Snapshot.OriginalSource || range.OMaths.Count != 0 || range.Fields.Count != 0 || range.InlineShapes.Count != 0) throw new InvalidOperationException("source-changed");
                    int start = range.Start; range.Select(); app.Selection.InsertXML(write.Package);
                    Fail("native:" + index, fault);
                    var equations = doc.Range(start, app.Selection.End).OMaths;
                    if (equations.Count != 1) throw new InvalidOperationException("native-shape-changed");
                    var control = doc.ContentControls.Add(WordApi.WdContentControlType.wdContentControlRichText, equations[1].Range);
                    control.Title = "Locus formula"; control.Tag = write.Payload;
                    if (control.Tag != write.Payload) throw new InvalidOperationException("metadata-changed");
                    inserted.Add(Tuple.Create(control.ID, true));
                }
                else if (region.Choice!.Native)
                {
                    string placeholder = "LOCUS_F_" + Guid.NewGuid().ToString("N");
                    range.Select(); app.Selection.InsertXML(Plain(placeholder));
                    foreach (WordApi.ContentControl c in doc.ContentControls) if (c.ID == region.ControlId) { c.Delete(false); break; }
                    var restored = doc.Content;
                    restored.Find.ClearFormatting();
                    if (!restored.Find.Execute(FindText: placeholder, MatchCase: true, MatchWholeWord: false, MatchWildcards: false, MatchSoundsLike: false, MatchAllWordForms: false, Forward: true, Wrap: WordApi.WdFindWrap.wdFindStop, Format: false) || restored.Text != placeholder)
                        throw new InvalidOperationException("scan-restore-range");
                    // Insert source and its capsule together. Word may collapse Selection.End
                    // for a plain run; discover the new control by the frozen entry payload.
                    restored.Select(); app.Selection.InsertXML(TextPackage(write.Snapshot.OriginalSource, write.Payload));
                    var control = doc.ContentControls.Cast<WordApi.ContentControl>().Single(c => c.Tag == write.Payload);
                    if (control.Range.Text != write.Snapshot.OriginalSource) throw new InvalidOperationException("scan-restore-source");
                    inserted.Add(Tuple.Create(control.ID, false));
                }
                else
                {
                    var control = region.ControlId.Length == 0 ? doc.ContentControls.Add(WordApi.WdContentControlType.wdContentControlRichText, range) : scan.FindControl(region);
                    WriteText(control, write.Payload); inserted.Add(Tuple.Create(control.ID, false));
                }
                trace?.Invoke("step:" + index); Fail("step:" + index, fault); index++;
            }
            betweenWrites(); Fail("before-end", fault);
        }
        catch (Exception error)
        {
            app.UndoRecord.EndCustomRecord();
            trace?.Invoke("ended-before-rollback");
            if (attempted && ScanSession.DocumentStamp(doc, false) != before) Rollback(doc, before, error, trace);
            throw;
        }
        finally { if (app.UndoRecord.IsRecordingCustomRecord) app.UndoRecord.EndCustomRecord(); }
        try
        {
            foreach (var item in inserted)
            {
                var control = doc.ContentControls.Cast<WordApi.ContentControl>().Single(c => c.ID == item.Item1);
                if (item.Item2) WordOperations.ReadUnique(doc, control); else ScanSession.ReadText(doc, control);
            }
            Fail("verified", fault);
            return inserted.Select(i => i.Item1).ToArray();
        }
        catch (Exception error) { Rollback(doc, before, error); throw; }
    }
    private static void WriteText(WordApi.ContentControl control, string payload)
    { control.Title = "Locus detected text"; control.Tag = payload; if (control.Tag != payload) throw new InvalidOperationException("metadata-changed"); }
    private static string Plain(string text) => WordOperations.Package("<w:r><w:t xml:space=\"preserve\">" + System.Security.SecurityElement.Escape(text) + "</w:t></w:r>");
    private static string TextPackage(string text, string payload) => WordOperations.Package("<w:sdt><w:sdtPr><w:alias w:val=\"Locus detected text\"/><w:tag w:val=\"" + System.Security.SecurityElement.Escape(payload) + "\"/></w:sdtPr><w:sdtContent><w:r><w:t xml:space=\"preserve\">" + System.Security.SecurityElement.Escape(text) + "</w:t></w:r></w:sdtContent></w:sdt>");
    private static void Fail(string stage, string fault) { if (stage == fault) throw new InvalidOperationException("F_FAULT:" + stage); }
    private static void Rollback(WordApi.Document doc, string before, Exception error, Action<string>? trace = null)
    {
        bool undone = doc.Undo(1); trace?.Invoke("undone:" + undone);
        if (!undone || ScanSession.DocumentStamp(doc, false) != before) throw new InvalidOperationException("scan-rollback-failed", error);
    }
}
