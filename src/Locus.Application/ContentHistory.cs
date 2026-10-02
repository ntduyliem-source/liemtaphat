using System.Collections.ObjectModel;
using Locus.Core;
using Locus.Core.Serialization;

namespace Locus.Application;

/// <summary>A separate result snapshot. It never replaces the input or its original readings.</summary>
public sealed record ContentResultOverride(string BaseReadingId, CandidateSet Result, string CandidateId, string Provenance,
    bool ManagedBalance=false,ContentResultOverride? BalanceBefore=null,string? ProductProposal=null)
{
    public Candidate? Candidate => Result?.Candidates.FirstOrDefault(c=>c.Id==CandidateId);
    public ContentResultOverride Reanchor(string readingId)=>this with{BaseReadingId=readingId,BalanceBefore=BalanceBefore?.Reanchor(readingId)};
    internal void Validate(ContentRegion region)
    {
        if(BaseReadingId!=(region.SelectedId??"draft:"+region.Id)||region.Selected==null&&ProductProposal==null||Candidate is not {} candidate||candidate.Kind!="direct"||
            candidate.Document.Domain!=(region.Selected?.Document.Domain??"chemistry")||Result.Source.Raw.Length>FormulaSession.MaxSourceLength||
            string.IsNullOrWhiteSpace(Provenance)||Provenance.Length>256)
            throw new FormatException("Invalid result override.");
        if(BalanceBefore!=null){if(!ManagedBalance||BalanceBefore.ManagedBalance||BalanceBefore.BalanceBefore!=null)throw new FormatException("Nested balance history.");BalanceBefore.Validate(region);}
        if(ProductProposal!=null){var proposal=Locus.Core.Assistance.AssistanceSerializer.Deserialize(ProductProposal);if(proposal.Kind!="complete-reaction"||proposal.Region.Source.Raw.Substring(proposal.Region.ReplacementSpan.Start,proposal.Region.ReplacementSpan.Length)!=region.Raw||!ContentBalanceWire.SameSpecies(candidate,proposal.Result.Candidates[0]))throw new FormatException("Product source mismatch.");}
        if(ManagedBalance)
        {
            var before=BalanceBefore?.Candidate??region.Selected;
            if(candidate.Document.Domain!="chemistry"||candidate.Document.Root.Type!="ChemReaction"||before==null||before.Document.Root.Type!="ChemReaction"||
                !ContentBalanceWire.SameSpecies(before,candidate)||!Locus.Core.Assistance.ReactionBalancer.VerifyConservation(candidate.Document.Root)||
                ProductProposal!=BalanceBefore?.ProductProposal)throw new FormatException("Invalid managed balance.");
        }
    }
}

public sealed record ContentChange(ContentRegion Before, ContentRegion After);

/// <summary>Exact regional before/after data. Applying a command is a separate session transaction.</summary>
public sealed class ContentCommand
{
    public Guid Id { get; }
    public string Kind { get; }
    public IReadOnlyList<ContentChange> Changes { get; }
    public ContentCommand(Guid id,string kind,IEnumerable<ContentChange> changes)
    {
        Id=id;Kind=kind;Changes=new ReadOnlyCollection<ContentChange>(changes.ToArray());
        if(id==Guid.Empty||kind is not ("reading" or "keep-text" or "balance" or "cancel-balance" or "balance-all" or "cancel-balance-all" or "undo-batch" or "auto-balance" or "product" or "drop-product")||
            Changes.Count==0||Changes.Count>ContentDocument.RegionLimit)throw new FormatException("Invalid content command.");
        var ids=new HashSet<Guid>();
        foreach(var change in Changes)
        {
            if(change?.Before is not {} before||change.After is not {} after||!ids.Add(before.Id)||before.Id!=after.Id||before.Revision!=after.Revision||
                before.Origin!=after.Origin||before.Start!=after.Start||before.End!=after.End||before.Window!=after.Window||
                Snapshot(before.Readings)!=Snapshot(after.Readings))throw new FormatException("Command changed its source anchor.");
            ValidateRegion(before);ValidateRegion(after);
        }
    }
    internal static void ValidateRegion(ContentRegion r)
    {
        if(r.Window==null||r.Window.Length>FormulaSession.MaxSourceLength||r.Origin<0||r.Start<r.Origin||r.End<=r.Start||(long)r.End-r.Origin>r.Window.Length||
            r.Id==Guid.Empty||r.Revision<0||r.Revision==long.MaxValue||r.Problem?.Length>512)throw new FormatException("Invalid command region.");
        new SourceSnapshot(r.Window).Validate(new TextSpan(r.Start-r.Origin,r.End-r.Origin));
        if(r.Readings is {} readings&&(readings.Source.Raw!=r.Window||readings.ReplacementSpan.Start!=r.Start-r.Origin||readings.ReplacementSpan.End!=r.End-r.Origin))throw new FormatException("Invalid command snapshot.");
        if(r.SelectedId!=null&&r.Selected==null)throw new FormatException("Unknown command reading.");
        r.ResultOverride?.Validate(r);
    }
    private static string? Snapshot(CandidateSet? value)=>value==null?null:CandidateSetSerializer.Serialize(value);
}
