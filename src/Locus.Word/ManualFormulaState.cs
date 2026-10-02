using System;
using System.Linq;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Serialization;

namespace Locus.Word;

// The Word capsule stores immutable core results, not instructions to run the solver on reopen.
public sealed class ManualFormulaState
{
    public CandidateSet? Readings { get; }
    public ReactionDraft? Draft { get; }
    public AssistanceProposal? Products { get; }
    public CandidateSet? Result { get; }
    public CandidateSet? BeforeBalance { get; }
    public bool KeepFromAuto { get; }
    public string OriginalSource => Readings?.OriginalReplacement ?? Draft!.Region.Source.Slice(Draft.Region.ReplacementSpan);
    public Candidate? Selected => Result?.Candidates.Single(c => c.Id == Result.SelectedCandidateId) ?? Readings?.Candidates.Single(c => c.Id == Readings.SelectedCandidateId);
    public bool CanCancelBalance => BeforeBalance != null;
    public string Identity => ManualStateCodec.Digest(ManualStateCodec.Serialize(this));

    public ManualFormulaState(CandidateSet? readings, ReactionDraft? draft = null, AssistanceProposal? products = null,
        CandidateSet? result = null, CandidateSet? beforeBalance = null, bool keepFromAuto = false)
    {
        Readings = readings; Draft = draft; Products = products; Result = result; BeforeBalance = beforeBalance; KeepFromAuto = keepFromAuto;
        Validate();
    }
    public static ManualFormulaState FromReadings(CandidateSet readings) => new ManualFormulaState(readings.Select(readings.SelectedCandidateId ?? readings.Candidates[0].Id));
    private static CandidateSet Choose(CandidateSet set) => set.Select(set.Candidates[0].Id);
    private static bool Same(CandidateSet a, CandidateSet b) => CandidateSetSerializer.Serialize(a) == CandidateSetSerializer.Serialize(b);
    private static bool SameProjection(Candidate a, Candidate b) => ChemistryProjection.Create(a.Document.Root).Source.Raw == ChemistryProjection.Create(b.Document.Root).Source.Raw;

    private void Validate()
    {
        if ((Readings == null) == (Draft == null) || Readings != null && Readings.SelectedCandidateId == null ||
            OriginalSource.Length == 0 || OriginalSource.Length > 4096 || OriginalSource.IndexOfAny(new[] {'\r','\n','\a'}) >= 0)
            throw new FormatException("Invalid Word source snapshot.");
        var source = Readings?.Source ?? Draft!.Region.Source;
        var replacement = Readings?.ReplacementSpan ?? Draft!.Region.ReplacementSpan;
        if (replacement.Start != 0 || replacement.End != source.Raw.Length || Draft != null && !Draft.Region.IsClosed)
            throw new FormatException("Select one complete Word source region.");
        if (Products != null && (Draft == null || Products.Kind != "complete-reaction" || Products.Region.Id != Draft.Region.Id))
            throw new FormatException("Product/source mismatch.");
        if (Result == null)
        {
            if (BeforeBalance != null || Products != null) throw new FormatException("Missing assisted result.");
            return;
        }
        var selected = Selected!;
        if (Result.Candidates.Count != 1 || Result.SelectedCandidateId == null || selected.Kind != "direct" || selected.Document.Domain != "chemistry" ||
            selected.Document.Root.Type != "ChemReaction" || Result.Markers != null || Result.Intent != null ||
            !Result.ContentSpan.Equals(new TextSpan(0, Result.Source.Raw.Length)) || !Result.ReplacementSpan.Equals(Result.ContentSpan) ||
            Result.Diagnostics.Concat(selected.Diagnostics).Any(d => d.Severity != "info"))
            throw new FormatException("Invalid assisted Word result.");
        // A freshly projected result has its own source and cannot borrow spans from the typed source.
        if (!Same(Result, Choose(ChemistryProjection.Create(selected.Document.Root, Result.Source.Revision))))
            throw new FormatException("Result projection differs.");
        CandidateSet baseline;
        if (Products != null) baseline = Choose(ChemistryProjection.ProductsBeforeBalance(Draft!, Products));
        else if (Readings != null) baseline = Readings;
        else throw new FormatException("Unaccepted product result.");
        var before = baseline.Candidates.Single(c => c.Id == baseline.SelectedCandidateId);
        if (!ChemistryProjection.SameSpecies(before, selected)) throw new FormatException("Assistance changed chemical species.");
        if (BeforeBalance != null)
        {
            if (!Same(BeforeBalance, baseline) || KeepFromAuto || SameProjection(before, selected) || !ReactionBalancer.VerifyConservation(selected.Document.Root))
                throw new FormatException("Invalid pre-balance snapshot.");
        }
        else if (Products == null || !Same(Result, baseline)) throw new FormatException("Untracked result change.");
    }
    public ManualFormulaState SelectReading(string id)
    {
        if (Readings == null) throw new InvalidOperationException("This source is a reaction draft.");
        return new ManualFormulaState(Readings.Select(id), keepFromAuto: true);
    }
    public ManualFormulaState Balance(out string message)
    {
        if (CanCancelBalance) throw new InvalidOperationException("Balance is already managed.");
        var candidate = Selected;
        if (candidate == null || candidate.Kind != "direct" || candidate.Document.Domain != "chemistry" ||
            candidate.Diagnostics.Concat((Result ?? Readings)!.Diagnostics).Any(d => d.Severity == "warning" || d.Severity == "error"))
            throw new InvalidOperationException("Chọn phương trình Hóa rõ nghĩa trước khi cân bằng.");
        if (candidate.Document.Root.Type == "ChemReaction" && ReactionBalancer.VerifyConservation(candidate.Document.Root))
        { message = "Phương trình đã cân bằng; Locus chưa đổi hệ số."; return this; }
        var balance = ReactionBalancer.Balance(candidate.Document);
        message = balance.Message;
        return balance.Status != BalanceStatus.Balanced ? this : new ManualFormulaState(Readings, Draft, Products, Choose(balance.Result!), Result ?? Readings);
    }
    public ManualFormulaState CancelBalance()
    {
        if (BeforeBalance == null) throw new InvalidOperationException("No managed balance to cancel.");
        return new ManualFormulaState(Readings, Draft, Products, Products == null ? null : BeforeBalance, keepFromAuto: true);
    }
    public ManualFormulaState AcceptProducts(AssistanceProposal proposal)
    {
        if (Draft == null || Products != null || proposal.Region.Id != Draft.Region.Id) throw new InvalidOperationException("Product draft changed.");
        var intermediate = Choose(ChemistryProjection.ProductsBeforeBalance(Draft, proposal));
        var result = Choose(proposal.Result);
        bool changed = !SameProjection(intermediate.Candidates[0], result.Candidates[0]);
        return new ManualFormulaState(null, Draft, proposal, changed ? result : intermediate, changed ? intermediate : null);
    }
    public ManualFormulaState DropProducts()
    {
        if (Products == null) throw new InvalidOperationException("No accepted products.");
        return new ManualFormulaState(null, Draft, keepFromAuto: true);
    }
}
