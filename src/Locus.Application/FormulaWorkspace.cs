namespace Locus.Application;

/// <summary>Owns the document while its visual tab is mounted, closed, or reopened within one host.</summary>
public sealed class FormulaWorkspace : IDisposable
{
    public FormulaSession Session { get; }
    public FormulaView View { get; set; } = new();
    public Guid DocumentId { get; set; } = Guid.NewGuid();
    public long DocumentRevision { get; set; }
    public bool Initialized { get; set; }
    public bool AutoSave { get; set; } = true;
    public bool AcceptChemistrySpace { get; set; }
    public string DraftStatus { get; set; } = "";
    public string SerializeDocument() => DocumentCodec.Serialize(new FormulaDocument(DocumentId, DocumentRevision, Session.State, View));
    public void Open(FormulaDocument document)
    {
        Session.Load(document.State);
        DocumentId = document.Id; DocumentRevision = document.Revision; View = document.View;
    }
    public FormulaWorkspace(IAnalysisScheduler scheduler)
    {
        Session = new(scheduler);
        Session.UpdateSource("x mũ 2 + 1");
    }
    public void Dispose() => Session.Dispose();
}
