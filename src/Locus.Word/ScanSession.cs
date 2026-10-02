using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Locus.Core;
using WordApi = Microsoft.Office.Interop.Word;

namespace Locus.Word;

internal sealed class ScanRegion
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public WordApi.Range Anchor { get; }
    public int Start { get; }
    public int End { get; }
    public string Raw { get; }
    public string ControlId { get; }
    public string Tag { get; }
    public string EntryId { get; }
    public ScanChoice? Choice { get; }
    public string Problem { get; }
    public ScanRegion(WordApi.Range range, ScanChoice? choice, string controlId = "", string tag = "", string entryId = "", string problem = "")
    { Anchor = range.Duplicate; Start = range.Start; End = range.End; Raw = range.Text; Choice = choice; ControlId = controlId; Tag = tag; EntryId = entryId; Problem = problem; }
    public string Status => Problem.Length != 0 ? "Đã thay đổi · giữ nguyên" : Choice!.Status;
    public string Source => Choice?.Formula.OriginalSource ?? Raw;
}

internal sealed class ScanSession
{
    public const int MaxRegions = 64;
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public WordApi.Document Document { get; }
    public WordApi.Window Window { get; }
    public WordApi.Range Scope { get; }
    public bool WholeBody { get; }
    public IReadOnlyList<ScanRegion> Regions { get; }
    public string Notice { get; }
    public string Fingerprint { get; }
    public string Configuration { get; }
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    private readonly long documentKey;
    private readonly int windowId;
    public string Identity => ManualStateCodec.Digest(Id + "|" + string.Join("|", Regions.Select(r => r.Id + ":" + r.Choice?.Formula.Identity + ":" + r.Choice?.Confirmed + ":" + r.Choice?.Ignored)));
    private ScanSession(WordApi.Document document, WordApi.Window window, WordApi.Range scope, bool wholeBody, WordDetectionSettings settings, IReadOnlyList<ScanRegion> regions, string notice)
    {
        Document = document; Window = window; Scope = scope.Duplicate; WholeBody = wholeBody;
        documentKey = ManualSession.Identity(Document); windowId = Window.Hwnd; Configuration = settings.Fingerprint;
        Regions = regions; Notice = notice; Fingerprint = DocumentStamp(Document);
    }
    public static void RequireDocument(WordApi.Application app)
    {
        if (app.Documents.Count == 0) throw new InvalidOperationException("select-source");
        var doc = app.ActiveDocument;
        if (doc.ReadOnly) throw new InvalidOperationException("read-only");
        if (doc.ProtectionType != WordApi.WdProtectionType.wdNoProtection) throw new InvalidOperationException("protected");
        if (doc.TrackRevisions) throw new InvalidOperationException("track-changes");
        if (doc.Content.End > 100000) throw new InvalidOperationException("scan-document-limit");
    }
    public static string DocumentStamp(WordApi.Document doc, bool includeControlIds = true)
    {
        var value = new StringBuilder(doc.Content.Text);
        value.Append('|').Append(doc.OMaths.Count).Append('|').Append(doc.Fields.Count).Append('|').Append(doc.InlineShapes.Count);
        foreach (WordApi.ContentControl c in doc.ContentControls)
            value.Append('|').Append(includeControlIds ? c.ID : "control").Append(':').Append(c.Range.Start).Append(':').Append(c.Range.End).Append(':').Append(c.Tag).Append(':').Append(c.LockContents).Append(':').Append(c.LockContentControl);
        return ManualStateCodec.Digest(value.ToString());
    }
    public static ScanSession Capture(WordApi.Application app, bool wholeBody, WordDetectionSettings settings, WordApi.Range? retainedScope = null)
    {
        NativeInputState.RequireWordThread((IntPtr)app.ActiveWindow.Hwnd); RequireDocument(app);
        if (app.UndoRecord.IsRecordingCustomRecord) throw new InvalidOperationException("another-transaction");
        var doc = app.ActiveDocument;
        var scope = retainedScope?.Duplicate ?? (wholeBody ? doc.Content.Duplicate : app.Selection.Range.Duplicate);
        var window = app.ActiveWindow;
        long capturedDocument = ManualSession.Identity(doc); int capturedWindow = window.Hwnd;
        if (ManualSession.Identity(scope.Document) != capturedDocument) throw new InvalidOperationException("target-changed");
        if (scope.StoryType != WordApi.WdStoryType.wdMainTextStory || scope.Start >= scope.End) throw new InvalidOperationException("scan-select-scope");
        string before = DocumentStamp(doc); var regions = new List<ScanRegion>(); var notices = new HashSet<string>();
        var blocks = new List<Tuple<int, int>>();
        void Block(WordApi.Range range) { if (range.StoryType == WordApi.WdStoryType.wdMainTextStory) blocks.Add(Tuple.Create(range.Start, range.End)); }
        foreach (WordApi.Table table in doc.Tables) Block(table.Range);
        foreach (WordApi.Field field in doc.Fields) { Block(field.Code); Block(field.Result); blocks.Add(Tuple.Create(field.Code.Start - 1, field.Result.End + 1)); }
        foreach (WordApi.InlineShape shape in doc.InlineShapes) Block(shape.Range);
        foreach (WordApi.OMath math in doc.OMaths) Block(math.Range);
        foreach (WordApi.ContentControl control in doc.ContentControls)
        {
            var range = control.Range; if (range.StoryType != WordApi.WdStoryType.wdMainTextStory) continue;
            Block(range);
            if (range.Start < scope.Start || range.End > scope.End || !control.Tag.StartsWith("locus:", StringComparison.Ordinal)) continue;
            try
            {
                if (range.Information[WordApi.WdInformation.wdWithInTable]) { notices.Add("Bỏ qua vùng trong bảng."); continue; }
                bool text = control.Tag.StartsWith(ScanTextSnapshot.Prefix, StringComparison.Ordinal);
                var saved = text ? ReadText(doc, control) : null;
                var snapshot = saved?.Snapshot ?? WordOperations.ReadUnique(doc, control);
                if (control.LockContents || control.LockContentControl) throw new InvalidOperationException("locked-region");
                regions.Add(new ScanRegion(range, new ScanChoice(snapshot.State, !text, saved?.Ignored ?? false, saved?.Confirmed ?? false), control.ID, control.Tag, snapshot.EntryId));
            }
            catch (Exception e) when (e is InvalidOperationException || e is FormatException || e is ArgumentException)
            { regions.Add(new ScanRegion(range, null, control.ID, control.Tag, problem: e.Message)); }
        }
        var merged = new List<Tuple<int, int>>();
        foreach (var block in blocks.OrderBy(b => b.Item1))
        {
            if (merged.Count == 0 || merged[merged.Count - 1].Item2 < block.Item1) merged.Add(block);
            else merged[merged.Count - 1] = Tuple.Create(merged[merged.Count - 1].Item1, Math.Max(merged[merged.Count - 1].Item2, block.Item2));
        }
        foreach (WordApi.Paragraph paragraph in scope.Paragraphs)
        {
            var p = paragraph.Range;
            if (p.Information[WordApi.WdInformation.wdWithInTable]) { notices.Add("Bỏ qua vùng trong bảng."); continue; }
            int start = p.Start, end = p.End;
            int cursor = start;
            foreach (var block in merged.Where(b => b.Item1 < end && b.Item2 > start))
            { AddGap(cursor, Math.Min(end, block.Item1)); cursor = Math.Max(cursor, block.Item2); }
            AddGap(cursor, end);
        }
        if (regions.Count > MaxRegions) throw new InvalidOperationException("scan-region-limit");
        var ordered = regions.OrderBy(r => r.Start).ToArray();
        for (int i = 1; i < ordered.Length; i++) if (ordered[i - 1].End > ordered[i].Start) throw new InvalidOperationException("scan-overlapping-regions");
        if (DocumentStamp(doc) != before) throw new InvalidOperationException("source-changed");
        if (ManualSession.Identity(app.ActiveDocument) != capturedDocument || app.ActiveWindow.Hwnd != capturedWindow) throw new InvalidOperationException("target-changed");
        return new ScanSession(doc, window, scope, wholeBody, settings, ordered, string.Join(" ", notices));

        void AddGap(int start, int end)
        {
            if (start >= end || end <= scope.Start || start >= scope.End) return;
            var gap = doc.Range(start, end); string raw = gap.Text ?? "";
            if (raw.Length > 4096) { notices.Add("Bỏ qua đoạn dài hơn 4.096 ký tự."); return; }
            foreach (var found in ScanAnalysis.Analyze(raw, settings))
            {
                var anchor = MapSpan(gap, raw, found.Item1);
                // Partial selection must not invent a suffix formula from a larger expression.
                if (anchor.Start < scope.Start || anchor.End > scope.End) continue;
                regions.Add(new ScanRegion(anchor, new ScanChoice(found.Item2)));
                if (regions.Count > MaxRegions) throw new InvalidOperationException("scan-region-limit");
            }
        }
    }
    internal static WordApi.Range MapSpan(WordApi.Range gap, string raw, TextSpan span)
    {
        var range = gap.Duplicate; range.SetRange(Boundary(span.Start), Boundary(span.End));
        if (range.Text != raw.Substring(span.Start, span.Length)) throw new InvalidOperationException("scan-range-mismatch");
        return range;
        int Boundary(int utf16)
        {
            int low = gap.Start, high = gap.End;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                string prefix = gap.Document.Range(gap.Start, middle).Text ?? "";
                if (prefix.Length < utf16) low = middle + 1; else high = middle;
            }
            if ((gap.Document.Range(gap.Start, low).Text ?? "") != raw.Substring(0, utf16)) throw new InvalidOperationException("scan-word-offset-unavailable");
            return low;
        }
    }
    public static ScanTextSnapshot ReadText(WordApi.Document doc, WordApi.ContentControl control)
    {
        var saved = ScanTextSnapshot.Decode(control.Tag);
        if (doc.ContentControls.Cast<WordApi.ContentControl>().Count(c => ScanTextSnapshot.HasEntry(c.Tag, saved.Snapshot.EntryId)) != 1) throw new InvalidOperationException("duplicate-entry-id");
        var range = control.Range;
        if (range.Text != saved.Snapshot.OriginalSource || range.OMaths.Count != 0 || range.Fields.Count != 0 || range.InlineShapes.Count != 0) throw new InvalidOperationException("scan-text-changed");
        return saved;
    }
    public void Validate(WordApi.Application app, WordDetectionSettings settings)
    {
        RequireDocument(app);
        if (app.UndoRecord.IsRecordingCustomRecord) throw new InvalidOperationException("another-transaction");
        if (ManualSession.Identity(app.ActiveDocument) != documentKey || app.ActiveWindow.Hwnd != windowId || settings.Fingerprint != Configuration) throw new InvalidOperationException("target-changed");
        if ((DateTime.UtcNow - CreatedUtc).TotalMinutes > 5 || DocumentStamp(Document) != Fingerprint) throw new InvalidOperationException("scan-stale");
        foreach (var region in Regions) ValidateRegion(region);
    }
    public void ValidateRegion(ScanRegion region, bool verifyNativeXml = true)
    {
        if (ManualSession.Identity(region.Anchor.Document) != documentKey) throw new InvalidOperationException("target-changed");
        if (region.Anchor.Start != region.Start || region.Anchor.End != region.End || region.Anchor.Text != region.Raw) throw new InvalidOperationException("scan-stale");
        if (region.ControlId.Length == 0) return;
        var control = FindControl(region); if (control.Tag != region.Tag) throw new InvalidOperationException("metadata-changed");
        if (region.Problem.Length != 0) return;
        if (control.LockContents || control.LockContentControl) throw new InvalidOperationException("locked-region");
        if (region.Choice!.Native) { if (verifyNativeXml) WordOperations.ReadUnique(Document, control); }
        else ReadText(Document, control);
    }
    public WordApi.ContentControl FindControl(ScanRegion region) => Document.ContentControls.Cast<WordApi.ContentControl>().Single(c => c.ID == region.ControlId);
    public WordApi.Range ScopeAfterWrite(IReadOnlyList<string> controlIds)
    {
        var range = Scope.Duplicate;
        // Word's tracked range can end before the newly inserted equation/control's
        // hidden boundary. Include only the controls written by this frozen plan.
        foreach (WordApi.ContentControl control in Document.ContentControls)
            if (controlIds.Contains(control.ID)) range.SetRange(Math.Min(range.Start, control.Range.Start), Math.Max(range.End, control.Range.End));
        return range;
    }
}
