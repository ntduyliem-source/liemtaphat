using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Locus.Core;
using Locus.Core.Assistance;

namespace Locus.Application;

public readonly record struct EditorSelection(int Start, int End)
{
    public void Validate(SourceSnapshot source) => source.Validate(new TextSpan(Start, End));
}

public sealed class AcceptedTransformation
{
    public AssistanceProposal Proposal { get; }
    public FormulaState Before { get; }
    public SourceSnapshot After { get; }
    public EditorSelection SelectionBefore { get; }
    public EditorSelection SelectionAfter { get; }
    public bool AppendedSpace { get; }
    public AcceptedTransformation(AssistanceProposal proposal, FormulaState before, SourceSnapshot after, EditorSelection selectionBefore, EditorSelection selectionAfter, bool appendedSpace = false)
    {
        ArgumentNullException.ThrowIfNull(proposal);ArgumentNullException.ThrowIfNull(before);ArgumentNullException.ThrowIfNull(after);
        if(before.Transformations!=null)throw new ArgumentException("A transformation stores its source state without recursively nesting history.");
        DocumentCodec.ValidateFormula(before,new());
        if(before.Raw!=proposal.Region.Source.Raw||before.SourceRevision!=proposal.Region.Source.Revision||AssistanceHistory.SettingsFingerprint(before.Settings)!=proposal.Context.SettingsFingerprint)
            throw new ArgumentException("Transformation source/context mismatch.");
        selectionBefore.Validate(proposal.Region.Source);selectionAfter.Validate(after);
        int caret=proposal.Region.ReplacementSpan.Start+(proposal.Region.Markers?.Open.Length??0)+proposal.ReplacementText.Length+(proposal.Region.IsClosed?proposal.Region.Markers?.Close.Length??0:0);
        var expected=proposal.AfterRaw;
        if(appendedSpace)expected=expected.Insert(caret++," ");
        if(after.Raw!=expected||selectionAfter.Start!=caret||selectionAfter.End!=caret||after.Id==proposal.Region.Source.Id)
            throw new ArgumentException("Transformation result/caret mismatch.");
        Proposal=proposal;Before=before;After=after;SelectionBefore=selectionBefore;SelectionAfter=selectionAfter;AppendedSpace=appendedSpace;
    }
}

public sealed class AssistanceHistory
{
    public const int Limit=16;
    public IReadOnlyList<AcceptedTransformation> Entries { get; }
    public AssistanceHistory(IEnumerable<AcceptedTransformation> entries)
    {
        var items=entries.ToArray();
        if(items.Length==0||items.Length>Limit||items.Any(i=>i==null))throw new ArgumentException("Invalid transformation history.");
        Entries=new ReadOnlyCollection<AcceptedTransformation>(items);
    }
    public static string SettingsFingerprint(FormulaSettings settings)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(AnalysisWire.Request(new("",0,settings)))));
    public static AssistanceContext Context(FormulaSettings settings,IEnumerable<ConditionFact>? conditions=null)=>new(SettingsFingerprint(settings),conditions);
}
