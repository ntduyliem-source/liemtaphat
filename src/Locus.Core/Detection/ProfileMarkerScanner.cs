using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Locus.Core.Detection
{
    public sealed class ProfileMarkerRegion
    {
        public TextSpan ContentSpan { get; }
        public TextSpan ReplacementSpan { get; }
        public MarkerProfile Profile { get; }
        public bool IsClosed { get; }
        internal ProfileMarkerRegion(TextSpan content, TextSpan replacement, MarkerProfile profile, bool closed)
        { ContentSpan = content; ReplacementSpan = replacement; Profile = profile; IsClosed = closed; }
    }

    public sealed class ProfileMarkerScan
    {
        public IReadOnlyList<ProfileMarkerRegion> Regions { get; }
        public IReadOnlyList<ProfileMarkerRegion> Drafts { get; }
        public IReadOnlyList<TextSpan> ReservedSpans { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public bool LimitExceeded { get; }
        internal ProfileMarkerScan(IEnumerable<ProfileMarkerRegion> regions, IEnumerable<ProfileMarkerRegion> drafts,
            IEnumerable<TextSpan> reserved, IEnumerable<Diagnostic> diagnostics, bool limit)
        { Regions = Freeze.Of(regions); Drafts = Freeze.Of(drafts); ReservedSpans = Freeze.Of(reserved); Diagnostics = Freeze.Of(diagnostics); LimitExceeded = limit; }
    }

    /// <summary>One bounded scan. Formula brackets and Locus wrappers have separate stacks.</summary>
    public static class ProfileMarkerScanner
    {
        private sealed class Frame
        {
            internal MarkerProfile Profile;
            internal Stack<char> Brackets = new Stack<char>();
            internal Frame(MarkerProfile profile) { Profile = profile; }
        }
        public static ProfileMarkerScan Scan(SourceSnapshot source, MarkerProfileSet profiles, int maxRegionLength = 4096,
            int maxRegions = 64, CancellationToken cancellationToken = default)
        {
            if (source == null || profiles == null) throw new ArgumentNullException();
            if (maxRegionLength < 1 || maxRegionLength > 4096 || maxRegions < 1 || maxRegions > 1024) throw new ArgumentOutOfRangeException();
            cancellationToken.ThrowIfCancellationRequested();
            var regions = new List<ProfileMarkerRegion>(); var drafts = new List<ProfileMarkerRegion>();
            var reserved = new List<TextSpan>(); var diagnostics = new List<Diagnostic>();
            var raw = source.Raw; bool exceeded = false;
            if (!profiles.IsValid)
                return new ProfileMarkerScan(regions, drafts, reserved, new[] { Error("INVALID_DELIMITER_CONFIG", new TextSpan(0, raw.Length), profiles.ValidationError!) }, false);
            var active = profiles.Profiles.Where(p => p.Enabled).ToArray();
            var protectedText = ProtectedTextRecognizer.Find(raw, cancellationToken);
            int pos = 0, protectedIndex = 0, count = 0;
            while (pos < raw.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (protectedIndex < protectedText.Count && protectedText[protectedIndex].Span.End <= pos) protectedIndex++;
                var profile = Find(raw, pos, active);
                if (protectedIndex < protectedText.Count && protectedText[protectedIndex].Span.Start <= pos &&
                    !(profile != null && pos == protectedText[protectedIndex].Span.Start))
                { pos = protectedText[protectedIndex++].Span.End; continue; }
                if (profile == null) { pos++; continue; }
                if (++count > maxRegions)
                { exceeded = true; diagnostics.Add(Error("REGION_COUNT_LIMIT", new TextSpan(pos, raw.Length))); break; }
                int start = pos, bodyStart = pos + profile.Markers.Open.Length, bodyEnd = raw.Length, end = raw.Length;
                bool nested = false, escaped = start > 0 && raw[start - 1] == '\\', mismatch = false, closed = false, depthLimit = false;
                var frames = new Stack<Frame>(); frames.Push(new Frame(profile)); pos = bodyStart;
                while (pos < raw.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var frame = frames.Peek(); var inner = Find(raw, pos, active);
                    if (inner != null)
                    {
                        nested = true;
                        if (frames.Count >= 128) { depthLimit = true; pos = raw.Length; break; }
                        frames.Push(new Frame(inner)); pos += inner.Markers.Open.Length; continue;
                    }
                    char ch = raw[pos];
                    if (frame.Brackets.Count > 0 && frame.Brackets.Peek() == ch)
                    { frame.Brackets.Pop(); pos++; continue; }
                    if (Matches(raw, pos, frame.Profile.Markers.Close))
                    {
                        mismatch |= frame.Brackets.Count > 0;
                        escaped |= pos > 0 && raw[pos - 1] == '\\';
                        bodyEnd = pos; pos += frame.Profile.Markers.Close.Length; frames.Pop();
                        if (frames.Count == 0) { closed = true; end = pos; break; }
                        continue;
                    }
                    char paired = ch == '(' ? ')' : ch == '[' ? ']' : ch == '{' ? '}' : '\0';
                    if (paired != '\0')
                    {
                        if (frame.Brackets.Count >= 128) { depthLimit = true; pos = raw.Length; break; }
                        frame.Brackets.Push(paired);
                    }
                    else if (ch == ')' || ch == ']' || ch == '}') mismatch = true;
                    pos++;
                }
                var replacement = new TextSpan(start, end);
                reserved.Add(new TextSpan(escaped && start > 0 && raw[start - 1] == '\\' ? start - 1 : start, end));
                var content = new TextSpan(bodyStart, closed ? bodyEnd : raw.Length);
                string? error = depthLimit ? "MARKER_DEPTH_LIMIT" : nested ? "NESTED_MARKER_UNSUPPORTED" : escaped ? "MARKER_ESCAPE_UNSUPPORTED" :
                    mismatch ? "MARKER_BRACKET_MISMATCH" : content.Length > maxRegionLength ? "REGION_LENGTH_LIMIT" :
                    source.Slice(content).IndexOfAny(new[] { '\r', '\n' }) >= 0 ? "UNSUPPORTED_MULTILINE_EXPRESSION" :
                    string.IsNullOrWhiteSpace(source.Slice(content)) ? "EMPTY_MARKED_REGION" : null;
                if (error != null) { diagnostics.Add(Error(error, replacement)); continue; }
                if (!closed)
                {
                    drafts.Add(new ProfileMarkerRegion(content, replacement, profile, false));
                    diagnostics.Add(Error("UNCLOSED_MARKER", replacement)); continue;
                }
                if (!source.IsScalarBoundary(start) || !source.IsScalarBoundary(bodyStart) || !source.IsScalarBoundary(bodyEnd) || !source.IsScalarBoundary(end))
                { diagnostics.Add(Error("INVALID_DELIMITER_BOUNDARY", replacement)); continue; }
                regions.Add(new ProfileMarkerRegion(content, replacement, profile, true));
            }
            return new ProfileMarkerScan(exceeded ? Array.Empty<ProfileMarkerRegion>() : (IEnumerable<ProfileMarkerRegion>)regions,
                exceeded ? Array.Empty<ProfileMarkerRegion>() : (IEnumerable<ProfileMarkerRegion>)drafts, reserved, diagnostics, exceeded);
        }
        private static MarkerProfile? Find(string raw, int pos, MarkerProfile[] profiles) => profiles.FirstOrDefault(p => Matches(raw, pos, p.Markers.Open));
        private static bool Matches(string raw, int pos, string text) => raw.Length - pos >= text.Length && string.Compare(raw, pos, text, 0, text.Length, StringComparison.Ordinal) == 0;
        private static Diagnostic Error(string code, TextSpan span, string message = "") => new Diagnostic(code, "error", span, message);
    }
}
