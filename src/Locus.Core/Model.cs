using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Locus.Core.Domains;
using Locus.Core.Detection;

namespace Locus.Core
{
    public static class Versions
    {
        public const string Core = "locus-core/0.1";
        public const string Grammar = "vi-math-m0-proposal-0.1";
        public const string Contract = "locus-core-contract/0.1";
        public const string MathDocument = "math-document/0.1";
        public const string Snapshot = "locus-candidate-set/0.1";
    }

    public readonly struct TextSpan : IEquatable<TextSpan>
    {
        public int Start { get; }
        public int End { get; }
        public int Length => End - Start;
        public TextSpan(int start, int end)
        {
            if (start < 0 || end < start) throw new ArgumentOutOfRangeException(nameof(start));
            Start = start; End = end;
        }
        public bool Contains(TextSpan other) => Start <= other.Start && End >= other.End;
        public bool Equals(TextSpan other) => Start == other.Start && End == other.End;
        public override bool Equals(object? obj) => obj is TextSpan s && Equals(s);
        public override int GetHashCode() => unchecked(Start * 397 ^ End);
        public override string ToString() => $"[{Start},{End})";
    }

    public sealed class SourceSnapshot
    {
        public string Raw { get; }
        public long Revision { get; }
        public string Id { get; }
        public string SchemaVersion => Versions.Contract;
        public SourceSnapshot(string raw, long revision = 0)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            ValidateUnicode(raw); Revision = revision;
            Id = StableId.Compute(revision.ToString(CultureInfo.InvariantCulture), raw);
        }
        public string Slice(TextSpan span) { Validate(span); return Raw.Substring(span.Start, span.Length); }
        public void Validate(TextSpan span)
        {
            if (span.End > Raw.Length || !IsScalarBoundary(span.Start) || !IsScalarBoundary(span.End))
                throw new ArgumentException("Span must be inside the source and cannot split a UTF-16 surrogate pair.", nameof(span));
        }
        public bool IsScalarBoundary(int offset) => offset >= 0 && offset <= Raw.Length &&
            (offset == 0 || offset == Raw.Length || !char.IsHighSurrogate(Raw[offset - 1]) || !char.IsLowSurrogate(Raw[offset]));
        internal static void ValidateUnicode(string raw)
        {
            for (int i = 0; i < raw.Length; i++)
            {
                if (char.IsHighSurrogate(raw[i]))
                {
                    if (++i >= raw.Length || !char.IsLowSurrogate(raw[i])) throw new ArgumentException("Source contains an unpaired surrogate.", nameof(raw));
                }
                else if (char.IsLowSurrogate(raw[i])) throw new ArgumentException("Source contains an unpaired surrogate.", nameof(raw));
            }
        }
    }

    public sealed class Diagnostic
    {
        public string Code { get; }
        public string Severity { get; }
        public TextSpan Span { get; }
        public string Message { get; }
        public Diagnostic(string code, string severity, TextSpan span, string? message = null)
        {
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Diagnostic code required.", nameof(code));
            if (severity != "info" && severity != "warning" && severity != "error") throw new ArgumentException("Invalid severity.", nameof(severity));
            Code = code; Severity = severity; Span = span; Message = message ?? code;
        }
    }

    public sealed class SourceEdit
    {
        public TextSpan Span { get; }
        public string Text { get; }
        public SourceEdit(TextSpan span, string text)
        { Span = span; Text = text ?? throw new ArgumentNullException(nameof(text)); SourceSnapshot.ValidateUnicode(Text); }
        public static string Apply(SourceSnapshot source, IEnumerable<SourceEdit> edits)
        {
            var ordered = edits.OrderByDescending(e => e.Span.Start).ThenByDescending(e => e.Span.End).ToArray();
            int boundary = source.Raw.Length;
            var result = new StringBuilder(source.Raw);
            foreach (var edit in ordered)
            {
                source.Validate(edit.Span);
                if (edit.Span.End > boundary) throw new ArgumentException("Repair edits overlap.", nameof(edits));
                result.Remove(edit.Span.Start, edit.Span.Length).Insert(edit.Span.Start, edit.Text);
                boundary = edit.Span.Start;
            }
            return result.ToString();
        }
    }

    public sealed class MathNode
    {
        public string Type { get; }
        public string? Value { get; }
        public string? Name { get; }
        public string? Operator { get; }
        public IReadOnlyList<MathNode> Children { get; }
        public IReadOnlyList<TextSpan> SourceSpans { get; }
        public string Id { get; }
        public int Depth { get; }
        public MathNode(string type, IEnumerable<MathNode>? children = null, string? value = null,
            string? name = null, string? @operator = null, IEnumerable<TextSpan>? sourceSpans = null)
        {
            Type = type; Value = value; Name = name; Operator = @operator;
            Children = Freeze.Of(children); SourceSpans = Freeze.Of(sourceSpans);
            if (Children.Any(n => n == null)) throw new ArgumentException("Null child.");
            if (ScientificNodes.IsExtended(type)) ScientificNodes.Validate(this);
            else {
            int count = type == "Number" || type == "Symbol" ? 0 : type == "Unary" || type == "Sqrt" ? 1 :
                type == "Binary" || type == "Power" || type == "Relation" ? 2 : -1;
            if (count < 0 || Children.Count != count) throw new ArgumentException("Invalid node type or arity.");
            if (type == "Number" && (value == null || !Regex.IsMatch(value, @"\A[0-9]+(?:\.[0-9]+)?\z", RegexOptions.CultureInvariant)))
                throw new ArgumentException("Invalid canonical decimal.");
            if (type == "Symbol" && (name == null || name.Length != 1 || !(name[0] >= 'a' && name[0] <= 'z' || name[0] >= 'A' && name[0] <= 'Z')))
                throw new ArgumentException("Invalid symbol.");
            if ((type != "Number" && value != null) || (type != "Symbol" && name != null)) throw new ArgumentException("Unexpected node value/name.");
            var allowed = type == "Binary" ? new[] { "add", "subtract", "multiply", "divide" } :
                type == "Unary" ? new[] { "plus", "minus" } : type == "Relation" ? new[] { "eq", "lt", "gt", "le", "ge" } : Array.Empty<string>();
            if (allowed.Length == 0 ? @operator != null : !allowed.Contains(@operator)) throw new ArgumentException("Invalid node operator.");
            }
            Depth = Children.Count == 0 ? 1 : 1 + Children.Max(n => n.Depth);
            if (Depth > 128) throw new ArgumentException("AST depth exceeds 128.");
            Id = StableId.Compute(Type, Value ?? "", Name ?? "", Operator ?? "", string.Join(",", Children.Select(n => n.Id)), string.Join(",", SourceSpans));
        }
    }

    public sealed class MathDocument : ScientificDocument
    {
        public override string SchemaVersion => Versions.MathDocument;
        public override string Domain => "math";
        public override string CoreVersion => Versions.Core;
        public override string GrammarVersion => Versions.Grammar;
        public MathDocument(MathNode root) : base(root) { ScientificNodes.ValidateDocument(root, "math"); }
    }

    public sealed class MarkerConfiguration
    {
        public string Open { get; }
        public string Close { get; }
        public static MarkerConfiguration Default => new MarkerConfiguration("lc[", "]");
        public MarkerConfiguration(string open, string close)
        { Open = open ?? throw new ArgumentNullException(nameof(open)); Close = close ?? throw new ArgumentNullException(nameof(close)); }
        public bool IsValid => Open.Length > 0 && Close.Length > 0 && Open.Length <= 64 && Close.Length <= 64 &&
            !Open.StartsWith(Close, StringComparison.Ordinal) && !Close.StartsWith(Open, StringComparison.Ordinal) &&
            Open.IndexOfAny(new[] { '\r', '\n', '\\' }) < 0 && Close.IndexOfAny(new[] { '\r', '\n', '\\' }) < 0 && WellFormed(Open) && WellFormed(Close);
        private static bool WellFormed(string value)
        { try { SourceSnapshot.ValidateUnicode(value); return true; } catch (ArgumentException) { return false; } }
    }

    public sealed class Candidate
    {
        public string Id { get; }
        public string Kind { get; }
        public ScientificDocument Document { get; }
        public SourceSnapshot Source { get; }
        public TextSpan ContentSpan { get; }
        public TextSpan ReplacementSpan { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public IReadOnlyList<SourceEdit> Edits { get; }
        public string Provenance { get; }
        public string GrammarVersion => Document.GrammarVersion;
        public string CoreVersion => Document.CoreVersion;
        public Candidate(string kind, ScientificDocument document, SourceSnapshot source, TextSpan contentSpan, TextSpan replacementSpan,
            IEnumerable<Diagnostic>? diagnostics = null, IEnumerable<SourceEdit>? edits = null, string? provenance = null)
        {
            if (kind != "direct" && kind != "interpretation" && kind != "repair") throw new ArgumentException("Invalid candidate kind.");
            Document = document ?? throw new ArgumentNullException(nameof(document)); Source = source ?? throw new ArgumentNullException(nameof(source));
            source.Validate(contentSpan); source.Validate(replacementSpan);
            if (!replacementSpan.Contains(contentSpan)) throw new ArgumentException("Replacement must contain content.");
            Kind = kind; ContentSpan = contentSpan; ReplacementSpan = replacementSpan;
            Diagnostics = Freeze.Of(diagnostics); Edits = Freeze.Of(edits); Provenance = provenance ?? "grammar";
            foreach (var d in Diagnostics) source.Validate(d.Span);
            foreach (var e in Edits) { source.Validate(e.Span); if (!contentSpan.Contains(e.Span)) throw new ArgumentException("Repair outside content."); }
            if (kind == "repair" ? Edits.Count == 0 : Edits.Count != 0) throw new ArgumentException("Edits belong to repair candidates only, and repairs need edits.");
            if (Edits.Count > 0) SourceEdit.Apply(source, Edits);
            ValidateNode(Document.Root, source, contentSpan);
            Id = StableId.Compute(CoreVersion, GrammarVersion, source.Id, contentSpan.ToString(), replacementSpan.ToString(), kind, Document.Root.Id, Provenance,
                string.Join(",", Edits.Select(e => StableId.Compute(e.Span.ToString(), e.Text))),
                string.Join(",", Diagnostics.Select(d => StableId.Compute(d.Code, d.Severity, d.Span.ToString(), d.Message))));
        }
        private static void ValidateNode(MathNode node, SourceSnapshot source, TextSpan contentSpan)
        {
            if (node.SourceSpans.Count == 0) throw new ArgumentException("Every AST node needs original source references.");
            foreach (var span in node.SourceSpans) { source.Validate(span); if (!contentSpan.Contains(span)) throw new ArgumentException("Node reference outside content."); }
            foreach (var child in node.Children) ValidateNode(child, source, contentSpan);
        }
    }

    public sealed class CandidateSet
    {
        public string SchemaVersion => Candidates.Any(c => c.GrammarVersion == Assistance.SmartChemistryVersions.Grammar) ? Assistance.SmartChemistryVersions.Snapshot : Intent != null ? MarkerVersions.Snapshot : Candidates.Any(c => c.Document.Domain != "math") ? DomainVersions.Snapshot : Versions.Snapshot;
        public SourceSnapshot Source { get; }
        public TextSpan ContentSpan { get; }
        public TextSpan ReplacementSpan { get; }
        public string OriginalContent => Source.Slice(ContentSpan);
        public string OriginalReplacement => Source.Slice(ReplacementSpan);
        public IReadOnlyList<Candidate> Candidates { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public string? SelectedCandidateId { get; }
        public MarkerConfiguration? Markers { get; }
        public RegionIntent? Intent { get; }
        public string ContentEligibility => Candidates.Count == 1 && Candidates[0].Kind == "direct" &&
            !Diagnostics.Concat(Candidates[0].Diagnostics).Any(d => d.Severity == "warning" || d.Severity == "error") ? "eligible" : "blocked";
        public CandidateSet(SourceSnapshot source, TextSpan contentSpan, TextSpan replacementSpan, IEnumerable<Candidate> candidates,
            IEnumerable<Diagnostic>? diagnostics = null, string? selectedCandidateId = null, MarkerConfiguration? markers = null, RegionIntent? intent = null)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source)); source.Validate(contentSpan); source.Validate(replacementSpan);
            if (!replacementSpan.Contains(contentSpan)) throw new ArgumentException("Replacement must contain content.");
            ContentSpan = contentSpan; ReplacementSpan = replacementSpan; Candidates = Freeze.Of(candidates); Diagnostics = Freeze.Of(diagnostics);
            if (Candidates.Count > 3 || Candidates.Select(c => c.Id).Distinct().Count() != Candidates.Count) throw new ArgumentException("Candidate count/IDs invalid.");
            if (Candidates.Count(c => c.Kind == "direct") > 1 || Candidates.Skip(1).Any(c => c.Kind == "direct")) throw new ArgumentException("Direct must be first and unique.");
            if (Candidates.Any(c => c.Source.Id != source.Id || !c.ContentSpan.Equals(contentSpan) || !c.ReplacementSpan.Equals(replacementSpan))) throw new ArgumentException("Candidate snapshot mismatch.");
            foreach (var d in Diagnostics) source.Validate(d.Span);
            if (selectedCandidateId != null && !Candidates.Any(c => c.Id == selectedCandidateId)) throw new ArgumentException("Unknown selected candidate.");
            if (markers != null && !markers.IsValid) throw new ArgumentException("Invalid marker configuration.");
            if (markers != null && (contentSpan.Start != replacementSpan.Start + markers.Open.Length ||
                replacementSpan.End != contentSpan.End + markers.Close.Length ||
                source.Slice(replacementSpan) != markers.Open + source.Slice(contentSpan) + markers.Close))
                throw new ArgumentException("Stored markers must match the original source and wrapper coordinates.");
            if (intent != null && (markers == null || intent.Domain != "auto" && Candidates.Any(c => c.Document.Domain != intent.Domain)))
                throw new ArgumentException("Region intent must match its wrapper and candidate domains.");
            SelectedCandidateId = selectedCandidateId; Markers = markers; Intent = intent;
        }
        public CandidateSet Select(string id) => new CandidateSet(Source, ContentSpan, ReplacementSpan, Candidates, Diagnostics, id, Markers, Intent);
    }

    internal static class Freeze
    {
        public static IReadOnlyList<T> Of<T>(IEnumerable<T>? source) => new ReadOnlyCollection<T>((source ?? Enumerable.Empty<T>()).ToArray());
    }

    internal static class StableId
    {
        public static string Compute(params string[] parts)
        {
            var text = new StringBuilder();
            foreach (var part in parts) text.Append(part.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(part);
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
    }
}
