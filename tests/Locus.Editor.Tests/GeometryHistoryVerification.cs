using System.Reflection;
using Locus.Application;
using Locus.Editor;

internal static class GeometryHistoryVerification
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Invoke(GeometryWorkspace editor, string method, params object[] arguments) =>
        typeof(GeometryWorkspace).GetMethod(method, Private)!.Invoke(editor, arguments);
    private static void Set(GeometryWorkspace editor, string field, object value) => typeof(GeometryWorkspace).GetField(field, Private)!.SetValue(editor, value);
    private static GeometryWorkspace Editor(GeometryWorkspaceModel model)
    {
        var editor = new GeometryWorkspace();
        typeof(GeometryWorkspace).GetProperty("Model", Private)!.SetValue(editor, model);
        return editor;
    }

    public static async Task RunAsync()
    {
        var model = new GeometryWorkspaceModel();
        await using (var editor = Editor(model))
        {
            var session = model.Plane;
            session.AddPoint(new(1,1)); var complete = session.Document;
            session.BeginGesture(); string first = session.AddPoint(new(2,2));
            ((List<string>)typeof(GeometryWorkspace).GetField("pending", Private)!.GetValue(editor)!).Add(first);
            Invoke(editor, "History", false);
            Assert(session.Document == complete && !session.InGesture && !session.CanRedo, "R12: Undo of an unfinished segment must preserve completed work and create no Redo.");
            Assert(((List<string>)typeof(GeometryWorkspace).GetField("pending", Private)!.GetValue(editor)!).Count == 0, "R12: Cancelled construction retained pending vertices.");
            var saved = (GeometryDocument)DocumentCodec.Open(DocumentCodec.Serialize(session.Document)).Document!;
            Assert(saved.Points.Length == 1, "R12: Saving after cancel persisted the unfinished vertex.");
            Invoke(editor, "History", false);
            Assert(session.Document.Points.Length == 0 && session.CanRedo, "R12: The next Undo must affect the previous completed action.");
            Invoke(editor, "History", true);
            Assert(session.Document == complete, "R12: Redo must restore the previous completed action only.");

            session.BeginGesture(); Set(editor, "dragDocument", complete);
            session.Move(complete.Points[0].Id,new(5,6));
            Invoke(editor, "History", false);
            Assert(session.Document == complete && session.CanUndo && !session.CanRedo, "R12: Undo during drag must cancel that drag only.");
        }

        model = new GeometryWorkspaceModel();
        await using (var editor = Editor(model))
        {
            var session = model.Plane;
            session.AddPoint(new(1,1)); var first = session.Document;
            session.AddPoint(new(2,2)); var second = session.Document;
            Invoke(editor, "History", false);
            session.BeginGesture(); session.AddPoint(new(3,3)); var unfinished = session.Document;
            Invoke(editor, "History", true);
            Assert(session.Document == unfinished && session.InGesture && session.CanRedo, "R12: Redo during construction must neither cancel the gesture nor advance old history.");
            Invoke(editor, "Cancel"); // Same path as Escape.
            Assert(session.Document == first && !session.InGesture && session.CanRedo, "R12: Escape must preserve prior Redo history.");
            Invoke(editor, "History", true);
            Assert(session.Document == second, "R12: Redo must remain usable after Escape.");
            session.BeginGesture(); session.AddPoint(new(4,4));
            Invoke(editor, "Tool", "polygon");
            Assert(session.Document == second && !session.InGesture, "R12: Switching tools must cancel the unfinished action only.");
        }
        Console.WriteLine("PASS: R12 geometry Undo during construction/drag, Redo guard, Escape/tool change and saved state.");
    }
}
