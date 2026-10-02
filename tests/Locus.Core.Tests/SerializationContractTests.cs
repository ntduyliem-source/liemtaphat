using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Locus.Core;
using Locus.Core.Export;
using Locus.Core.Serialization;

/// <summary>Stored-snapshot and projection contracts, independent of parser fixtures.</summary>
internal static class SerializationContractTests
{
    internal static void Run(Action<string, Action> check)
    {
        var lazy = new Lazy<Fixture>(() => new Fixture());
        Fixture F() => lazy.Value;
        void Pass(string name, Func<bool> condition) => check(name, () =>
        {
            if (!condition()) throw new Exception("Contract failed: " + name);
        });
        void Reject(string name, Func<string> mutated) => check(name, () =>
        {
            try { CandidateSetSerializer.Deserialize(mutated()); }
            catch (FormatException) { return; }
            throw new Exception("Corrupt/unsupported snapshot was accepted: " + name);
        });

        Pass("snapshot-nul-emoji-nfd-crlf-raw-exact", () => F().Restored.Source.Raw == F().Raw);
        Pass("snapshot-source-revision-and-id-exact", () => F().Restored.Source.Revision == 42 && F().Restored.Source.Id == F().Source.Id);
        Pass("snapshot-entire-candidate-set-and-selection", () => F().Restored.Candidates.Count == 2 && F().Restored.SelectedCandidateId == F().Repair.Id);
        Pass("snapshot-repair-edits-and-markers", () => F().Restored.Candidates[1].Edits[0].Text == "(" && F().Restored.Markers!.Open == "lc[");
        Pass("snapshot-serialize-roundtrip-deterministic", () => F().Json == CandidateSetSerializer.Serialize(F().Restored));
        Pass("export-original-includes-marker", () => CandidateExporter.Export(F().Direct).OriginalText == "lc[x+1/2]");
        Pass("export-direct-division-precedence", () => CandidateExporter.ToLatex(F().Direct) == @"x+\frac{1}{2}");
        Pass("export-repair-division-precedence", () => CandidateExporter.ToLatex(F().Repair) == @"\frac{x+1}{2}");

        Func<Fixture, MathNode>[] nodes =
        {
            f => f.U(f.P(f.S("x"),f.N("2"))),
            f => f.P(f.U(f.S("x")),f.N("2")),
            f => f.P(f.S("x"),f.P(f.N("2"),f.N("3"))),
            f => f.P(f.P(f.S("x"),f.N("2")),f.N("3")),
            f => f.B("subtract",f.S("x"),f.B("add",f.N("1"),f.N("2"))),
            f => f.B("divide",f.B("divide",f.N("8"),f.N("4")),f.N("2")),
            f => f.B("multiply",f.B("divide",f.N("2"),f.N("3")),f.S("x")),
            f => f.B("divide",f.N("2"),f.B("multiply",f.N("3"),f.S("x")))
        };
        string[] names = { "negative-power", "negative-base", "power-right-associated", "power-left-associated", "subtracted-sum", "nested-division", "division-then-multiply", "product-denominator" };
        for (int i = 0; i < nodes.Length; i++)
        {
            int index = i;
            Pass("export-mathml-xml-" + names[i], () => XElement.Parse(CandidateExporter.ToMathMl(F().C(nodes[index](F())))).Name.NamespaceName == CandidateExporter.MathMlNamespace);
            Pass("export-omml-xml-" + names[i], () => XElement.Parse(CandidateExporter.ToOmml(F().C(nodes[index](F())))).Name.NamespaceName == CandidateExporter.OmmlNamespace);
        }
        Pass("export-unary-outside-power", () => CandidateExporter.ToLatex(F().C(F().U(F().P(F().S("x"), F().N("2"))))).StartsWith("-{", StringComparison.Ordinal));
        Pass("export-negative-base-delimited", () =>
        {
            var f = F();
            var candidate = f.C(f.P(f.U(f.S("x")), f.N("2")));
            return CandidateExporter.ToLatex(candidate) == @"{\left(-x\right)}^{2}";
        });

        Reject("snapshot-unknown-envelope-version", () =>
        {
            var envelope = JsonNode.Parse(F().Json)!;
            envelope["envelopeVersion"] = "locus-json-envelope/9";
            return envelope.ToJsonString();
        });
        Reject("snapshot-rehashed-unknown-core-version", () => F().Changed(p => p["coreVersion"] = "unknown"));
        Reject("snapshot-rehashed-source-identity-corruption", () => F().Changed(p => p["source"]!["raw"] = "changed"));
        Reject("snapshot-rehashed-node-id-corruption", () => F().Changed(p => p["candidates"]![0]!["root"]!["id"] = "bad"));
        Reject("snapshot-rehashed-selected-id-corruption", () => F().Changed(p => p["selectedCandidateId"] = "missing"));
        Reject("snapshot-rehashed-span-corruption", () => F().Changed(p => p["contentSpan"]!["end"] = 100000));
        Reject("snapshot-trailing-json-rejected", () => F().Json + " {}");
        Reject("snapshot-truncated-json-rejected", () => F().Json[..^3]);
        Pass("snapshot-valid-depth-101-roundtrip", () =>
        {
            MathNode deep = F().S("x");
            for (int i = 0; i < 100; i++) deep = F().U(deep);
            var set = new CandidateSet(F().Source, F().Content, F().Replacement, new[] { F().C(deep) }, markers: MarkerConfiguration.Default);
            return CandidateSetSerializer.Deserialize(CandidateSetSerializer.Serialize(set)).Candidates[0].Document.Root.Depth == 101;
        });
    }

    private sealed class Fixture
    {
        internal string Raw { get; } = "\0😀e\u0301 lc[x+1/2] \r\n";
        internal SourceSnapshot Source { get; }
        internal TextSpan Content { get; }
        internal TextSpan Replacement { get; }
        internal Candidate Direct { get; }
        internal Candidate Repair { get; }
        internal string Json { get; }
        internal CandidateSet Restored { get; }

        internal Fixture()
        {
            Source = new SourceSnapshot(Raw, 42);
            int start = Raw.IndexOf("x+", StringComparison.Ordinal);
            Content = new TextSpan(start, start + 5);
            Replacement = new TextSpan(start - 3, start + 6);
            Direct = C(B("add", S("x"), B("divide", N("1"), N("2"))));
            Repair = new Candidate("repair", new MathDocument(B("divide", B("add", S("x"), N("1")), N("2"))),
                Source, Content, Replacement, edits: new[] { new SourceEdit(new TextSpan(start, start), "("), new SourceEdit(new TextSpan(start + 3, start + 3), ")") });
            var set = new CandidateSet(Source, Content, Replacement, new[] { Direct, Repair }, selectedCandidateId: Repair.Id, markers: MarkerConfiguration.Default);
            Json = CandidateSetSerializer.Serialize(set);
            Restored = CandidateSetSerializer.Deserialize(Json);
        }

        internal MathNode S(string name) => new("Symbol", name: name, sourceSpans: new[] { Content });
        internal MathNode N(string value) => new("Number", value: value, sourceSpans: new[] { Content });
        internal MathNode B(string op, MathNode left, MathNode right) => new("Binary", new[] { left, right }, @operator: op, sourceSpans: new[] { Content });
        internal MathNode P(MathNode basis, MathNode exponent) => new("Power", new[] { basis, exponent }, sourceSpans: new[] { Content });
        internal MathNode U(MathNode operand) => new("Unary", new[] { operand }, @operator: "minus", sourceSpans: new[] { Content });
        internal Candidate C(MathNode node) => new("direct", new MathDocument(node), Source, Content, Replacement);

        internal string Changed(Action<JsonNode> alter)
        {
            var envelope = JsonNode.Parse(Json)!;
            var payload = JsonNode.Parse(envelope["payload"]!.GetValue<string>())!;
            alter(payload);
            string text = payload.ToJsonString();
            envelope["payload"] = text;
            envelope["sha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
            return envelope.ToJsonString();
        }
    }
}
