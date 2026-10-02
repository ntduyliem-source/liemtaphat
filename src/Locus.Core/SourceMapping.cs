using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace Locus.Core
{
    public sealed class MappingSegment
    {
        public string Kind { get; }
        public IReadOnlyList<TextSpan> SourceSpans { get; }
        public IReadOnlyList<TextSpan> NormalizedSpans { get; }
        public MappingSegment(string kind, IEnumerable<TextSpan> sourceSpans, IEnumerable<TextSpan> normalizedSpans)
        {
            if (!new[] { "identity", "compose", "expand", "replace", "elide", "synthetic" }.Contains(kind)) throw new ArgumentException("Invalid map kind.");
            Kind = kind; SourceSpans = Freeze.Of(sourceSpans); NormalizedSpans = Freeze.Of(normalizedSpans);
            ValidateOrder(SourceSpans); ValidateOrder(NormalizedSpans);
        }
        private static void ValidateOrder(IReadOnlyList<TextSpan> spans)
        {
            for (int i = 1; i < spans.Count; i++) if (spans[i].Start < spans[i - 1].End) throw new ArgumentException("Mapping spans must be ordered and disjoint.");
        }
    }

    public sealed class NormalizedSource
    {
        public SourceSnapshot Source { get; }
        public string Text { get; }
        public IReadOnlyList<MappingSegment> Mappings { get; }
        private NormalizedSource(SourceSnapshot source, string text, IEnumerable<MappingSegment> mappings)
        { Source = source; Text = text; Mappings = Freeze.Of(mappings); }

        public static NormalizedSource Create(SourceSnapshot source, CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            cancellationToken.ThrowIfCancellationRequested();
            var text = new StringBuilder(); var maps = new List<MappingSegment>();
            int start = 0;
            // Group canonical combining sequences (including Hangul composition) independently of runtime grapheme tables.
            while (start < source.Raw.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int end = ScalarEnd(source.Raw, start);
                while (end < source.Raw.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var category = CharUnicodeInfo.GetUnicodeCategory(source.Raw, end);
                    bool mark = category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark || category == UnicodeCategory.EnclosingMark;
                    bool hangul = !mark && CanComposeHangul(source.Raw.Substring(start, end - start), source.Raw[end]);
                    if (!mark && !hangul) break;
                    end = ScalarEnd(source.Raw, end);
                }
                string raw = source.Raw.Substring(start, end - start);
                cancellationToken.ThrowIfCancellationRequested();
                string normalized = NormalizeOperators(raw.Normalize(NormalizationForm.FormC));
                cancellationToken.ThrowIfCancellationRequested();
                var ns = new TextSpan(text.Length, text.Length + normalized.Length);
                string kind = raw == normalized ? "identity" : raw.Length > normalized.Length ? "compose" : raw.Length < normalized.Length ? "expand" : "replace";
                maps.Add(new MappingSegment(kind, new[] { new TextSpan(start, end) }, new[] { ns }));
                text.Append(normalized); start = end;
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new NormalizedSource(source, text.ToString(), maps);
        }

        public IReadOnlyList<TextSpan> ProjectToSource(TextSpan normalizedSpan)
        {
            if (normalizedSpan.End > Text.Length || !Boundary(Text, normalizedSpan.Start) || !Boundary(Text, normalizedSpan.End))
                throw new ArgumentException("Invalid normalized span.");
            if (normalizedSpan.Length == 0)
            {
                if (normalizedSpan.Start == Text.Length) return Freeze.Of(new[] { new TextSpan(Source.Raw.Length, Source.Raw.Length) });
                var segment = Mappings.First(m => m.NormalizedSpans.Any(s => s.Start <= normalizedSpan.Start && s.End > normalizedSpan.Start));
                var ns = segment.NormalizedSpans[0]; var ss = segment.SourceSpans[0];
                if (segment.Kind == "identity") return Freeze.Of(new[] { new TextSpan(ss.Start + normalizedSpan.Start - ns.Start, ss.Start + normalizedSpan.Start - ns.Start) });
                if (normalizedSpan.Start == ns.Start) return Freeze.Of(new[] { new TextSpan(ss.Start, ss.Start) });
                // No unique source caret exists inside a composed/expanded segment. Return its complete provenance.
                return segment.SourceSpans;
            }
            var result = new List<TextSpan>();
            foreach (var segment in Mappings)
            {
                foreach (var ns in segment.NormalizedSpans)
                {
                    if (ns.End <= normalizedSpan.Start || ns.Start >= normalizedSpan.End) continue;
                    if (segment.Kind == "identity" && segment.SourceSpans.Count == 1)
                    {
                        var ss = segment.SourceSpans[0];
                        AddMerged(result, new TextSpan(ss.Start + Math.Max(ns.Start, normalizedSpan.Start) - ns.Start,
                            ss.Start + Math.Min(ns.End, normalizedSpan.End) - ns.Start));
                    }
                    else foreach (var ss in segment.SourceSpans) AddMerged(result, ss);
                }
            }
            return Freeze.Of(result);
        }

        private static void AddMerged(List<TextSpan> result, TextSpan span)
        {
            if (result.Count > 0 && result[result.Count - 1].End >= span.Start)
            { var last = result[result.Count - 1]; result[result.Count - 1] = new TextSpan(last.Start, Math.Max(last.End, span.End)); }
            else result.Add(span);
        }
        private static int ScalarEnd(string raw, int start) => start + (char.IsHighSurrogate(raw[start]) ? 2 : 1);
        private static bool Boundary(string text, int p) => p >= 0 && p <= text.Length && (p == 0 || p == text.Length || !char.IsHighSurrogate(text[p - 1]) || !char.IsLowSurrogate(text[p]));
        private static bool CanComposeHangul(string raw, char next)
        {
            string nfc = raw.Normalize(NormalizationForm.FormC);
            if (nfc.Length != 1) return false;
            int previous = nfc[0];
            return (previous >= 0x1100 && previous <= 0x1112 && next >= 0x1161 && next <= 0x1175) ||
                (previous >= 0xac00 && previous <= 0xd7a3 && (previous - 0xac00) % 28 == 0 && next >= 0x11a8 && next <= 0x11c2);
        }
        private static string NormalizeOperators(string text)
        {
            var output = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '\u2212') output.Append('-');
                else if (c == '\u00d7' || c == '\u00b7') output.Append('*');
                else if (c == '\u00f7') output.Append('/');
                else if (c == '\u2264') output.Append("<=");
                else if (c == '\u2265') output.Append(">=");
                else if (c != '\r' && c != '\n' && char.IsWhiteSpace(c)) output.Append(' ');
                else output.Append(c);
            }
            return output.ToString();
        }
    }
}
