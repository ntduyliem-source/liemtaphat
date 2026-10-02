using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Locus.Word;
using WordApi = Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static Dictionary<string, object> AssistManual(Dictionary<string, object> state, string action, string argument = "") =>
        json.Deserialize<Dictionary<string, object>>((string)Manual.ChooseAssistance((string)state["sessionId"], (string)state["previewIdentity"], action, argument));
    private static void RunSmartNative()
    {
        // Uses the production manual entry point and its real focus/input guards.
        app.Visible = true; var anchor = app.Documents.Add(); anchor.Content.Text = "Locus E — native acceptance fixture.\r";
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Locus", "word-manual-settings.json");
        var savedBytes = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
        string savedSettings = (string)ManualState()["configuration"];
        try
        {
            Stage("smart-native/editor-prerequisite");
            var focusWait = System.Diagnostics.Stopwatch.StartNew();
            while (!((bool)((Dictionary<string, object>)ManualState()["input"])["EditorFocus"]) && focusWait.Elapsed.TotalSeconds < 40) System.Threading.Thread.Sleep(200);
            FocusManual();
            Manual.ConfigureProfiles(smartSettings.Serialize());
            foreach (var raw in new[] { "lc[x mu\u0303 2]", "toan-[1 trên 2]", "ly-[v=10 m/s]", "hoa-[3H2+O2=H2O]" })
            Test("smart-native/direct/" + raw, () => {
                var doc = ManualDocument(raw); string before = State(doc); var preview = PreviewManual();
                Converted(CompleteManual(preview)); var stored = WordOperations.ReadUnique(doc, doc.ContentControls[1]);
                Check(stored.OriginalSource == raw && stored.Selected.Id == (string)preview["candidateId"] && !stored.State.CanCancelBalance, "Direct source became assisted.");
                string inserted = State(doc); Check(doc.Undo(1) && State(doc) == before, "Direct Undo differs."); Check(doc.Redo(1) && State(doc) == inserted, "Redo differs.");
                Check((string)CompleteManual(ManageManual(doc.ContentControls[1]), "restore")["message"] == "restored" && State(doc) == before, "Source restore differs.");
            });
            Test("smart-native/balance-update-and-undo", () => {
                var doc = ManualDocument("hoa-[3H2+O2=H2O]"); string before = State(doc);
                Converted(CompleteManual(PreviewManual())); string direct = State(doc);
                var preview = AssistManual(ManageManual(doc.ContentControls[1]), "balance");
                string expected = (string)preview["previewIdentity"];
                var changed = CompleteManual(preview, "replace");
                if ((string)changed["message"] != "updated") Check(State(doc) == direct, "Failed update left a partial write.");
                Check((string)changed["message"] == "updated", "Update refused: " + json.Serialize(changed));
                Check(WordOperations.ReadUnique(doc, doc.ContentControls[1]).State.Identity == expected, "Updated native differs from preview.");
                string balanced = State(doc); Check(doc.Undo(1) && State(doc) == direct, "Update is not one Undo."); Check(doc.Redo(1) && State(doc) == balanced, "Update Redo differs.");
                var cancel = AssistManual(ManageManual(doc.ContentControls[1]), "cancel-balance");
                Check((string)CompleteManual(cancel, "replace")["message"] == "updated", "Cancel balance update failed.");
                var stored = WordOperations.ReadUnique(doc, doc.ContentControls[1]); Check(stored.State.KeepFromAuto && !stored.State.CanCancelBalance && stored.Selected.Id == Smart(stored.OriginalSource).Selected!.Id, "Cancel lost coefficients/ignore.");
                Check((string)CompleteManual(ManageManual(doc.ContentControls[1]), "restore")["message"] == "restored" && State(doc) == before, "Raw restore differs.");
            });
            Test("smart-native/products-save-reopen-without-addin", () => {
                var doc = ManualDocument("hoa-[3H2+O2=]"); string before = State(doc); var preview = PreviewManual();
                preview = AssistManual(preview, "condition", "activation=ignition");
                var proposal = (Dictionary<string, object>)((System.Collections.ArrayList)preview["proposals"])[0];
                preview = AssistManual(preview, "preview-product", (string)proposal["Id"]);
                preview = AssistManual(preview, "accept-product", (string)proposal["Id"]);
                preview = AssistManual(preview, "cancel-balance"); Converted(CompleteManual(preview));
                string tag = doc.ContentControls[1].Tag;
                Check(WordOperations.ReadUnique(doc, doc.ContentControls[1]).State.Result!.Source.Raw == "3H2+O2->H2O", "Products/cancel differ.");
                string path = Path.Combine(directory, "products-cancelled.docx"); doc.SaveAs2(path, WordApi.WdSaveFormat.wdFormatXMLDocument, AddToRecentFiles: false); Close(doc);
                app.COMAddIns.Item("Locus.Word.Manual").Connect = false;
                doc = app.Documents.Open(path); documents.Add(doc);
                Check(doc.OMaths.Count == 1 && doc.ContentControls[1].Tag == tag, "Native disappeared without Locus."); Close(doc);
                app.COMAddIns.Item("Locus.Word.Manual").Connect = true;
                doc = app.Documents.Open(path); documents.Add(doc);
                Check(WordOperations.ReadUnique(doc, doc.ContentControls[1]).Encode() == tag, "Saved metadata changed.");
                Check((string)CompleteManual(ManageManual(doc.ContentControls[1]), "restore")["message"] == "restored" && State(doc) == before, "Draft restore differs.");
            });
            Test("smart-native/stale-snapshot-and-native-drift", () => {
                var doc = ManualDocument("hoa-[3H2+O2=H2O]"); var initial = PreviewManual(); var balanced = AssistManual(initial, "balance");
                Reject(() => AssistManual(initial, "cancel-balance")); string before = State(doc);
                Manual.ConfirmPreview((string)balanced["sessionId"], "convert", (string)initial["candidateId"]);
                Check(State(doc) == before, "Old displayed candidate wrote document.");
                var preview = PreviewManual(); Converted(CompleteManual(preview));
                doc.ContentControls[1].Range.OMaths[1].Range.Text = "H2+O2";
                before = State(doc); Reject(() => WordOperations.ReadUnique(doc, doc.ContentControls[1])); Check(State(doc) == before, "Drift check wrote document.");
            });
        }
        finally
        {
            try { Manual.CancelPreview(); Manual.ConfigureProfiles(savedSettings); } finally {
                if (savedBytes != null) File.WriteAllBytes(settingsPath, savedBytes); else if (File.Exists(settingsPath)) File.Delete(settingsPath);
                try { anchor.Close(WordApi.WdSaveOptions.wdDoNotSaveChanges); } catch (System.Runtime.InteropServices.COMException error) { Write("cleanup-warning.json", new { error.Message }); }
            }
        }
    }
}
