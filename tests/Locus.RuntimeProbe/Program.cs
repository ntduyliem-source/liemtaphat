using System;
using System.Text;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;
using Locus.Core.Serialization;

internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        string[] samples = { "can2", "x mũ 2", "x mu\u0303 2", "1 trên 2", "q+7/11", "căn z cộng 8", "can(6+9", "7/8q", "-q^3", "q^-3", "a-(b-c)", "lc[ x^2 ]", "😀 Đặt q^7 rồi r=4." };
        var engine = new AnalysisEngine();
        foreach (string raw in samples)
        {
            var mode = raw.StartsWith("lc[", StringComparison.Ordinal) ? InputMode.Markers : raw.StartsWith("😀", StringComparison.Ordinal) ? InputMode.Passive : InputMode.Explicit;
            var source = new SourceSnapshot(raw, 7);
            var analysis = engine.Analyze(source, new AnalysisOptions(mode));
            Console.WriteLine(source.Id + "|" + analysis.Detection + "|" + analysis.ContentEligibility);
            foreach (var region in analysis.Regions)
            {
                var serialized = CandidateSetSerializer.Serialize(region);
                var restored = CandidateSetSerializer.Deserialize(serialized);
                if (restored.Source.Raw != raw) throw new Exception("Source changed during roundtrip.");
                foreach (var candidate in restored.Candidates)
                {
                    var e = CandidateExporter.Export(candidate);
                    Console.WriteLine(candidate.Id + "|" + candidate.Kind + "|" + Encode(e.MathMl) + "|" + Encode(e.Omml) + "|" + Encode(e.Latex));
                }
            }
            var normalized = NormalizedSource.Create(source);
            Console.WriteLine("NORMALIZED|" + Encode(normalized.Text));
        }
        return 0;
    }
    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
