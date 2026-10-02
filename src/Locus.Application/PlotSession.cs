using Locus.Core.Plotting;

namespace Locus.Application;

/// <summary>Shared immutable scene history. Pointer gestures commit once; Esc restores the exact prior scene.</summary>
public sealed class PlotSession
{
    public PlotDocument Document { get; private set; } = new(Guid.NewGuid(), 0, [], new(), [], new(), PlotExpression.Grammar);
    private readonly List<PlotDocument> undo = [], redo = [];
    private PlotDocument? gesture;
    public long Version { get; private set; }
    public bool CanUndo => undo.Count > 0 || gesture != null;
    public bool CanRedo => redo.Count > 0;
    public bool InGesture => gesture != null;
    public void BeginGesture() { gesture ??= Document; }
    public void EndGesture(bool cancel = false)
    {
        if (gesture == null) return;
        if (cancel) { Document = gesture; Version++; }
        else if (gesture != Document) Push(gesture);
        gesture = null;
    }
    public void Apply(PlotDocument next)
    {
        next = next with { Id = Document.Id, Revision = Document.Revision + 1, Grammar = PlotExpression.Grammar };
        DocumentCodec.ValidatePlot(next);
        if (gesture == null) Push(Document);
        Document = next; Version++;
    }
    private void Push(PlotDocument before) { undo.Add(before); if (undo.Count > 80) undo.RemoveAt(0); redo.Clear(); }
    public bool History(bool forward)
    {
        EndGesture(); var from = forward ? redo : undo; var to = forward ? undo : redo;
        if (from.Count == 0) return false;
        to.Add(Document); Document = from[^1]; from.RemoveAt(from.Count - 1); Version++; return true;
    }
    public void Load(PlotDocument document)
    {
        DocumentCodec.ValidatePlot(document); gesture = null; undo.Clear(); redo.Clear();
        Document = document with { Parameters = document.Parameters ?? [], Axes = document.Axes ?? new(), Grammar = PlotExpression.Grammar }; Version++;
    }
    public void AddCurve(string raw = "")
    {
        if (Document.Curves.Length == 32) throw new FormatException("Mỗi tài liệu hiện hỗ trợ 32 đường.");
        string[] colors = ["#28705c", "#426bc1", "#b14f43", "#9959b5", "#bd8121"];
        Apply(Document with { Curves = [..Document.Curves, new(Guid.NewGuid().ToString("N"), raw, colors[Document.Curves.Length % colors.Length])] });
    }
    public void UpdateCurve(PlotCurve curve) => Apply(Document with { Curves = Document.Curves.Select(c => c.Id == curve.Id ? curve : c).ToArray() });
    public void UpdateParameter(PlotParameter parameter)
    {
        var values = (Document.Parameters ?? []).ToDictionary(p => p.Name, StringComparer.Ordinal); values[parameter.Name] = parameter;
        Apply(Document with { Parameters = values.Values.ToArray() });
    }
    public string Serialize()
    {
        var curves = Document.Curves.Select(c => { try { return c with { AstSnapshot = PlotExpression.Parse(c.InterpretedRaw ?? c.Raw).Snapshot }; } catch (PlotParseException) { return c with { AstSnapshot = null }; } }).ToArray();
        return DocumentCodec.Serialize(Document with { Curves = curves });
    }
}
