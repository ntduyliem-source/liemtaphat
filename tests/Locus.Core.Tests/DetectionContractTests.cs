using Locus.Core;
using Locus.Core.Detection;

public static class DetectionContractTests
{
    public static void Run(Action<string, Action> check)
    {
        check("invalid-marker-config-and-surrogates", () =>
        {
            var configs = new[] { new MarkerConfiguration("", "]"), new MarkerConfiguration("#", "#"),
                new MarkerConfiguration("<", "<<"), new MarkerConfiguration("a\nb", "c"),
                new MarkerConfiguration("a", "\\"), new MarkerConfiguration("\ud83d", "]"),
                new MarkerConfiguration("lc[", "\ude00"), new MarkerConfiguration(new string('a', 65), "]") };
            foreach (var config in configs)
            {
                var result = Analyze("😀 lc[x^2]", InputMode.Markers, config);
                Assert(result.Detection == DetectionStatus.Reject && result.Regions.Count == 0, "Invalid config produced a region.");
                Assert(Has(result, "INVALID_DELIMITER_CONFIG"), "Expected invalid-marker diagnostic.");
            }
        });
        check("literal-markers-not-regex", () =>
        {
            var result = Analyze("Trước $(7 trên 9)$ sau", InputMode.Markers, new MarkerConfiguration("$(", ")$"));
            One(result, "7 trên 9");
            Assert(result.Regions[0].OriginalReplacement == "$(7 trên 9)$", "Lost custom wrapper.");
            Assert(result.Regions[0].Markers?.Open == "$(", "Actual marker config was not captured.");
        });
        check("utf16-marker-whitespace-roundtrip", () =>
        {
            const string raw = "😀 lc[ x^5 ] sau";
            var source = new SourceSnapshot(raw);
            var result = new AnalysisEngine().Analyze(source, new AnalysisOptions(InputMode.Markers));
            One(result, " x^5 ");
            Assert(result.Regions[0].ContentSpan.Start == raw.IndexOf(" x^5 ", StringComparison.Ordinal), "Offset did not count surrogate pair.");
            Assert(result.Regions[0].OriginalReplacement == "lc[ x^5 ]", "Whitespace or marker lost.");
            Assert(ReferenceEquals(result.Source, source), "Input source must remain the same immutable snapshot.");
        });
        check("partial-close-does-not-convert", () =>
        {
            var result = Analyze("A <<x^7>", InputMode.Markers, new MarkerConfiguration("<<", ">>"));
            Assert(result.Regions.Count == 0 && result.IsIncomplete, "Partial close was considered complete.");
            Assert(Has(result, "UNCLOSED_MARKER"), "Missing unclosed diagnostic.");
        });
        check("nested-region-keeps-independent-neighbor", () =>
        {
            var result = Analyze("lc[x+lc[1]]; lc[y^3]", InputMode.Markers);
            One(result, "y^3");
            Assert(Has(result, "NESTED_MARKER_UNSUPPORTED"), "Nested region not diagnosed.");
            Assert(result.ContentEligibility == "blocked", "Mixed invalid input must not authorize bulk auto.");
            Assert(result.Regions[0].ContentEligibility == "eligible", "Independent region lost its own eligibility.");
        });
        check("escaped-region-keeps-independent-neighbor", () =>
        {
            var result = Analyze("\\lc[x^2] puis lc[y^3]", InputMode.Markers);
            One(result, "y^3");
            Assert(Has(result, "MARKER_ESCAPE_UNSUPPORTED"), "Escaped open not diagnosed.");
            Assert(result.Diagnostics.First(d => d.Code == "MARKER_ESCAPE_UNSUPPORTED").Span.Start == 0, "Escape diagnostic omitted backslash.");
        });
        check("url-prose-mixed-with-independent-formula", () =>
        {
            var result = Analyze("Xem https://e.test/x^2?q=a+1; tính z^3.", InputMode.Passive);
            One(result, "z^3");
            Assert(result.ContentEligibility == "eligible", "Protected prose must not taint independent valid math.");
        });
        check("passive-dates-stay-text-with-neighbor-formula", () =>
        {
            foreach (var domains in new[] { DetectionDomains.Math, DetectionDomains.All })
            foreach (var raw in new[] { "Bài thử máy 28/09: tính x^2.", "Ngày 1/2; tính x^2.", "Nga\u0300y 1/2; tính x^2.", "Ngày: 28/9/2026; tính x^2.", "Lịch 2026-09-28; tính x^2.", "Lịch 28.09.2026; tính x^2." })
            {
                var result = new AnalysisEngine().Analyze(new SourceSnapshot(raw), new AnalysisOptions(InputMode.Passive, enabledDomains: domains));
                One(result, "x^2");
            }
        });
        check("date-guard-preserves-explicit-and-mathematical-division", () =>
        {
            One(Analyze("28/09", InputMode.Explicit), "28/09");
            One(Analyze("lc[28/09]", InputMode.Markers), "28/09");
            foreach (var formula in new[] { "1/2", "28/9", "x+28/09", "28/09+x", "x + 28/09", "28/09 + x", "x = 28/09", "căn 28/09", "28/09 cộng x", "28/09 bằng x", "1/2x", "01-02", "1.02" })
            {
                var result = Analyze("Tính " + formula + ".", InputMode.Passive);
                if (formula == "1.02") Assert(result.Regions.Count == 0, "Lone decimal became a formula.");
                else One(result, formula);
            }
        });
        check("quoted-path-with-spaces-is-opaque", () =>
        {
            var result = Analyze("Mở \"C:\\bài tập\\x^2.txt\" rồi y+3.", InputMode.Passive);
            One(result, "y+3");
        });
        check("relative-path-does-not-leak-formula-suffix", () =>
        {
            var result = Analyze("Mở bai/x+1/2.txt rồi z+4.", InputMode.Passive);
            One(result, "z+4");
        });
        check("marker-in-url-never-shadows-independent-marker", () =>
        {
            var result = Analyze("https://e.test/lc[x^2] lc[y^3]", InputMode.Markers);
            One(result, "y^3");
        });
        check("opaque-identifiers-are-not-mined", () =>
        {
            foreach (var raw in new[] { "abc+x^2", "canx+2", "A1+x^2", "x_1+y^2", "abc + x^2", "abc+ x^2", "abc +x^2", "x^2 +abc", "x^2+ abc" })
            {
                var result = Analyze(raw, InputMode.Passive);
                Assert(result.Regions.Count == 0, "Mined a formula out of unsupported identifier/expression: " + raw);
            }
            One(Analyze("Mã A1; tính y^4.", InputMode.Passive), "y^4");
        });
        check("nfd-alias-and-nbsp-keep-raw-span", () =>
        {
            const string formula = "ca\u0306n\u00a05 cộng x";
            One(Analyze("Tính " + formula + ".", InputMode.Passive), formula);
        });
        check("compact-decimal-root-is-not-a-relative-path", () =>
        {
            One(Analyze("Tính can2.5/3.", InputMode.Passive), "can2.5/3");
        });
        check("newline-ends-passive-corridor", () =>
        {
            var result = Analyze("x+\r\n1; y^2", InputMode.Passive);
            One(result, "y^2");
        });
        check("region-count-limit-does-not-return-a-safe-looking-prefix", () =>
        {
            var result = new AnalysisEngine().Analyze(new SourceSnapshot("lc[x^2] lc[y^3]"), new AnalysisOptions(InputMode.Markers, maxRegions: 1));
            Assert(result.Regions.Count == 0 && result.ContentEligibility == "blocked" && Has(result, "REGION_COUNT_LIMIT"), "Region truncation looked eligible.");
        });
        check("source-and-region-size-limits", () =>
        {
            var raw = new SourceSnapshot("x^123456789");
            var sourceLimited = new AnalysisEngine().Analyze(raw, new AnalysisOptions(maxSourceLength: 4));
            var regionLimited = new AnalysisEngine().Analyze(raw, new AnalysisOptions(maxRegionLength: 4));
            Assert(sourceLimited.Regions.Count == 0 && Has(sourceLimited, "SOURCE_LENGTH_LIMIT"), "Source limit not enforced.");
            Assert(regionLimited.Regions.Count == 0 && Has(regionLimited, "REGION_LENGTH_LIMIT"), "Region limit not enforced.");
            Assert(sourceLimited.Source.Raw == raw.Raw && regionLimited.Source.Raw == raw.Raw, "Limit handling changed source.");
        });
        check("many-nested-openers-remain-one-rejected-region", () =>
        {
            string raw = string.Concat(Enumerable.Repeat("lc[", 1000)) + "x^2" + new string(']', 1000);
            var result = Analyze(raw, InputMode.Markers);
            Assert(result.Regions.Count == 0 && Has(result, "NESTED_MARKER_UNSUPPORTED"), "Nested input returned child formula.");
        });
        check("cancellation-propagates-without-result", () =>
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            bool cancelled = false;
            try { new AnalysisEngine().Analyze(new SourceSnapshot("lc[x^2]"), new AnalysisOptions(InputMode.Markers), cancellation.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            Assert(cancelled, "Cancellation was silently turned into an analysis result.");
        });
        check("unsupported-domain-does-not-pretend-to-be-algebra", () =>
        {
            var result = new AnalysisEngine().Analyze(new SourceSnapshot("H2SO4"), new AnalysisOptions(domain: "chemistry"));
            Assert(result.Detection == DetectionStatus.Deferred && result.Regions.Count == 0 && result.ContentEligibility == "pending", "Unimplemented domain returned math.");
        });
        check("repair-only-error-is-top-level-and-marker-preserved", () =>
        {
            var result = Analyze("lc[can(7+9]", InputMode.Markers);
            One(result, "can(7+9");
            Assert(result.Regions[0].Candidates[0].Kind == "repair" && result.ContentEligibility == "blocked", "Repair became automatic/direct.");
            Assert(Has(result, "MISSING_CLOSE_PAREN"), "Source diagnostic lost.");
            Assert(result.Regions[0].Diagnostics.Count == 0, "Source error should appear at analysis level in corpus projection.");
            Assert(result.Regions[0].OriginalReplacement == "lc[can(7+9]", "Source wrapper changed by repair.");
        });
        check("marker-ambiguity-keeps-set-warning-and-all-candidates", () =>
        {
            var result = Analyze("lc[7/9z]", InputMode.Markers);
            One(result, "7/9z");
            Assert(result.Regions[0].Candidates.Count == 2 && result.Regions[0].Candidates[1].Kind == "interpretation", "Ambiguity discarded.");
            Assert(result.Regions[0].Diagnostics.Any(d => d.Code == "AMBIGUOUS_IMPLICIT_DIVISION"), "Set ambiguity warning lost.");
            Assert(result.Diagnostics.Count == 0 && result.ContentEligibility == "blocked", "Ambiguity diagnostic scope/eligibility wrong.");
        });
        check("engine-has-no-shared-current-source-state", () =>
        {
            var engine = new AnalysisEngine();
            Parallel.For(1, 25, number =>
            {
                string content = "z^" + number;
                var result = engine.Analyze(new SourceSnapshot("lc[" + content + "]", number), new AnalysisOptions(InputMode.Markers));
                One(result, content);
                Assert(result.Source.Revision == number, "Concurrent analysis mixed source revisions.");
            });
        });
    }

    private static AnalysisResult Analyze(string raw, InputMode mode, MarkerConfiguration? markers = null) =>
        new AnalysisEngine().Analyze(new SourceSnapshot(raw), new AnalysisOptions(mode, markers: markers));
    private static bool Has(AnalysisResult result, string code) => result.Diagnostics.Any(d => d.Code == code);
    private static void One(AnalysisResult result, string content)
    {
        Assert(result.Detection == DetectionStatus.Accept && result.Regions.Count == 1, "Expected exactly one accepted region: " + content + "; got " + result.Regions.Count + "; " + string.Join(",", result.Diagnostics.Select(d => d.Code)));
        Assert(result.Regions[0].OriginalContent == content, "Wrong source boundary: " + result.Regions[0].OriginalContent);
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
