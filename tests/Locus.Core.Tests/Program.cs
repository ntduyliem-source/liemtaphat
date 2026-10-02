using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Parsing;
using Locus.Core.Export;
using Locus.Core.Serialization;

public static class CoreVerification
{
    private static readonly List<object> Results = new();
    private static int failures;
    private static string Root = "";
    private static string CorpusText = "";
    private static readonly AnalysisEngine Engine = new();
    private static readonly FormulaParser Parser = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Root = args.Length == 2 && args[0] == "--root" ? Path.GetFullPath(args[1]) : FindRoot();
        var report = await RunInMemory(File.ReadAllText(Path.Combine(Root, "corpus/m0/cases.json")));
        RunTiming();
        string artifacts = Path.Combine(Root, "artifacts", "m1"); Directory.CreateDirectory(artifacts);
        File.WriteAllText(Path.Combine(artifacts, "verification.json"), report, new UTF8Encoding(false));
        Console.WriteLine($"Core verification: {Results.Count - failures}/{Results.Count} passed; {failures} failed. Report: artifacts/m1/verification.json");
        return failures == 0 ? 0 : 1;
    }

    // The exact same contract checks can run in a browser without filesystem access.
    public static async Task<string> RunInMemory(string corpus)
    {
        Results.Clear(); failures = 0; CorpusText = corpus;
        var watch = Stopwatch.StartNew();
        RunCorpus();
        RunSourceTests();
        RunHeldOutTests();
        ParserContractTests.Run((id, action) => Check(id, "parser-contract-heldout", action));
        DetectionContractTests.Run((id, action) => Check(id, "detector-contract-heldout", action));
        RunSerializationTests();
        SerializationContractTests.Run((id, action) => Check(id, "serialization-export-contract", action));
        RunExportTests();
        await RunCancellationTests();
        RunGeneratedTests();
        return JsonSerializer.Serialize(new
        {
            schemaVersion = "locus-m1-verification/1", capturedAtUtc = DateTimeOffset.UtcNow,
            core = Versions.Core, grammar = Versions.Grammar,
            summary = new { checks = Results.Count, passed = Results.Count - failures, failed = failures, elapsedMs = watch.ElapsedMilliseconds },
            limits = new[] { "Corpus compares core content projections, not Word host write guards or IME.", "Eight pending M0 cases remain excluded from pass counts.", "No Word document or clipboard integration tested by this runner." },
            results = Results
        }, JsonOptions);
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Locus.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Run from the repository or pass --root path.");
    }
    private static void Check(string id, string scope, Action action)
    {
        try { action(); Results.Add(new { id, scope, status = "PASS" }); }
        catch (Exception e) { failures++; Results.Add(new { id, scope, status = "FAIL", error = e.ToString() }); Console.WriteLine($"FAIL {id}: {e.Message}"); }
    }
    private static void Equal<T>(T expected, T actual, string label = "value")
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"{label}: expected {expected}, got {actual}"); }
    private static void True(bool condition, string label) { if (!condition) throw new Exception(label); }
    private static void Throws(Action action, string label)
    { try { action(); } catch (Exception e) when (e is ArgumentException || e is FormatException || e is InvalidOperationException || e is System.Runtime.Serialization.SerializationException || e is NotSupportedException) { return; } throw new Exception("Expected rejection: " + label); }
    private static TextSpan Span(JsonElement e) => new(e[0].GetInt32(), e[1].GetInt32());
    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString()!;
    private static CandidateSet Parse(string raw) { var source = new SourceSnapshot(raw); return Parser.Parse(source, new TextSpan(0, raw.Length)); }
    private static AnalysisOptions Options(JsonElement context)
    {
        var mode = Text(context, "inputMode") switch { "passive" => InputMode.Passive, "marked" => InputMode.Markers, _ => InputMode.Explicit };
        MarkerConfiguration? markers = context.TryGetProperty("delimiters", out var d) ? new(Text(d, "open"), Text(d, "close")) : null;
        return new AnalysisOptions(mode, Text(context, "domain"), markers);
    }

    private static void RunCorpus()
    {
        using var doc = JsonDocument.Parse(CorpusText);
        int pending = 0, content = 0, snapshots = 0;
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (Text(c, "status") == "pending") { pending++; continue; }
            var context = c.GetProperty("context"); var expected = c.GetProperty("expected");
            string raw = Text(c.GetProperty("source"), "raw"); string id = Text(c, "id");
            bool stored = Text(context, "action") == "reopen_snapshot";
            if (stored) snapshots++; else content++;
            Check(id, stored ? "stored-snapshot-content-only" : "corpus-core-content-only", () =>
            {
                var source = new SourceSnapshot(raw);
                if (stored)
                {
                    foreach (var region in expected.GetProperty("regions").EnumerateArray())
                    {
                        var cs = Span(region.GetProperty("sourceSpan")); var rs = Span(region.GetProperty("replacementSpan"));
                        var candidates = region.GetProperty("candidates").EnumerateArray().Select(x => new Candidate(Text(x, "kind"),
                            new MathDocument(FromProjection(x.GetProperty("mathDocument").GetProperty("root"), cs)), source, cs, rs,
                            ReadDiagnostics(x.GetProperty("diagnostics")), x.GetProperty("edits").EnumerateArray().Select(e => new SourceEdit(Span(e.GetProperty("span")), Text(e, "replacement"))), "stored-fixture")).ToArray();
                        var snapshot = new CandidateSet(source, cs, rs, candidates, ReadDiagnostics(region.GetProperty("diagnostics")), candidates[0].Id);
                        var restored = CandidateSetSerializer.Deserialize(CandidateSetSerializer.Serialize(snapshot));
                        CompareRegion(region, restored); Equal(snapshot.SelectedCandidateId, restored.SelectedCandidateId, "stored selection");
                    }
                    return;
                }
                var actual = Engine.Analyze(source, Options(context));
                Equal(Text(expected, "detection"), actual.Detection.ToString().ToLowerInvariant(), "detection");
                Equal(raw, actual.Source.Raw, "raw source");
                Equal(Text(expected.GetProperty("auto"), "contentEligibility"), actual.ContentEligibility, "content eligibility");
                CompareDiagnostics(expected.GetProperty("diagnostics"), actual.Diagnostics, "analysis diagnostics");
                var regions = expected.GetProperty("regions").EnumerateArray().ToArray();
                Equal(regions.Length, actual.Regions.Count, "region count");
                for (int i = 0; i < regions.Length; i++) CompareRegion(regions[i], actual.Regions[i]);
            });
        }
        Equal(8, pending, "documented pending cases");
        Console.WriteLine($"Corpus: {content} content cases + {snapshots} stored snapshot cases; {pending} pending. Word writeExpectation/presentation guards are not asserted.");
    }
    private static IEnumerable<Diagnostic> ReadDiagnostics(JsonElement e) => e.EnumerateArray().Select(d => new Diagnostic(Text(d, "code"), Text(d, "severity"), Span(d.GetProperty("span")), Text(d, "message"))).ToArray();
    private static void CompareRegion(JsonElement expected, CandidateSet actual)
    {
        Equal(Span(expected.GetProperty("sourceSpan")), actual.ContentSpan, "content span");
        Equal(Span(expected.GetProperty("replacementSpan")), actual.ReplacementSpan, "replacement span");
        Equal(Text(expected, "sourceText"), actual.OriginalContent, "original content");
        Equal(Text(expected, "replacementText"), actual.OriginalReplacement, "original replacement");
        CompareDiagnostics(expected.GetProperty("diagnostics"), actual.Diagnostics, "region diagnostics");
        var candidates = expected.GetProperty("candidates").EnumerateArray().ToArray();
        Equal(candidates.Length, actual.Candidates.Count, "candidate count");
        for (int i = 0; i < candidates.Length; i++)
        {
            var ex = candidates[i]; var ac = actual.Candidates[i];
            Equal(Text(ex, "kind"), ac.Kind, "candidate kind");
            var ast = JsonNode.Parse(ex.GetProperty("mathDocument").GetProperty("root").GetRawText());
            True(JsonNode.DeepEquals(ast, Projection(ac.Document.Root)), "AST differs: expected " + ast + "; actual " + Projection(ac.Document.Root));
            CompareDiagnostics(ex.GetProperty("diagnostics"), ac.Diagnostics, "candidate diagnostics");
            var edits = ex.GetProperty("edits").EnumerateArray().ToArray(); Equal(edits.Length, ac.Edits.Count, "edit count");
            for (int n = 0; n < edits.Length; n++) { Equal(Span(edits[n].GetProperty("span")), ac.Edits[n].Span, "edit span"); Equal(Text(edits[n], "replacement"), ac.Edits[n].Text, "edit text"); }
        }
    }
    private static void CompareDiagnostics(JsonElement expected, IReadOnlyList<Diagnostic> actual, string label)
    {
        var ds = expected.EnumerateArray().ToArray(); Equal(ds.Length, actual.Count, label + " count");
        for (int i = 0; i < ds.Length; i++) { Equal(Text(ds[i], "code"), actual[i].Code, label + " code"); Equal(Text(ds[i], "severity"), actual[i].Severity, label + " severity"); Equal(Span(ds[i].GetProperty("span")), actual[i].Span, label + " span"); }
    }
    internal static JsonObject Projection(MathNode n)
    {
        var o = new JsonObject { ["type"] = n.Type };
        if (n.Value != null) o["value"] = n.Value;
        if (n.Name != null) o["name"] = n.Name;
        if (n.Operator != null) o["operator"] = n.Operator;
        string[] keys = n.Type switch { "Binary" or "Relation" => ["left", "right"], "Power" => ["base", "exponent"], "Unary" => ["operand"], "Sqrt" => ["radicand"], _ => [] };
        for (int i = 0; i < keys.Length; i++) o[keys[i]] = Projection(n.Children[i]);
        return o;
    }
    private static MathNode FromProjection(JsonElement e, TextSpan span)
    {
        string type = Text(e, "type");
        string[] keys = type switch { "Binary" or "Relation" => ["left", "right"], "Power" => ["base", "exponent"], "Unary" => ["operand"], "Sqrt" => ["radicand"], _ => [] };
        return new MathNode(type, keys.Select(k => FromProjection(e.GetProperty(k), span)), e.TryGetProperty("value", out var value) ? value.GetString() : null,
            e.TryGetProperty("name", out var name) ? name.GetString() : null, e.TryGetProperty("operator", out var op) ? op.GetString() : null, [span]);
    }

    private static void RunSourceTests()
    {
        Check("unicode-normalization-map", "source", () =>
        {
            const string raw = "😀 ca\u0306n\u00a0x ≤ 2\r\n";
            var source = new SourceSnapshot(raw); var n = NormalizedSource.Create(source);
            Equal("😀 căn x <= 2\r\n", n.Text); Equal(raw, source.Raw);
            var rawStart = raw.IndexOf("a\u0306", StringComparison.Ordinal); int normalizedStart = n.Text.IndexOf('ă');
            Equal(new TextSpan(rawStart, rawStart + 2), n.ProjectToSource(new TextSpan(normalizedStart, normalizedStart + 1)).Single());
            int op = n.Text.IndexOf("<=", StringComparison.Ordinal); int rawOp = raw.IndexOf('≤');
            Equal(new TextSpan(rawOp, rawOp + 1), n.ProjectToSource(new TextSpan(op + 1, op + 2)).Single());
            Equal(new TextSpan(0, raw.Length), n.ProjectToSource(new TextSpan(0, n.Text.Length)).Single());
            True(n.Mappings.Any(m => m.Kind == "expand") && n.Mappings.Any(m => m.Kind == "compose"), "Map records expansion and composition.");
            Throws(() => source.Slice(new TextSpan(1, 2)), "surrogate split");
            Throws(() => new SourceSnapshot("\ud800"), "unpaired surrogate");
        });
        Check("normalization-hangul-and-nonidentity", "source", () =>
        {
            var s = new SourceSnapshot("\u1100\u1161\u11a8 + \u212B"); var n = NormalizedSource.Create(s);
            Equal(s.Raw.Normalize(NormalizationForm.FormC), n.Text);
            Equal(new TextSpan(0, 3), n.ProjectToSource(new TextSpan(0, 1)).Single());
        });
        Check("immutable-source-node-and-candidates", "model", () =>
        {
            var s = Parse("x^2"); var list = s.Candidates.ToList(); var clone = new CandidateSet(s.Source, s.ContentSpan, s.ReplacementSpan, list); list.Clear();
            Equal(1, clone.Candidates.Count); True(clone.Candidates is not Candidate[], "No writable array leaks.");
            Equal(Parse("x^2").Candidates[0].Id, s.Candidates[0].Id, "stable candidate ID");
            True(new SourceSnapshot("x", 1).Id != new SourceSnapshot("x", 2).Id, "Revision changes snapshot identity.");
            var span = new TextSpan(0, 1);
            Throws(() => new MathNode("Number", value: "1e3", sourceSpans: [span]), "out-of-grammar number");
            Throws(() => new MathNode("Symbol", name: "xy", sourceSpans: [span]), "multi-letter symbol");
            Throws(() => s.Select("unknown"), "unknown candidate selection");
        });
        Check("repair-edits-source-invariants", "source", () =>
        {
            foreach (string raw in new[] { "q+7/11", "căn z cộng 8", "sqrt(6+9" })
            {
                var set = Parse(raw); var repair = set.Candidates.Single(c => c.Kind == "repair");
                var edited = SourceEdit.Apply(set.Source, repair.Edits); var reparsed = Parse(edited);
                True(JsonNode.DeepEquals(Projection(repair.Document.Root), Projection(reparsed.Candidates[0].Document.Root)), "Repair edits must reproduce the proposed AST.");
                Equal(raw, set.Source.Raw); Equal("blocked", set.ContentEligibility);
            }
            Throws(() => SourceEdit.Apply(new SourceSnapshot("abc"), [new(new TextSpan(0, 2), "a"), new(new TextSpan(1, 3), "b")]), "overlapping repair");
        });
        Check("candidate-identity-encodes-edits-and-policy-unambiguously", "model", () =>
        {
            var source = new SourceSnapshot("x"); var span = new TextSpan(0, 1);
            var doc = new MathDocument(new MathNode("Symbol", name: "x", sourceSpans: [span]));
            var a = new Candidate("repair", doc, source, span, span, edits: [new(new TextSpan(0, 0), "a|[1,1):b")]);
            var b = new Candidate("repair", doc, source, span, span, edits: [new(new TextSpan(0, 0), "a"), new(new TextSpan(1, 1), "b")]);
            True(SourceEdit.Apply(source, a.Edits) != SourceEdit.Apply(source, b.Edits), "Different repair fixtures.");
            True(a.Id != b.Id, "Edit identity cannot depend on ambiguous string concatenation.");
            var plain = new Candidate("direct", doc, source, span, span);
            var warned = new Candidate("direct", doc, source, span, span, [new Diagnostic("NEW_WARNING", "warning", span)]);
            True(plain.Id != warned.Id, "Policy-bearing diagnostics must invalidate candidate identity.");
        });
    }

    private static void RunHeldOutTests()
    {
        var pairs = new[] { ("7 cộng 12 nhân 3", "7+12*3"), ("z mu 5", "z^5"), ("can37", "sqrt(37)"), ("3,25 chia 5", "3.25/5"), ("−q^3", "-(q^3)"), ("q^-3", "q^(-3)"), ("4(q-7)", "4*(q-7)"), ("(a+2)(b-9)", "(a+2)*(b-9)"), ("q^r^s", "q^(r^s)"), ("ca\u0306n 49", "sqrt(49)"), ("x·y", "x*y"), ("x≤2", "x<=2") };
        foreach (var pair in pairs) Check("heldout:" + pair.Item1, "heldout-grammar", () =>
        {
            var a = Parse(pair.Item1); var b = Parse(pair.Item2);
            True(a.Candidates.Count > 0 && b.Candidates.Count > 0, "Valid expressions must parse.");
            True(JsonNode.DeepEquals(Projection(a.Candidates[0].Document.Root), Projection(b.Candidates[0].Document.Root)), "Equivalent spellings differ.");
        });
        foreach (var raw in new[] { "xy+2", "canx+2", "log(x)", "x2+1", "2 3", "x___2", ".5", "1.", "x<y<z", "sqrt sqrt x", "x+\u200d1", "H2SO4" })
            Check("reject:" + raw, "heldout-rejection", () => Equal(0, Parse(raw).Candidates.Count));
        Check("prose-many-independent-regions", "heldout-detection", () =>
        {
            const string raw = "Đặt q^7, rồi r = 4 + 9. Email q+7@a.test giữ nguyên; cuối là z/6.";
            var r = Engine.Analyze(new SourceSnapshot(raw), new AnalysisOptions(InputMode.Passive));
            Equal("q^7|r = 4 + 9|z/6", string.Join("|", r.Regions.Select(x => x.OriginalContent)));
        });
        Check("marker-multiple-unicode-and-whitespace", "heldout-marker", () =>
        {
            const string raw = "😀 Có << q^7 >>; rồi << 3 trên 5 >> nhé.";
            var r = Engine.Analyze(new SourceSnapshot(raw), new AnalysisOptions(InputMode.Markers, markers: new MarkerConfiguration("<<", ">>")));
            Equal(2, r.Regions.Count); Equal("<< q^7 >>|<< 3 trên 5 >>", string.Join("|", r.Regions.Select(x => x.OriginalReplacement)));
            foreach (var set in r.Regions) Equal("<<", set.Markers!.Open);
        });
        Check("ambiguity-never-hidden-by-cap", "candidate-policy", () =>
        { var r = Parse("7/8q/9r"); Equal(0, r.Candidates.Count); True(r.Diagnostics.Any(d => d.Code == "AMBIGUITY_LIMIT"), "Ambiguity limit diagnostic required."); });
        Check("limit-depth-and-length", "limits", () =>
        {
            Equal(0, Parse(new string('(', 2000) + "x" + new string(')', 2000)).Candidates.Count);
            var tooLong = Engine.Analyze(new SourceSnapshot(new string('x', 70000)), new AnalysisOptions());
            Equal("blocked", tooLong.ContentEligibility); Equal(0, tooLong.Regions.Count);
        });
    }

    private static void RunSerializationTests()
    {
        foreach (var raw in new[] { "x+1/2", "căn x cộng 1", "2/3x", "ca\u0306n\u00a049", "can(2+3" })
            Check("snapshot:" + raw, "serialization", () =>
            {
                var first = Parse(raw); var selected = first.Select(first.Candidates.Last().Id);
                string json = CandidateSetSerializer.Serialize(selected); var restored = CandidateSetSerializer.Deserialize(json);
                Equal(json, CandidateSetSerializer.Serialize(restored), "deterministic snapshot roundtrip");
                Equal(raw, restored.Source.Raw); Equal(selected.SelectedCandidateId, restored.SelectedCandidateId);
                Equal(selected.Candidates.Count, restored.Candidates.Count, "All candidates preserved");
                True(restored.Candidates.All(c => c.Document.Root.SourceSpans.Count > 0), "Source references survive.");
            });
        Check("snapshot-marker-original-and-config", "serialization", () =>
        {
            var result = Engine.Analyze(new SourceSnapshot("😀 << x mu\u0303 2 >> fin", 17), new AnalysisOptions(InputMode.Markers, markers: new MarkerConfiguration("<<", ">>")));
            var set = result.Regions.Single(); var restored = CandidateSetSerializer.Deserialize(CandidateSetSerializer.Serialize(set));
            Equal("<< x mu\u0303 2 >>", restored.OriginalReplacement); Equal(17L, restored.Source.Revision); Equal(">>", restored.Markers!.Close);
        });
        Check("snapshot-reject-corruption-and-unsupported-version", "serialization", () =>
        {
            string json = CandidateSetSerializer.Serialize(Parse("x^2"));
            Throws(() => CandidateSetSerializer.Deserialize(json.Substring(0, json.Length - 5)), "truncated JSON");
            string corrupted = json.Replace("Power", "Sqrt", StringComparison.Ordinal);
            True(corrupted != json, "Corruption fixture must alter payload.");
            Throws(() => CandidateSetSerializer.Deserialize(corrupted), "corruption checksum");
            var version = JsonNode.Parse(json)!.AsObject(); version["envelopeVersion"] = "locus-json-envelope/99";
            Throws(() => CandidateSetSerializer.Deserialize(version.ToJsonString()), "future envelope");
            Throws(() => CandidateSetSerializer.Deserialize(json + "{}"), "trailing JSON object");
            foreach (var mutate in new Action<JsonObject>[] {
                p => p["snapshotVersion"] = "locus-candidate-set/99",
                p => p["source"]!["id"] = "wrong-source",
                p => p["selectedCandidateId"] = "missing-candidate",
                p => p["contentSpan"]!["end"] = 99999,
                p => p["candidates"]![0]!["root"]!["id"] = "wrong-node"
            }) Throws(() => CandidateSetSerializer.Deserialize(RewriteSnapshot(json, mutate)), "invalid snapshot even with recomputed checksum");
        });
    }
    private static string RewriteSnapshot(string json, Action<JsonObject> mutate)
    {
        var envelope = JsonNode.Parse(json)!.AsObject(); var payload = JsonNode.Parse(envelope["payload"]!.GetValue<string>())!.AsObject();
        mutate(payload); string text = payload.ToJsonString(); envelope["payload"] = text;
        envelope["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        return envelope.ToJsonString();
    }

    private static void RunExportTests()
    {
        XNamespace mml = "http://www.w3.org/1998/Math/MathML", omml = "http://schemas.openxmlformats.org/officeDocument/2006/math";
        foreach (string raw in new[] { "x+1/2", "(a+b)/(c-d)", "sqrt(q+8)^3", "(-x)^2", "-x^2", "x^(y^2)", "(x^y)^2", "a-(b-c)", "a/(b/c)", "x<y", "2/3x" })
            Check("export:" + raw, "export", () =>
            {
                var set = Parse(raw);
                foreach (var c in set.Candidates)
                {
                    var e = CandidateExporter.Export(c); Equal(c.Id, e.CandidateId); Equal(c.Source.Id, e.SourceId); Equal(raw, e.OriginalText);
                    var mx = XElement.Parse(e.MathMl); var ox = XElement.Parse(e.Omml);
                    Equal(mml + "math", mx.Name); Equal(omml + "oMath", ox.Name);
                    Equal(CountType(c.Document.Root, "Binary", "divide"), mx.Descendants(mml + "mfrac").Count(), "MathML fractions");
                    Equal(CountType(c.Document.Root, "Binary", "divide"), ox.Descendants(omml + "f").Count(), "OMML fractions");
                    Equal(CountType(c.Document.Root, "Power"), mx.Descendants(mml + "msup").Count(), "MathML powers");
                    Equal(CountType(c.Document.Root, "Sqrt"), ox.Descendants(omml + "rad").Count(), "OMML roots");
                }
            });
        Check("export-distinguishes-grouping", "export", () =>
        {
            foreach (var pair in new[] { ("(-x)^2", "-x^2"), ("(x^y)^2", "x^(y^2)"), ("a-(b-c)", "a-b-c"), ("a/(b/c)", "a/b/c") })
            {
                var a = CandidateExporter.Export(Parse(pair.Item1).Candidates[0]); var b = CandidateExporter.Export(Parse(pair.Item2).Candidates[0]);
                True(a.Latex != b.Latex && a.MathMl != b.MathMl && a.Omml != b.Omml, "Different AST grouping must remain distinct in all exporters.");
            }
        });
    }
    private static int CountType(MathNode n, string type, string? op = null) => (n.Type == type && (op == null || n.Operator == op) ? 1 : 0) + n.Children.Sum(c => CountType(c, type, op));

    private static async Task RunCancellationTests()
    {
        Check("pre-cancelled-parser-and-normalizer", "cancellation", () =>
        {
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); int caught = 0;
            var s = new SourceSnapshot("x^2");
            try { Parser.Parse(s, new TextSpan(0, 3), cancellationToken: cancellation.Token); } catch (OperationCanceledException) { caught++; }
            try { NormalizedSource.Create(s, cancellation.Token); } catch (OperationCanceledException) { caught++; }
            Equal(2, caught);
        });
        using var gate = new LatestRequestGate<string>();
        var firstCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldTask = gate.RunAsync(_ => firstCompletion.Task);
        var fresh = await gate.RunAsync(_ => Task.FromResult("new source/config"));
        firstCompletion.SetResult("old ignored cancellation"); var stale = await oldTask;
        Check("out-of-order-result-discarded", "cancellation", () => { True(fresh.IsCurrent, "Latest result must publish."); True(!stale.IsCurrent && stale.Value == null, "Stale result must not escape."); });
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = gate.RunAsync(_ => pending.Task); gate.Invalidate(); pending.SetResult("late"); var invalidated = await old;
        Check("config-or-session-invalidation", "cancellation", () => True(!invalidated.IsCurrent, "Invalidated request cannot publish."));
        var callbackCompletion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var broken = gate.RunAsync(token => { token.Register(() => throw new InvalidOperationException("callback failed")); return callbackCompletion.Task; });
        var surviving = await gate.RunAsync(_ => Task.FromResult("new")); callbackCompletion.SetResult("old"); var discarded = await broken;
        Check("throwing-cancellation-callback-does-not-poison-next-request", "cancellation", () => { True(surviving.IsCurrent, "New request must survive prior consumer callback."); True(!discarded.IsCurrent, "Prior result must remain discarded."); });
        Check("pre-cancelled-long-combining-sequence", "cancellation", () =>
        {
            using var cts = new CancellationTokenSource();
            var input = new SourceSnapshot("a" + new string('\u0301', 200000));
            // A 1 ms timer is not guaranteed to fire before this synchronous call finishes,
            // even on native runtimes. Timer delivery is a host observation in WEB0.
            cts.Cancel();
            try { NormalizedSource.Create(input, cts.Token); } catch (OperationCanceledException) { return; }
            throw new Exception("A cancelled combining-sequence normalization returned a result.");
        });
    }

    private static void RunGeneratedTests()
    {
        Check("generated-grammar-and-repair-invariants", "generated-200", () =>
        {
            var rng = new Random(711);
            for (int i = 0; i < 200; i++)
            {
                int a = rng.Next(1, 1000), b = rng.Next(1, 1000); char variable = (char)('a' + rng.Next(26));
                string raw = $"{variable}+{a}/{b}"; var set = Parse(raw);
                Equal(2, set.Candidates.Count, "Generated family candidate count");
                Equal("direct", set.Candidates[0].Kind); Equal("repair", set.Candidates[1].Kind); Equal("blocked", set.ContentEligibility);
                var n = set.Candidates[0].Document.Root; Equal(variable.ToString(), n.Children[0].Name); Equal(a.ToString(), n.Children[1].Children[0].Value); Equal(b.ToString(), n.Children[1].Children[1].Value);
                foreach (var c in set.Candidates) { Equal(set.Source.Id, c.Source.Id); True(c.Document.Root.SourceSpans.All(set.ContentSpan.Contains), "Original source refs only."); }
            }
        });
        Check("deterministic-malformed-inputs", "generated-300", () =>
        {
            var rng = new Random(912); const string alphabet = "xyz0123+-*/^(),_\\[] canmu√≤\u200b";
            for (int i = 0; i < 300; i++)
            {
                string raw = new(Enumerable.Range(0, rng.Next(1, 120)).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());
                var a = Parse(raw); var b = Parse(raw); Equal(raw, a.Source.Raw); True(a.Candidates.Count <= 3, "Bounded candidate set.");
                Equal(string.Join('|', a.Candidates.Select(c => c.Id)), string.Join('|', b.Candidates.Select(c => c.Id)), "Deterministic candidates");
                if (a.Candidates.Any(c => c.Kind != "direct")) Equal("blocked", a.ContentEligibility);
            }
        });
    }

    private static void RunTiming()
    {
        string[] samples = ["can2", "x mũ 2", "1 trên 2", "căn(x+1)^2", "x+1/2", "2/3x", "Ta có lc[x^2] và lc[1 trên 2]."];
        var observations = new List<double>();
        for (int i = 0; i < 70; i++) Engine.Analyze(new SourceSnapshot(samples[i % samples.Length]), new AnalysisOptions(i % samples.Length == 6 ? InputMode.Markers : InputMode.Explicit));
        for (int i = 0; i < 350; i++)
        {
            var sw = Stopwatch.StartNew();
            Engine.Analyze(new SourceSnapshot(samples[i % samples.Length]), new AnalysisOptions(i % samples.Length == 6 ? InputMode.Markers : InputMode.Explicit));
            observations.Add(sw.Elapsed.TotalMilliseconds);
        }
        observations.Sort();
        Directory.CreateDirectory(Path.Combine(Root, "artifacts/m1"));
        File.WriteAllText(Path.Combine(Root, "artifacts/m1/timing.json"), JsonSerializer.Serialize(new
        {
            capturedAtUtc = DateTimeOffset.UtcNow, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            os = System.Runtime.InteropServices.RuntimeInformation.OSDescription, iterations = observations.Count,
            p50Ms = observations[observations.Count / 2], p95Ms = observations[(int)(observations.Count * .95) - 1], maxMs = observations[^1],
            samples, scope = "Warm local core content analysis, short authored samples only; no preview/Word/network measurement."
        }, JsonOptions));
    }
}
