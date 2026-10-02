using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Locus.Core;
using Locus.Core.Detection;
using WordApi = Microsoft.Office.Interop.Word;

namespace Locus.Word;

// One explicit selection, held only in memory. Word offsets are never inferred from .NET offsets.
internal sealed class ManualSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public WordApi.Document Document { get; }
    public WordApi.Window Window { get; }
    public WordApi.Range Anchor { get; }
    public ManualFormulaState Formula { get; }
    public CandidateSet? Candidates => Formula.Readings;
    public string EntryId { get; } = "";
    public string Mode { get; }
    public string ControlId { get; } = "";
    public string Tag { get; } = "";
    public int Start { get; }
    public int End { get; }
    public int SelectionStart { get; }
    public int SelectionEnd { get; }
    public string Configuration { get; }
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    private readonly string documentHash;
    private readonly long documentKey;
    private readonly int windowId;

    public static long Identity(object value)
    {
        IntPtr pointer = Marshal.GetIUnknownForObject(value);
        try { return pointer.ToInt64(); } finally { Marshal.Release(pointer); }
    }

    private ManualSession(WordApi.Application app, ManualFormulaState formula, string mode, string configuration, WordApi.ContentControl? control, string entryId = "")
    {
        Document = app.ActiveDocument; Window = app.ActiveWindow;
        documentKey = Identity(Document); windowId = Window.Hwnd;
        SelectionStart = app.Selection.Start; SelectionEnd = app.Selection.End;
        Anchor = (control == null ? app.Selection.Range : control.Range).Duplicate;
        Start = Anchor.Start; End = Anchor.End;
        Formula = formula; Mode = mode; Configuration = configuration; EntryId = entryId;
        if (control != null) { ControlId = control.ID; Tag = control.Tag; }
        documentHash = Fingerprint(Document);
    }

    public static ManualSession OpenSource(WordApi.Application app, MarkerConfiguration markers, DetectionDomains domains=DetectionDomains.Math)
        => OpenSource(app, WordDetectionSettings.Upgrade(markers, domains));
    public static ManualSession OpenSource(WordApi.Application app, WordDetectionSettings settings)
    {
        string? reason = WordOperations.Refusal(app.ActiveDocument, app.Selection);
        if (reason != null) throw new InvalidOperationException(reason);
        string raw = app.Selection.Text;
        return new ManualSession(app, ManualSourceAnalysis.Analyze(raw, settings), "source", settings.Fingerprint, null);
    }

    public static ManualSession OpenManaged(WordApi.Application app, MarkerConfiguration markers, DetectionDomains domains=DetectionDomains.Math)
        => OpenManaged(app, WordDetectionSettings.Upgrade(markers, domains));
    public static ManualSession OpenManaged(WordApi.Application app, WordDetectionSettings settings)
    {
        var doc = app.ActiveDocument; var selection = app.Selection;
        if (selection.StoryType != WordApi.WdStoryType.wdMainTextStory) throw new InvalidOperationException("non-main-story");
        var controls = doc.ContentControls.Cast<WordApi.ContentControl>().Where(c =>
            c.Range.StoryType == selection.StoryType && c.Range.Start <= selection.Start && c.Range.End >= selection.End &&
            c.Tag.StartsWith("locus:", StringComparison.Ordinal)).ToArray();
        if (controls.Length != 1) throw new InvalidOperationException("select-managed-formula");
        var snapshot = WordOperations.ReadUnique(doc, controls[0]);
        return new ManualSession(app, snapshot.State, "managed", settings.Fingerprint, controls[0], snapshot.EntryId);
    }

    public static string ConfigurationKey(MarkerConfiguration markers,DetectionDomains domains=DetectionDomains.Math) => WordDetectionSettings.Upgrade(markers, domains).Fingerprint;
    private static string Fingerprint(WordApi.Document doc)
    {
        if (doc.Content.End > 500000) throw new InvalidOperationException("document-size-limit");
        using var hash = SHA256.Create();
        return Convert.ToBase64String(hash.ComputeHash(Encoding.Unicode.GetBytes(doc.Content.Text)));
    }

    public void Validate(WordApi.Application app, MarkerConfiguration markers,DetectionDomains domains=DetectionDomains.Math)
        => Validate(app, WordDetectionSettings.Upgrade(markers, domains));
    public void Validate(WordApi.Application app, WordDetectionSettings settings)
    {
        if ((DateTime.UtcNow - CreatedUtc).TotalMinutes > 2) throw new InvalidOperationException("preview-expired");
        if (app.Documents.Count == 0 || Identity(app.ActiveDocument) != documentKey || app.ActiveWindow.Hwnd != windowId)
            throw new InvalidOperationException("target-changed");
        if (Configuration != settings.Fingerprint) throw new InvalidOperationException("settings-changed");
        if (Anchor.Start != Start || Anchor.End != End || app.Selection.Start != SelectionStart || app.Selection.End != SelectionEnd ||
            app.Selection.StoryType != WordApi.WdStoryType.wdMainTextStory || Fingerprint(Document) != documentHash)
            throw new InvalidOperationException("source-or-selection-changed");
        if (Mode == "source")
        {
            string? reason = WordOperations.Refusal(Document, app.Selection);
            if (reason != null) throw new InvalidOperationException(reason);
            if (Anchor.Text != Formula.OriginalSource) throw new InvalidOperationException("source-changed");
        }
        else
        {
            var control = FindControl();
            if (control.Tag != Tag) throw new InvalidOperationException("metadata-changed");
            WordOperations.ReadUnique(Document, control);
            if (Document.ReadOnly || Document.TrackRevisions || Document.ProtectionType != WordApi.WdProtectionType.wdNoProtection ||
                control.LockContentControl || control.LockContents || control.Range.Information[WordApi.WdInformation.wdWithInTable])
                throw new InvalidOperationException("managed-context-not-writable");
        }
    }

    public WordApi.ContentControl FindControl() => Document.ContentControls.Cast<WordApi.ContentControl>().Single(c => c.ID == ControlId);
}
