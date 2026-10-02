using System;
using System.Collections.Generic;
using System.Linq;
using Locus.Core.Detection;

namespace Locus.Core.Assistance
{
    public static class AssistanceVersions
    {
        public const string Contract = "locus-assistance/0.1";
        public const string Solver = "exact-balance/0.1";
        public const string Catalog = "reaction-catalog/0.1";
    }

    public sealed class ConditionFact
    {
        public string Key { get; }
        public string Value { get; }
        public ConditionFact(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 64 || key.Any(c => !(c >= 'a' && c <= 'z' || c == '-')) ||
                string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.IndexOfAny(new[] {'\r','\n'}) >= 0) throw new ArgumentException("Invalid condition fact.");
            SourceSnapshot.ValidateUnicode(value); Key = key; Value = value;
        }
    }

    public sealed class AssistanceContext
    {
        public string SettingsFingerprint { get; }
        public IReadOnlyList<ConditionFact> Conditions { get; }
        public string Id { get; }
        public AssistanceContext(string settingsFingerprint, IEnumerable<ConditionFact>? conditions = null)
        {
            if (settingsFingerprint == null || settingsFingerprint.Length != 64 || settingsFingerprint.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
                throw new ArgumentException("A settings fingerprint is required.");
            var facts = (conditions ?? Enumerable.Empty<ConditionFact>()).ToArray();
            if (facts.Length > 16 || facts.Any(f => f == null) || facts.Select(f => f.Key).Distinct(StringComparer.Ordinal).Count() != facts.Length)
                throw new ArgumentException("Invalid condition set.");
            SettingsFingerprint = settingsFingerprint;
            Conditions = Freeze.Of(facts.OrderBy(f => f.Key, StringComparer.Ordinal));
            Id = StableId.Compute(AssistanceVersions.Contract, settingsFingerprint, string.Join(",", Conditions.Select(f => StableId.Compute(f.Key, f.Value))));
        }
    }

    /// <summary>A complete formula body or an unfinished wrapper. Neither implies permission to accept assistance.</summary>
    public sealed class AssistanceRegion
    {
        public string Id { get; }
        public SourceSnapshot Source { get; }
        public TextSpan ContentSpan { get; }
        public TextSpan ReplacementSpan { get; }
        public MarkerConfiguration? Markers { get; }
        public RegionIntent? Intent { get; }
        public bool IsClosed { get; }
        public AssistanceRegion(SourceSnapshot source, TextSpan content, TextSpan replacement,
            MarkerConfiguration? markers = null, RegionIntent? intent = null, bool isClosed = true)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source)); source.Validate(content); source.Validate(replacement);
            if (source.Raw.Length > 4096 || content.Length == 0 || !replacement.Contains(content) || (intent != null && (markers == null || intent.Domain != "auto" && intent.Domain != "chemistry")))
                throw new ArgumentException("Invalid assistance region.");
            if (markers == null)
            {
                if (!isClosed || !replacement.Equals(content)) throw new ArgumentException("Unwrapped content has no pending closer.");
            }
            else if (!markers.IsValid || content.Start != replacement.Start + markers.Open.Length ||
                replacement.End != content.End + (isClosed ? markers.Close.Length : 0) || !isClosed && replacement.End != source.Raw.Length ||
                source.Slice(replacement) != markers.Open + source.Slice(content) + (isClosed ? markers.Close : ""))
                throw new ArgumentException("Assistance wrapper/source mismatch.");
            ContentSpan = content; ReplacementSpan = replacement; Markers = markers; Intent = intent; IsClosed = isClosed;
            Id = StableId.Compute(AssistanceVersions.Contract, source.Id, content.ToString(), replacement.ToString(), markers?.Open ?? "", markers?.Close ?? "", intent?.ProfileId ?? "", intent?.Domain ?? "", isClosed.ToString());
        }
    }

    public sealed class ReactionDraft
    {
        public AssistanceRegion Region { get; }
        public CandidateSet Reactants { get; }
        public TextSpan SeparatorSpan { get; }
        public string Arrow { get; }
        public string Id { get; }
        public ReactionDraft(AssistanceRegion region, CandidateSet reactants, TextSpan separatorSpan, string arrow)
        {
            Region = region ?? throw new ArgumentNullException(nameof(region)); Reactants = reactants ?? throw new ArgumentNullException(nameof(reactants));
            region.Source.Validate(separatorSpan);
            if (!region.ContentSpan.Contains(separatorSpan) || separatorSpan.Length == 0 || reactants.Source.Id != region.Source.Id ||
                !region.ContentSpan.Contains(reactants.ContentSpan) || reactants.ContentSpan.End > separatorSpan.Start || reactants.Candidates.Count != 1 ||
                reactants.Candidates[0].Kind != "direct" || reactants.Candidates[0].Document.Domain != "chemistry" || reactants.Candidates[0].Document.Root.Type == "ChemReaction" ||
                (arrow != "arrow" && arrow != "reversible") ||
                !string.IsNullOrWhiteSpace(region.Source.Raw.Substring(separatorSpan.End, region.ContentSpan.End - separatorSpan.End)) ||
                !string.IsNullOrWhiteSpace(region.Source.Raw.Substring(region.ContentSpan.Start, reactants.ContentSpan.Start - region.ContentSpan.Start)) ||
                !string.IsNullOrWhiteSpace(region.Source.Raw.Substring(reactants.ContentSpan.End, separatorSpan.Start - reactants.ContentSpan.End)))
                throw new ArgumentException("Invalid reaction draft.");
            var literal = region.Source.Slice(separatorSpan);
            if (arrow == "arrow" ? literal != "=" && literal != "->" && literal != "→" : literal != "<->" && literal != "⇌") throw new ArgumentException("Draft separator mismatch.");
            SeparatorSpan = separatorSpan; Arrow = arrow;
            Id = StableId.Compute(AssistanceVersions.Contract, region.Id, reactants.Candidates[0].Id, separatorSpan.ToString(), arrow);
        }
    }

    public sealed class AssistanceProvenance
    {
        public string SolverVersion { get; }
        public string? CatalogVersion { get; }
        public string? RuleId { get; }
        public IReadOnlyList<string> References { get; }
        public AssistanceProvenance(string solverVersion, string? catalogVersion = null, string? ruleId = null, IEnumerable<string>? references = null)
        {
            if (solverVersion != AssistanceVersions.Solver || catalogVersion != null && catalogVersion != AssistanceVersions.Catalog ||
                (catalogVersion == null) != (ruleId == null) || ruleId != null && (ruleId.Length == 0 || ruleId.Length > 128)) throw new ArgumentException("Unsupported assistance provenance.");
            var links = (references ?? Enumerable.Empty<string>()).ToArray();
            if (links.Length > 8 || links.Any(l => l == null || l.Length > 2048 || !Uri.TryCreate(l, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
                throw new ArgumentException("Invalid rule references.");
            SolverVersion = solverVersion; CatalogVersion = catalogVersion; RuleId = ruleId; References = Freeze.Of(links);
        }
    }

    /// <summary>The generated result owns a different source snapshot. Its spans never pretend to belong to the user's old text.</summary>
    public sealed class AssistanceProposal
    {
        public string Version => AssistanceVersions.Contract;
        public string Id { get; }
        public string Kind { get; }
        public AssistanceRegion Region { get; }
        public AssistanceContext Context { get; }
        public CandidateSet Result { get; }
        public CandidateSet? OriginalReading { get; }
        public AssistanceProvenance Provenance { get; }
        public string ReplacementText => Result.Source.Raw;
        public SourceEdit Edit => new SourceEdit(Region.ContentSpan, ReplacementText);
        public string AfterRaw => SourceEdit.Apply(Region.Source, new[] { Edit });
        public AssistanceProposal(string kind, AssistanceRegion region, AssistanceContext context, CandidateSet result,
            AssistanceProvenance provenance, CandidateSet? originalReading = null)
        {
            if (kind != "balance" && kind != "complete-reaction") throw new ArgumentException("Unsupported assistance kind.");
            Region = region ?? throw new ArgumentNullException(nameof(region)); Context = context ?? throw new ArgumentNullException(nameof(context));
            Result = result ?? throw new ArgumentNullException(nameof(result)); Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
            if (result.Source.Raw.Length > 4096 || result.Candidates.Count != 1 || result.Candidates[0].Kind != "direct" || result.Candidates[0].Document.Domain != "chemistry" ||
                result.Candidates[0].Document.Root.Type != "ChemReaction" || result.Markers != null || result.Intent != null ||
                !result.ContentSpan.Equals(new TextSpan(0, result.Source.Raw.Length)) || !result.ReplacementSpan.Equals(result.ContentSpan) ||
                result.Candidates[0].Diagnostics.Concat(result.Diagnostics).Any(d => d.Severity != "info") || result.Source.Raw == region.Source.Slice(region.ContentSpan))
                throw new ArgumentException("Invalid or duplicate assistance result.");
            var canonical = ChemistryProjection.Create(result.Candidates[0].Document.Root, result.Source.Revision);
            if (canonical.Source.Raw != result.Source.Raw || canonical.Candidates[0].Document.Root.Id != result.Candidates[0].Document.Root.Id)
                throw new ArgumentException("Result text and generated AST must be the same projection.");
            if (originalReading != null && (originalReading.Source.Id != region.Source.Id || !originalReading.ContentSpan.Equals(region.ContentSpan) ||
                originalReading.Candidates.Count != 1 || originalReading.Candidates[0].Kind != "direct" || originalReading.Candidates[0].Document.Domain != "chemistry"))
                throw new ArgumentException("Original reading/source mismatch.");
            if (kind == "balance" ? originalReading == null || provenance.CatalogVersion != null : provenance.CatalogVersion == null || provenance.References.Count == 0)
                throw new ArgumentException("Assistance kind/provenance mismatch.");
            Kind = kind; OriginalReading = originalReading;
            if (AfterRaw.Length > 4096) throw new ArgumentException("Assisted source exceeds the input budget.");
            Id = StableId.Compute(Version, kind, region.Source.Id, region.ContentSpan.ToString(), region.ReplacementSpan.ToString(),
                region.Markers?.Open ?? "", region.Markers?.Close ?? "", region.Intent?.ProfileId ?? "", region.Intent?.Domain ?? "", region.IsClosed.ToString(),
                context.Id, result.Candidates[0].Id, originalReading?.Candidates[0].Id ?? "", provenance.SolverVersion,
                provenance.CatalogVersion ?? "", provenance.RuleId ?? "", string.Join("\n", provenance.References));
        }
    }
}
