using Locus.Core;
using Locus.Core.Parsing;

/// <summary>Held-out contracts: general grammar behavior and adversarial boundaries.</summary>
internal static class ParserContractTests
{
    private static readonly FormulaParser Parser = new();

    internal static void Run(Action<string, Action> check)
    {
        check("parser-bare-root-and-power-heldout", () =>
        {
            Equal("Binary:add(Sqrt(Power(Number:7,Number:3)),Number:5)", Shape(First("sqrt7^3+5")));
            Equal("Power(Sqrt(Number:7),Number:3)", Shape(First("sqrt(7)^3")));
            Equal("Unary:minus(Power(Symbol:b,Unary:minus(Power(Number:3,Number:2))))", Shape(First("-b^-3^2")));
        });

        check("parser-implicit-denominator-chain-heldout", () =>
        {
            var result = Parse("1/2z(y+8)");
            Equal(2, result.Candidates.Count);
            Equal("Binary:multiply(Binary:multiply(Binary:divide(Number:1,Number:2),Symbol:z),Binary:add(Symbol:y,Number:8))", Shape(result.Candidates[0].Document.Root));
            Equal("Binary:divide(Number:1,Binary:multiply(Binary:multiply(Number:2,Symbol:z),Binary:add(Symbol:y,Number:8)))", Shape(result.Candidates[1].Document.Root));
            True(result.Diagnostics.Any(d => d.Code == "AMBIGUOUS_IMPLICIT_DIVISION"), "Ambiguity diagnostic missing.");
            Equal("blocked", result.ContentEligibility);
        });

        check("parser-multiple-ambiguity-sites-heldout", () =>
        {
            foreach (string text in new[] { "7/3z+8/5y", "sqrt(4/3x)+2/7y", "2/3x/4y" })
                Rejects(text, "AMBIGUITY_LIMIT");
        });

        check("parser-repair-edits-reproduce-semantic-candidate", () =>
        {
            foreach (string text in new[] { "t + 23 / 5", "sqrt(t+23/5)", "căn z cộng 12", "3*can(7+8" })
            {
                const string prefix = "😀 ghi ";
                const string suffix = " kết thúc";
                var raw = prefix + text + suffix;
                var source = new SourceSnapshot(raw);
                var content = new TextSpan(prefix.Length, prefix.Length + text.Length);
                var result = Parser.Parse(source, content);
                var repairs = result.Candidates.Where(c => c.Kind == "repair").ToArray();
                True(repairs.Length > 0, "Expected a declared repair for " + text);
                foreach (var repair in repairs)
                {
                    var amended = SourceEdit.Apply(source, repair.Edits);
                    int change = repair.Edits.Sum(edit => edit.Text.Length - edit.Span.Length);
                    var reparsed = Parser.Parse(new SourceSnapshot(amended), new TextSpan(content.Start, content.End + change));
                    True(reparsed.Candidates.Count > 0 && reparsed.Candidates[0].Kind == "direct", "Repair edits did not form a direct parse.");
                    Equal(Shape(repair.Document.Root), Shape(reparsed.Candidates[0].Document.Root));
                    Equal(prefix, amended[..prefix.Length]);
                    Equal(suffix, amended[(amended.Length - suffix.Length)..]);
                    Equal(raw, result.Source.Raw);
                }
            }
        });

        check("parser-parentheses-suppress-scope-repairs", () =>
        {
            foreach (string text in new[] { "(t+23)/5", "t+(23/5)", "sqrt(z+12)", "sqrt((t+23)/5)" })
            {
                var result = Parse(text);
                Equal(1, result.Candidates.Count);
                Equal("direct", result.Candidates[0].Kind);
            }
        });

        check("parser-candidate-budget-does-not-combine-repairs", () =>
        {
            var result = Parse("sqrt(a+1/7)+sqrt(b+2/9)+sqrt(c+3/8)");
            Equal(3, result.Candidates.Count);
            Equal("direct", result.Candidates[0].Kind);
            True(result.Candidates.Skip(1).All(c => c.Kind == "repair" && c.Edits.Count == 2), "Expected one pair of parentheses per independent repair.");
            Equal(3, result.Candidates.Select(c => c.Id).Distinct().Count());
            Equal("blocked", result.ContentEligibility);
        });

        check("parser-nfd-token-spans-use-original-utf16", () =>
        {
            string raw = "😀 lc[z mu\u0303 12]";
            int start = raw.IndexOf('z');
            int end = raw.LastIndexOf(']');
            var source = new SourceSnapshot(raw);
            var result = Parser.Parse(source, new TextSpan(start, end), new TextSpan(3, raw.Length));
            var power = result.Candidates[0].Document.Root;
            Equal("Power(Symbol:z,Number:12)", Shape(power));
            Equal(new TextSpan(start, start + 1), power.Children[0].SourceSpans.Single());
            int number = raw.IndexOf("12", StringComparison.Ordinal);
            Equal(new TextSpan(number, number + 2), power.Children[1].SourceSpans.Single());
            var lexical = new FormulaTokenizer().Tokenize(source, new TextSpan(start, end));
            var keyword = lexical.Tokens.Single(t => t.Kind == FormulaTokenKind.Power);
            Equal("mu\u0303", source.Slice(keyword.Span));
            Equal("mũ", keyword.NormalizedText);
            Equal(raw, source.Raw);
        });

        check("parser-long-flat-and-nested-depth-boundaries", () =>
        {
            Equal(64, First(string.Join("+", Enumerable.Repeat("x", 64))).Depth);
            Rejects(string.Join("+", Enumerable.Repeat("x", 65)), "MAX_DEPTH_EXCEEDED");
            Equal("Symbol:z", Shape(First(new string('(', 63) + "z" + new string(')', 63))));
            Rejects(new string('(', 64) + "z" + new string(')', 64), "MAX_DEPTH_EXCEEDED");
        });

        check("parser-region-and-tokenizer-size-boundaries", () =>
        {
            Equal(4096, First(new string('7', FormulaParser.MaximumRegionLength)).Value!.Length);
            Rejects(new string('7', FormulaParser.MaximumRegionLength + 1), "INPUT_TOO_LONG");
            var source = new SourceSnapshot(new string('7', FormulaParser.MaximumRegionLength + 1));
            var tokenized = new FormulaTokenizer().Tokenize(source, new TextSpan(0, source.Raw.Length));
            True(tokenized.Diagnostics.Any(d => d.Code == "INPUT_TOO_LONG"), "Public tokenizer must share the bounded-input contract.");
        });

        check("parser-unsupported-scientific-notation-stays-rejected", () =>
        {
            foreach (string text in new[] { "1e-2", "23E+7", "4.2e3", "5,2E-9", "2e−3", "5,2E−9" })
                Rejects(text, "UNSUPPORTED_SCIENTIFIC_NOTATION");
            Equal("Binary:subtract(Binary:multiply(Number:2,Symbol:e),Number:3)", Shape(First("2*e-3")));
            Equal("Binary:subtract(Binary:multiply(Number:2,Symbol:e),Number:3)", Shape(First("2*e−3")));
            Equal("Binary:multiply(Number:2,Symbol:e)", Shape(First("2e")));
        });

        check("parser-malformed-numbers-and-identifiers-heldout", () =>
        {
            foreach (string text in new[] { "6.2.3", "2..3", "7, 8", ".8", "4.", "rooty", "ab", "x2", "mu2", "sin(z)", "x+\u200B2" })
            {
                var result = Parse(text);
                Equal(0, result.Candidates.Count);
                Equal("blocked", result.ContentEligibility);
            }
            Equal("Number:2.50", Shape(First("0002,50")));
        });

        check("parser-cancellation-and-invalid-surrogate-boundary", () =>
        {
            var source = new SourceSnapshot("z+8");
            bool cancelled = false;
            try { Parser.Parse(source, new TextSpan(0, source.Raw.Length), cancellationToken: new CancellationToken(true)); }
            catch (OperationCanceledException) { cancelled = true; }
            True(cancelled, "Cancellation must not return a candidate.");
            var emoji = new SourceSnapshot("😀z^3");
            bool rejected = false;
            try { Parser.Parse(emoji, new TextSpan(1, emoji.Raw.Length)); }
            catch (ArgumentException) { rejected = true; }
            True(rejected, "A span may not start inside an emoji surrogate pair.");
        });

        check("parser-deterministic-and-concurrent-snapshots", () =>
        {
            var source = new SourceSnapshot("z+23/5", revision: 7);
            var span = new TextSpan(0, source.Raw.Length);
            string[] expected = Parser.Parse(source, span).Candidates.Select(c => c.Id).ToArray();
            Parallel.For(0, 24, _ =>
            {
                string[] actual = Parser.Parse(source, span).Candidates.Select(c => c.Id).ToArray();
                True(expected.SequenceEqual(actual), "Concurrent calls changed deterministic candidate IDs.");
            });
            var next = Parser.Parse(new SourceSnapshot(source.Raw, revision: 8), span);
            True(!expected.SequenceEqual(next.Candidates.Select(c => c.Id)), "Source revision must be part of snapshot identity.");
        });
    }

    private static CandidateSet Parse(string raw)
    {
        var source = new SourceSnapshot(raw);
        return Parser.Parse(source, new TextSpan(0, raw.Length));
    }

    private static MathNode First(string raw)
    {
        var result = Parse(raw);
        True(result.Candidates.Count != 0 && result.Candidates[0].Kind == "direct", "Expected direct parse for " + raw);
        return result.Candidates[0].Document.Root;
    }

    private static void Rejects(string raw, string code)
    {
        var result = Parse(raw);
        Equal(0, result.Candidates.Count);
        True(result.Diagnostics.Any(d => d.Code == code), "Expected " + code + " for " + raw);
    }

    private static string Shape(MathNode node)
    {
        if (node.Type == "Number") return "Number:" + node.Value;
        if (node.Type == "Symbol") return "Symbol:" + node.Name;
        return node.Type + (node.Operator == null ? "" : ":" + node.Operator) + "(" + string.Join(",", node.Children.Select(Shape)) + ")";
    }

    private static void True(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected {expected}; actual {actual}."); }
}
