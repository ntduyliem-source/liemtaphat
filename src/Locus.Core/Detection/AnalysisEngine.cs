using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Locus.Core.Parsing;

namespace Locus.Core.Detection
{
    /// <summary>Locates complete source regions and delegates all mathematical interpretation to FormulaParser.</summary>
    public sealed class AnalysisEngine
    {
        private readonly FormulaParser parser;
        public AnalysisEngine() : this(new FormulaParser()) { }
        public AnalysisEngine(FormulaParser parser) { this.parser = parser ?? throw new ArgumentNullException(nameof(parser)); }

        public AnalysisResult Analyze(SourceSnapshot source, AnalysisOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            options = options ?? new AnalysisOptions();
            cancellationToken.ThrowIfCancellationRequested();
            var all = new TextSpan(0, source.Raw.Length);
            if (source.Raw.Length > options.MaxSourceLength) return Rejected(source, "SOURCE_LENGTH_LIMIT", all);
            if (!string.Equals(options.Domain, "math", StringComparison.Ordinal))
                return new AnalysisResult(source, DetectionStatus.Deferred, diagnostics: new[] { Error("UNSUPPORTED_DOMAIN", all) });
            if (options.MarkerProfiles != null) return ProfileAnalysisEngine.Analyze(source, options, cancellationToken);
            if (options.EnabledDomains != DetectionDomains.Math) return DomainAnalysisEngine.Analyze(source, options, cancellationToken);
            var protectedText = ProtectedTextRecognizer.Find(source.Raw, cancellationToken, options.InputMode == InputMode.Passive);
            var regions = new List<CandidateSet>();
            var diagnostics = new List<Diagnostic>();

            if (options.InputMode == InputMode.Markers)
            {
                var located = MarkerRegionLocator.Find(source, options, protectedText, cancellationToken);
                diagnostics.AddRange(located.Diagnostics);
                if (located.LimitExceeded) return new AnalysisResult(source, DetectionStatus.Reject, diagnostics: diagnostics);
                foreach (var region in located.Regions)
                    ParseRegion(source, region.ContentSpan, region.ReplacementSpan, options.Markers, regions, diagnostics, cancellationToken);
            }
            else if (options.InputMode == InputMode.Explicit)
            {
                if (protectedText.Count > 0)
                    diagnostics.AddRange(protectedText.Select(item => Error(item.Code, item.Span)));
                else if (all.Length > options.MaxRegionLength) diagnostics.Add(Error("REGION_LENGTH_LIMIT", all));
                else ParseRegion(source, all, all, null, regions, diagnostics, cancellationToken);
            }
            else
            {
                int attempted = 0;
                foreach (var span in PassiveRegionLocator.Find(source, protectedText, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (++attempted > options.MaxRegions) return Rejected(source, "REGION_COUNT_LIMIT", all);
                    if (span.Length > options.MaxRegionLength) { diagnostics.Add(Error("REGION_LENGTH_LIMIT", span)); continue; }
                    ParseRegion(source, span, span, null, regions, diagnostics, cancellationToken);
                }
            }
            if (regions.Count == 0 && diagnostics.Count == 0)
            {
                if (protectedText.Count > 0) diagnostics.AddRange(protectedText.Select(item => Error(item.Code, item.Span)));
                else diagnostics.Add(Error("INSUFFICIENT_MATH_EVIDENCE", all));
            }
            cancellationToken.ThrowIfCancellationRequested();
            bool incomplete = diagnostics.Any(d => d.Code == "UNCLOSED_MARKER" || d.Code == "MISSING_OPERAND" || d.Code == "MISSING_CLOSE_PAREN");
            return new AnalysisResult(source, regions.Count > 0 ? DetectionStatus.Accept : DetectionStatus.Reject,
                regions, diagnostics, incomplete);
        }

        private void ParseRegion(SourceSnapshot source, TextSpan contentSpan, TextSpan replacementSpan,
            MarkerConfiguration? markers, List<CandidateSet> regions, List<Diagnostic> diagnostics,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = parser.Parse(source, contentSpan, replacementSpan, cancellationToken);
            if (parsed.Candidates.Count == 0)
            {
                diagnostics.AddRange(parsed.Diagnostics);
                if (parsed.Diagnostics.Count == 0) diagnostics.Add(Error("PARSE_FAILED", contentSpan));
                return;
            }
            // Corpus places source parse errors at analysis level (e.g. a missing ')' with a repair),
            // while alternative-interpretation warnings belong to the candidate set.
            diagnostics.AddRange(parsed.Diagnostics.Where(d => d.Severity == "error"));
            bool hasDirect = parsed.Candidates.Any(c => c.Kind == "direct");
            var regionDiagnostics = parsed.Diagnostics.Where(d => d.Severity != "error" || hasDirect).ToArray();
            regions.Add(new CandidateSet(source, contentSpan, replacementSpan, parsed.Candidates,
                regionDiagnostics, parsed.SelectedCandidateId, markers));
        }

        private static AnalysisResult Rejected(SourceSnapshot source, string code, TextSpan span) =>
            new AnalysisResult(source, DetectionStatus.Reject, diagnostics: new[] { Error(code, span) });
        private static Diagnostic Error(string code, TextSpan span) => new Diagnostic(code, "error", span);
    }
}
