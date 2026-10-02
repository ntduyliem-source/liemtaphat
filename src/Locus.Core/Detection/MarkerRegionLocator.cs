using System;
using System.Collections.Generic;
using System.Threading;

namespace Locus.Core.Detection
{
    internal sealed class MarkerRegion
    {
        public TextSpan ContentSpan { get; }
        public TextSpan ReplacementSpan { get; }
        public MarkerRegion(TextSpan contentSpan, TextSpan replacementSpan)
        { ContentSpan = contentSpan; ReplacementSpan = replacementSpan; }
    }

    internal sealed class MarkerScanResult
    {
        public List<MarkerRegion> Regions { get; } = new List<MarkerRegion>();
        public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();
        public bool LimitExceeded { get; set; }
    }

    internal static class MarkerRegionLocator
    {
        public static MarkerScanResult Find(SourceSnapshot source, AnalysisOptions options,
            IReadOnlyList<ProtectedText> protectedText, CancellationToken cancellationToken)
        {
            var result = new MarkerScanResult();
            var config = options.Markers;
            string raw = source.Raw;
            if (!IsValidConfiguration(config))
            {
                result.Diagnostics.Add(Error("INVALID_DELIMITER_CONFIG", new TextSpan(0, raw.Length)));
                return result;
            }
            int position = 0, protectedIndex = 0, regionCount = 0;
            while (position < raw.Length)
            {
                if ((position & 127) == 0) cancellationToken.ThrowIfCancellationRequested();
                while (protectedIndex < protectedText.Count && protectedText[protectedIndex].Span.End <= position) protectedIndex++;
                if (protectedIndex < protectedText.Count && protectedText[protectedIndex].Span.Start <= position)
                { position = protectedText[protectedIndex++].Span.End; continue; }
                if (!Matches(raw, position, config.Open)) { position++; continue; }
                if (++regionCount > options.MaxRegions)
                {
                    result.Regions.Clear(); result.LimitExceeded = true;
                    result.Diagnostics.Add(Error("REGION_COUNT_LIMIT", new TextSpan(position, raw.Length)));
                    return result;
                }
                int start = position, bodyStart = position + config.Open.Length;
                bool nested = false, escaped = position > 0 && raw[position - 1] == '\\';
                int diagnosticStart = escaped ? start - 1 : start;
                int depth = 1, bodyEnd = -1, end = -1;
                position = bodyStart;
                // Scan once across the entire outer region. Repeated opening markers do not cause
                // repeated IndexOf searches for the same distant close (quadratic nested input).
                while (position < raw.Length)
                {
                    if ((position & 127) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (Matches(raw, position, config.Open))
                    { nested = true; depth++; position += config.Open.Length; continue; }
                    if (Matches(raw, position, config.Close))
                    {
                        escaped |= position > 0 && raw[position - 1] == '\\';
                        depth--; bodyEnd = position; position += config.Close.Length;
                        if (depth == 0) { end = position; break; }
                        continue;
                    }
                    position++;
                }
                var replacement = new TextSpan(start, end < 0 ? raw.Length : end);
                var diagnosticSpan = new TextSpan(diagnosticStart, replacement.End);
                if (nested) { result.Diagnostics.Add(Error("NESTED_MARKER_UNSUPPORTED", diagnosticSpan)); continue; }
                if (escaped) { result.Diagnostics.Add(Error("MARKER_ESCAPE_UNSUPPORTED", diagnosticSpan)); continue; }
                if (end < 0) { result.Diagnostics.Add(Error("UNCLOSED_MARKER", diagnosticSpan)); continue; }
                if (!source.IsScalarBoundary(start) || !source.IsScalarBoundary(bodyStart) ||
                    !source.IsScalarBoundary(bodyEnd) || !source.IsScalarBoundary(end))
                { result.Diagnostics.Add(Error("INVALID_DELIMITER_BOUNDARY", new TextSpan(0, raw.Length))); continue; }
                var contentSpan = new TextSpan(bodyStart, bodyEnd);
                if (contentSpan.Length > options.MaxRegionLength)
                { result.Diagnostics.Add(Error("REGION_LENGTH_LIMIT", replacement)); continue; }
                string content = source.Slice(contentSpan);
                if (string.IsNullOrWhiteSpace(content))
                { result.Diagnostics.Add(Error("EMPTY_MARKED_REGION", replacement)); continue; }
                if (content.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                { result.Diagnostics.Add(Error("UNSUPPORTED_MULTILINE_EXPRESSION", contentSpan)); continue; }
                result.Regions.Add(new MarkerRegion(contentSpan, replacement));
            }
            return result;
        }

        public static bool IsValidConfiguration(MarkerConfiguration config)
        {
            if (!config.IsValid) return false;
            try { SourceSnapshot.ValidateUnicode(config.Open); SourceSnapshot.ValidateUnicode(config.Close); }
            catch (ArgumentException) { return false; }
            return true;
        }
        private static bool Matches(string raw, int position, string marker) =>
            raw.Length - position >= marker.Length && string.Compare(raw, position, marker, 0, marker.Length, StringComparison.Ordinal) == 0;
        private static Diagnostic Error(string code, TextSpan span) => new Diagnostic(code, "error", span);
    }
}
