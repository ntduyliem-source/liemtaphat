using System;
using System.Linq;
using Locus.Word;
using WordApi = Microsoft.Office.Interop.Word;

internal static partial class Program
{
    private static string ReplaceSmart(ManagedSnapshot snapshot, string fault = "")
    {
        dynamic research = app.COMAddIns.Item("Locus.Word.W0").Object;
        return research.RunSandboxResearchOperation("replace", snapshot.Encode(), app.ActiveDocument.ContentControls[1].ID, fault);
    }
    private static void RunSmartTransactions()
    {
        // Transaction tests only. The research connector writes exclusively to its own fixtures.
        // These rows do not stand in for ManualConnect's focus/preview acceptance.
        foreach (var fault in new[] { "IntermediateSourceWritten", "OldControlDetached", "NativeReplaced", "ReplacementTagWritten" })
        Test("smart-transaction/rollback/" + fault, () => {
            var state = Smart("hoa-[3H2+O2=H2O]"); var doc = New(state.OriginalSource); var original = new ManagedSnapshot(state); Convert(original);
            string before = State(doc); var update = new ManagedSnapshot(state.Balance(out _), original.EntryId);
            RejectReason(() => ReplaceSmart(update, fault), "W0_FAULT:" + fault);
            Check(State(doc) == before && WordOperations.ReadUnique(doc, doc.ContentControls[1]).Encode() == original.Encode(), "Fault left a partial update.");
        });
        foreach (var prefix in new[] { "", "  ", "😀 e\u0301\t" })
        Test("smart-transaction/outside-text/" + prefix, () => {
            var state = Smart("hoa-[3H2+O2=H2O]"); var doc = New(state.OriginalSource);
            doc.Content.Text = prefix + state.OriginalSource + prefix + "End.\r";
            var source = doc.Content; Check(source.Find.Execute(FindText:state.OriginalSource, MatchCase:true, MatchWildcards:false), "Missing fixture source."); source.Select();
            var original = new ManagedSnapshot(state); Convert(original); string before = State(doc);
            var control = doc.ContentControls[1]; string left = doc.Range(0, control.Range.Start).Text, right = doc.Range(control.Range.End, doc.Content.End).Text;
            var update = new ManagedSnapshot(state.Balance(out _), original.EntryId); ReplaceSmart(update); control = doc.ContentControls[1];
            Check(WordOperations.ReadUnique(doc,control).Encode() == update.Encode() && left == doc.Range(0,control.Range.Start).Text && right == doc.Range(control.Range.End,doc.Content.End).Text, "Update changed outside text.");
            string after = State(doc); Check(doc.Undo(1) && State(doc) == before, "One Undo did not restore the old formula."); Check(doc.Redo(1) && State(doc) == after, "Redo differs.");
        });
        Test("smart-transaction/legacy-upgrade-and-repair", () => {
            var state = Smart("x+1/2"); var doc = New(state.OriginalSource); var old = new ManagedSnapshot(state.Readings!); Convert(old);
            var repair = state.SelectReading(state.Readings!.Candidates.Last().Id); var update = new ManagedSnapshot(repair, old.EntryId);
            string before = State(doc); ReplaceSmart(update);
            Check(WordOperations.ReadUnique(doc,doc.ContentControls[1]).Selected.Kind == "repair", "Selected repair lost.");
            Check(doc.Undo(1) && State(doc) == before && doc.ContentControls[1].Tag.StartsWith(ManagedSnapshot.Prefix, StringComparison.Ordinal), "Legacy metadata did not survive Undo.");
        });
        Test("smart-transaction/refuse-foreign-source", () => {
            var state = Smart("hoa-[3H2+O2=H2O]"); var doc = New(state.OriginalSource); var old = new ManagedSnapshot(state); Convert(old);
            string before = State(doc);
            Reject(() => ReplaceSmart(new ManagedSnapshot(Smart("hoa-[H2+Cl2=HCl]"), old.EntryId)));
            Check(State(doc) == before, "Foreign source replaced the formula.");
        });
    }
}
