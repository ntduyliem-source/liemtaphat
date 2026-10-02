using System;
using System.Linq;
using System.Xml.Linq;
using Locus.Core.Export;
using WordApi = Microsoft.Office.Interop.Word;

namespace Locus.Word;

// Shared native transactions. W0 restricts these to fixtures; ManualConnect requires a live explicit preview.
public static class WordOperations
{
    public static string? Refusal(WordApi.Document document, WordApi.Selection selection)
    {
        if (document.ReadOnly) return "read-only";
        if (document.ProtectionType != WordApi.WdProtectionType.wdNoProtection) return "protected";
        if (document.TrackRevisions) return "track-changes";
        if (selection.StoryType != WordApi.WdStoryType.wdMainTextStory) return "non-main-story";
        if (selection.Range.Information[WordApi.WdInformation.wdWithInTable]) return "table";
        if (selection.Type != WordApi.WdSelectionType.wdSelectionNormal || selection.Start == selection.End) return "select-source";
        if (selection.Text == null || selection.Text.Length > 4096 || selection.Text.IndexOfAny(new[] {'\r','\n','\a'}) >= 0) return "source-shape";
        if (selection.Range.OMaths.Count != 0 || selection.Range.Fields.Count != 0 || selection.Range.FormFields.Count != 0 || selection.Range.InlineShapes.Count != 0) return "existing-structure";
        foreach (WordApi.ContentControl control in document.ContentControls)
            if (control.Range.StoryType == selection.StoryType && control.Range.Start <= selection.End && control.Range.End >= selection.Start) return "existing-content-control";
        return null;
    }

    public static string Package(string omml) => "<pkg:package xmlns:pkg=\"http://schemas.microsoft.com/office/2006/xmlPackage\"><pkg:part pkg:name=\"/_rels/.rels\" pkg:contentType=\"application/vnd.openxmlformats-package.relationships+xml\"><pkg:xmlData><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships></pkg:xmlData></pkg:part><pkg:part pkg:name=\"/word/document.xml\" pkg:contentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"><pkg:xmlData><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p>" + omml + "</w:p></w:body></w:document></pkg:xmlData></pkg:part></pkg:package>";

    public static string ConvertSelected(WordApi.Application app, ManagedSnapshot snapshot, Action revalidate, string faultAfter = "")
    {
        NativeInputState.RequireWordThread((IntPtr)app.ActiveWindow.Hwnd);
        string payload = snapshot.Encode(); // Complete snapshot and limits before the first document write.
        var doc = app.ActiveDocument; var selection = app.Selection;
        string? reason = Refusal(doc, selection);
        if (reason != null) throw new InvalidOperationException(reason);
        if (selection.Text != snapshot.OriginalSource) throw new InvalidOperationException("source-changed");
        int beforeStart = selection.Start, beforeEnd = selection.End;
        revalidate();
        if (app.ActiveDocument != doc || selection.Start != beforeStart || selection.End != beforeEnd || selection.Text != snapshot.OriginalSource) throw new InvalidOperationException("target-changed");
        if (app.UndoRecord.IsRecordingCustomRecord) throw new InvalidOperationException("another-transaction");
        string beforeText=doc.Content.Text; int beforeMath=doc.OMaths.Count,beforeControls=doc.ContentControls.Count;
        bool changed = false, attempted = false;
        WordApi.ContentControl? insertedControl=null;
        app.UndoRecord.StartCustomRecord("Locus: convert formula");
        try
        {
            // Selection.InsertXML preserves Word's original selection in its native undo record.
            attempted=true; selection.InsertXML(Package(CandidateExporter.ToOmml(snapshot.Selected))); Fault("InsertReturnedBeforeFlag", faultAfter); changed = true; Fault("NativeInserted", faultAfter);
            var native = doc.Range(beforeStart, selection.End).OMaths;
            if (native.Count != 1) throw new InvalidOperationException("Native insertion did not create exactly one equation.");
            var control = doc.ContentControls.Add(WordApi.WdContentControlType.wdContentControlRichText, native[1].Range);
            insertedControl=control;
            control.Title = "Locus formula"; Fault("ControlAdded", faultAfter);
            control.Tag = payload;
            if (control.Tag != payload) throw new InvalidOperationException("Metadata readback differs.");
            Fault("TagWritten", faultAfter);
        }
        catch
        {
            app.UndoRecord.EndCustomRecord();
            if ((changed || attempted && (doc.Content.Text!=beforeText || doc.OMaths.Count!=beforeMath || doc.ContentControls.Count!=beforeControls)) && !doc.Undo(1)) throw new InvalidOperationException("Rollback failed; stop editing this test document.");
            throw;
        }
        finally { if (app.UndoRecord.IsRecordingCustomRecord) app.UndoRecord.EndCustomRecord(); }
        // WordOpenXML can disturb a custom Undo record even though it looks read-only.
        // Validate the complete native result after closing the record, on this same UI turn.
        try {
            ReadUnique(doc,insertedControl!);
            // Set insertion mode last, after native validation. Moving the selection is
            // not a document edit or an additional Undo step.
            PlaceCaretAfterEquation(doc,selection,insertedControl!);Fault("SelectionMoved",faultAfter);
            return insertedControl!.ID;
        }
        catch { if(!doc.Undo(1))throw new InvalidOperationException("Native verification rollback failed.");throw; }
    }

    private static void Fault(string stage, string selected) { if (stage == selected) throw new InvalidOperationException("W0_FAULT:" + stage); }

    internal static void PlaceCaretAfterEquation(WordApi.Document document,WordApi.Selection selection,WordApi.ContentControl control)
    {
        // ContentControl.Range.End can precede Word's hidden closing boundary. Even at the
        // right numeric position, SetRange/Collapse may retain math insertion affinity.
        // Move left from the following plain character to inherit its text insertion mode.
        // A math range obtained through control.Range is clipped to that control. Locate
        // its full document-level range rather than adding a guessed boundary offset.
        int position=ContainingEquation(document,control).Range.End;
        if(position<control.Range.End||position>=document.Content.End)throw new InvalidOperationException($"no-plain-text-continuation:position={position},controlEnd={control.Range.End},documentEnd={document.Content.End}");
        var following=document.Range(position,position);
        int moved=following.MoveEnd(WordApi.WdUnits.wdCharacter,1);
        if(moved!=1||following.OMaths.Count!=0||following.Fields.Count!=0||following.InlineShapes.Count!=0)
            throw new InvalidOperationException($"no-plain-text-continuation:moved={moved},start={following.Start},end={following.End},math={following.OMaths.Count},fields={following.Fields.Count},shapes={following.InlineShapes.Count}");
        following.Select();selection.MoveLeft(WordApi.WdUnits.wdCharacter,1);
        if(selection.Start!=position||selection.End!=position)throw new InvalidOperationException("continuation-caret-moved");
    }

    private static WordApi.OMath ContainingEquation(WordApi.Document document,WordApi.ContentControl control)
    {
        var equations=document.OMaths.Cast<WordApi.OMath>().Where(math=>math.Range.StoryType==control.Range.StoryType&&math.Range.Start<=control.Range.Start&&math.Range.End>=control.Range.End).ToArray();
        if(equations.Length!=1)throw new InvalidOperationException("containing-equation-not-unique");
        return equations[0];
    }

    public static ManagedSnapshot ReadUnique(WordApi.Document document, WordApi.ContentControl control)
    {
        var snapshot = ManagedSnapshot.Decode(control.Tag);
        int copies = 0;
        foreach (WordApi.ContentControl item in document.ContentControls)
        {
            if (!ScanTextSnapshot.HasEntry(item.Tag, snapshot.EntryId)) continue;
            copies++;
        }
        if (copies != 1) throw new InvalidOperationException("duplicate-entry-id");
        if (control.Range.OMaths.Count != 1) throw new InvalidOperationException("native-shape-changed");
        if (control.Range.Text!=control.Range.OMaths[1].Range.Text)throw new InvalidOperationException("managed-range-expanded");
        bool scientific=snapshot.Selected.Document.Domain!="math";
        string expected=NativeMathSignature.FromXml(CandidateExporter.ToOmml(snapshot.Selected),scientific);
        if (NativeMathSignature.FromXml(control.Range.WordOpenXML,scientific) != expected || NativeMathSignature.FromXml(ContainingEquation(document,control).Range.WordOpenXML,scientific)!=expected)
            throw new InvalidOperationException("native-content-changed");
        return snapshot;
    }
    private static void RequireManagedWrite(WordApi.Application app, WordApi.ContentControl control)
    {
        NativeInputState.RequireWordThread((IntPtr)app.ActiveWindow.Hwnd);
        var doc=app.ActiveDocument;
        if(control.Range.Document!=doc || doc.ReadOnly || doc.TrackRevisions || doc.ProtectionType!=WordApi.WdProtectionType.wdNoProtection || control.LockContents || control.LockContentControl || control.Range.StoryType!=WordApi.WdStoryType.wdMainTextStory || control.Range.Information[WordApi.WdInformation.wdWithInTable] || app.UndoRecord.IsRecordingCustomRecord)throw new InvalidOperationException("managed-context-not-writable");
    }
    public static void Restore(WordApi.Application app, WordApi.ContentControl control, string faultAfter = "", Action? revalidate = null)
    {
        RequireManagedWrite(app,control);
        var snapshot = ReadUnique(app.ActiveDocument, control);
        var range = control.Range.Duplicate;
        var document=app.ActiveDocument;
        string beforeText=document.Content.Text;int beforeMath=document.OMaths.Count,beforeControls=document.ContentControls.Count;
        revalidate?.Invoke();RequireManagedWrite(app,control);
        bool changed=false,attempted=false;
        app.UndoRecord.StartCustomRecord("Locus: restore original source");
        try {
            // Replace OMML explicitly with a plain text run; Range.Text may keep math mode,
            // Delete(true) can smart-delete neighboring spaces, and OMath.Remove can split Undo.
            string controlId=control.ID;
            range.Select();
            attempted=true;
            app.Selection.InsertXML(Package("<w:r><w:t xml:space=\"preserve\">"+System.Security.SecurityElement.Escape(snapshot.OriginalSource)+"</w:t></w:r>"));
            Fault("RestoreReturnedBeforeFlag",faultAfter);changed=true; Fault("SourceRestored",faultAfter);
            foreach(WordApi.ContentControl item in app.ActiveDocument.ContentControls)if(item.ID==controlId){item.Delete(false);break;}
            Fault("ControlDetached",faultAfter);
        }
        catch {app.UndoRecord.EndCustomRecord(); if((changed||attempted&&(document.Content.Text!=beforeText||document.OMaths.Count!=beforeMath||document.ContentControls.Count!=beforeControls))&&!document.Undo(1))throw new InvalidOperationException("restore-rollback-failed"); throw;}
        finally { if(app.UndoRecord.IsRecordingCustomRecord)app.UndoRecord.EndCustomRecord(); }
    }
    public static void Detach(WordApi.Application app, WordApi.ContentControl control, Action? revalidate = null)
    {
        RequireManagedWrite(app,control);
        ReadUnique(app.ActiveDocument, control);
        revalidate?.Invoke();RequireManagedWrite(app,control);
        app.UndoRecord.StartCustomRecord("Locus: detach formula");
        try { control.Delete(false); }
        finally { app.UndoRecord.EndCustomRecord(); }
    }

    public static string ReplaceManaged(WordApi.Application app, WordApi.ContentControl control, ManagedSnapshot snapshot, Action revalidate, string faultAfter = "")
    {
        RequireManagedWrite(app, control);
        var document = app.ActiveDocument;
        var original = ReadUnique(document, control);
        if (original.EntryId != snapshot.EntryId || original.OriginalSource != snapshot.OriginalSource)
            throw new InvalidOperationException("managed-source-changed");
        string payload = snapshot.Encode();
        string nativePackage = Package(CandidateExporter.ToOmml(snapshot.Selected));
        var range = control.Range.Duplicate; int start = range.Start;
        string beforeText = document.Content.Text;
        string[] beforeTags = document.ContentControls.Cast<WordApi.ContentControl>().Select(c => c.Tag).ToArray();
        int beforeMath = document.OMaths.Count;
        string placeholder = "LOCUS_" + Guid.NewGuid().ToString("N");
        if (beforeText.Contains(placeholder)) throw new InvalidOperationException("replacement-marker-collision");
        string oldTag = control.Tag;
        revalidate(); RequireManagedWrite(app, control);
        if (control.Tag != oldTag || range.Start != start) throw new InvalidOperationException("metadata-changed");
        WordApi.ContentControl? inserted = null;
        app.UndoRecord.StartCustomRecord("Locus: update formula");
        try
        {
            // Word rejects an OMML package inserted inside an existing math container. Use the
            // same plain-run replacement as Restore, then insert the prepared result in this Undo.
            string controlId = control.ID;
            range.Select(); app.Selection.InsertXML(Package("<w:r><w:t>" + placeholder + "</w:t></w:r>"));
            Fault("IntermediateSourceWritten", faultAfter);
            foreach (WordApi.ContentControl item in document.ContentControls) if (item.ID == controlId) { item.Delete(false); break; }
            Fault("OldControlDetached", faultAfter);
            // Find returns Word's own range after its hidden math/control boundaries collapse.
            // No .NET string length is used to infer the replacement's Word positions.
            var restored = document.Content;
            if (!restored.Find.Execute(FindText: placeholder, MatchCase: true, MatchWildcards: false, Wrap: WordApi.WdFindWrap.wdFindStop) || restored.Text != placeholder || restored.OMaths.Count != 0)
                throw new InvalidOperationException("replacement-source-range-changed");
            start = restored.Start; restored.Select();
            app.Selection.InsertXML(nativePackage); Fault("NativeReplaced", faultAfter);
            var native = document.Range(start, app.Selection.End).OMaths;
            if (native.Count != 1) throw new InvalidOperationException("Replacement did not create exactly one equation.");
            inserted = document.ContentControls.Add(WordApi.WdContentControlType.wdContentControlRichText, native[1].Range);
            inserted.Title = "Locus formula"; inserted.Tag = payload;
            if (inserted.Tag != payload) throw new InvalidOperationException("Metadata readback differs.");
            Fault("ReplacementTagWritten", faultAfter);
        }
        catch
        {
            app.UndoRecord.EndCustomRecord();
            if ((beforeText != document.Content.Text || beforeMath != document.OMaths.Count || !beforeTags.SequenceEqual(document.ContentControls.Cast<WordApi.ContentControl>().Select(c => c.Tag))) && !document.Undo(1))
                throw new InvalidOperationException("replacement-rollback-failed");
            throw;
        }
        finally { if (app.UndoRecord.IsRecordingCustomRecord) app.UndoRecord.EndCustomRecord(); }
        try { ReadUnique(document, inserted!); PlaceCaretAfterEquation(document, app.Selection, inserted!); return inserted!.ID; }
        catch { if (!document.Undo(1)) throw new InvalidOperationException("replacement-verification-rollback-failed"); throw; }
    }
}
