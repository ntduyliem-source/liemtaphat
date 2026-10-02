using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Locus.Core.Detection
{
    internal static class ProfileAnalysisEngine
    {
        internal static AnalysisResult Analyze(SourceSnapshot source, AnalysisOptions options, CancellationToken token)
        {
            var scan = ProfileMarkerScanner.Scan(source, options.MarkerProfiles!, options.MaxRegionLength, options.MaxRegions, token);
            var diagnostics = scan.Diagnostics.ToList(); var regions = new List<CandidateSet>();
            var all = new TextSpan(0, source.Raw.Length);
            if (scan.LimitExceeded || !options.MarkerProfiles!.IsValid)
                return new AnalysisResult(source, DetectionStatus.Reject, diagnostics: diagnostics);
            // No registered wrapper: preserve the existing parsing and candidate identity exactly.
            if (scan.ReservedSpans.Count == 0 && options.InputMode != InputMode.Markers)
                return (options.EnabledDomains & DetectionDomains.Chemistry) != 0 ? DomainAnalysisEngine.Analyze(source, LegacyOptions(options), token, true) : new AnalysisEngine().Analyze(source, LegacyOptions(options), token);
            if (options.InputMode == InputMode.Explicit && scan.ReservedSpans.Count > 0 &&
                (scan.ReservedSpans.Count != 1 || scan.ReservedSpans[0].Start != 0 || scan.ReservedSpans[0].End != source.Raw.Length))
                return new AnalysisResult(source, DetectionStatus.Reject, diagnostics: new[] { new Diagnostic("EXPLICIT_WRAPPER_SCOPE", "error", all, "Dùng một cặp cho toàn bộ nguồn, hoặc chọn cách đọc vùng/câu.") });
            foreach (var region in scan.Regions)
                Parse(region.ContentSpan, region.ReplacementSpan, region.Profile.Resolve(options.EnabledDomains), region.Profile);

            if (options.InputMode == InputMode.Passive && options.EnabledDomains != DetectionDomains.None)
            {
                var protectedText = ProtectedTextRecognizer.Find(source.Raw, token, protectDates: true);
                if ((options.EnabledDomains & DetectionDomains.Physics) != 0)
                    protectedText.RemoveAll(p => DomainAnalysisEngine.QuantityUnitContext(source, p, token));
                protectedText.AddRange(scan.ReservedSpans.Select(s => new ProtectedText(s, "PROTECTED_MARKER")));
                var merged = Merge(protectedText);
                var spans = options.EnabledDomains == DetectionDomains.Math ? PassiveRegionLocator.Find(source, merged, token) :
                    DomainAnalysisEngine.Corridors(source, merged, options.EnabledDomains, token, true);
                int attempts = scan.ReservedSpans.Count;
                foreach (var span in spans)
                {
                    if (++attempts > options.MaxRegions)
                        return new AnalysisResult(source, DetectionStatus.Reject, diagnostics: new[] { new Diagnostic("REGION_COUNT_LIMIT", "error", all) });
                    Parse(span, span, options.EnabledDomains, null);
                }
            }
            if (regions.Count == 0 && diagnostics.Count == 0)
                diagnostics.Add(new Diagnostic(options.EnabledDomains == DetectionDomains.None ? "DETECTION_DISABLED" : "INSUFFICIENT_FORMULA_EVIDENCE", "info", all));
            token.ThrowIfCancellationRequested();
            return new AnalysisResult(source, regions.Count > 0 ? DetectionStatus.Accept : DetectionStatus.Reject,
                regions.OrderBy(r => r.ReplacementSpan.Start), diagnostics,
                scan.Drafts.Count > 0 || diagnostics.Any(d => d.Code.Contains("MISSING") || d.Code == "UNCLOSED_MARKER"));

            void Parse(TextSpan content, TextSpan replacement, DetectionDomains domains, MarkerProfile? profile)
            {
                token.ThrowIfCancellationRequested();
                if (domains == DetectionDomains.None) { diagnostics.Add(new Diagnostic("DETECTION_DISABLED", "info", content)); return; }
                if (content.Length > options.MaxRegionLength) { diagnostics.Add(new Diagnostic("REGION_LENGTH_LIMIT", "error", content)); return; }
                // Classify the body itself, so wrappers cannot hide an address; unit exemptions stay domain-specific.
                var opaque = ProtectedTextRecognizer.Find(source.Slice(content), token)
                    .Select(p => new ProtectedText(new TextSpan(content.Start + p.Span.Start, content.Start + p.Span.End), p.Code)).ToList();
                if ((domains & DetectionDomains.Physics) != 0) opaque.RemoveAll(p => DomainAnalysisEngine.QuantityUnitContext(source, p, token));
                if (opaque.Count > 0) { diagnostics.AddRange(opaque.Select(p => new Diagnostic(p.Code, "error", p.Span))); return; }
                var parsed = DomainAnalysisEngine.Route(source, content, replacement, domains, token, true);
                if (parsed.Candidates.Count == 0) { diagnostics.AddRange(parsed.Diagnostics); return; }
                if (profile == null && options.InputMode == InputMode.Passive && !DomainAnalysisEngine.HasPassiveEvidence(parsed, source, content, token)) return;
                // A marker limits the domain; it does not suppress ambiguity/repair diagnostics.
                regions.Add(new CandidateSet(source, content, replacement, parsed.Candidates, parsed.Diagnostics,
                    parsed.SelectedCandidateId, profile?.Markers, profile == null ? null : new RegionIntent(profile.Id, profile.Domain)));
            }
        }
        private static AnalysisOptions LegacyOptions(AnalysisOptions o) => new AnalysisOptions(o.InputMode, o.Domain, o.Markers,
            o.MaxSourceLength, o.MaxRegionLength, o.MaxRegions, o.EnabledDomains);
        private static List<ProtectedText> Merge(List<ProtectedText> items)
        {
            var result = new List<ProtectedText>();
            foreach (var item in items.OrderBy(p => p.Span.Start))
            {
                if (result.Count == 0 || result[result.Count - 1].Span.End < item.Span.Start) result.Add(item);
                else
                {
                    var previous = result[result.Count - 1];
                    result[result.Count - 1] = new ProtectedText(new TextSpan(previous.Span.Start, Math.Max(previous.Span.End, item.Span.End)), "PROTECTED_MARKER");
                }
            }
            return result;
        }
    }
}
