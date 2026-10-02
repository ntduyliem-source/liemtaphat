using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;

namespace Locus.Word;

internal sealed class ScanChoice
{
    public ManualFormulaState Formula { get; private set; }
    public bool Confirmed { get; private set; }
    public bool Ignored { get; private set; }
    public bool Native { get; }
    public bool Ready => !Native && !Ignored && (Confirmed || IsUnambiguous(Formula));
    public static bool IsUnambiguous(ManualFormulaState state)
    {
        var set = state.Readings;
        return set != null && set.Candidates.Count(c => c.Kind != "repair") == 1 && set.Candidates[0].Kind == "direct" &&
            !set.Diagnostics.Concat(set.Candidates[0].Diagnostics).Any(d => d.Severity == "warning" || d.Severity == "error");
    }
    public ScanChoice(ManualFormulaState formula, bool native = false, bool ignored = false, bool confirmed = false)
    { Formula = formula; Native = native; Ignored = ignored; Confirmed = confirmed; }
    public void Choose(string id)
    {
        if (Native || Formula.Readings == null) throw new InvalidOperationException("scan-choice-unavailable");
        Formula = Formula.SelectReading(id); Confirmed = true;
    }
    public string Status => Native ? "Công thức native" : Ignored ? "Giữ text · bỏ qua" : Ready ? "Sẵn sàng chuyển" : "Cần chọn cách hiểu";
}

// Explicit text decisions travel with the document; scanning alone never writes this capsule.
internal sealed class ScanTextSnapshot
{
    public const string Prefix = "locus:text:1:";
    public ManagedSnapshot Snapshot { get; }
    public bool Ignored { get; }
    public bool Confirmed { get; }
    public ScanTextSnapshot(ManagedSnapshot snapshot, bool ignored, bool confirmed)
    { Snapshot = snapshot; Ignored = ignored; Confirmed = confirmed; }
    public string Encode()
    {
        string payload = (Ignored ? "1" : "0") + (Confirmed ? "1" : "0") + Snapshot.Encode();
        string value = Prefix + ManualStateCodec.Digest(payload) + ":" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        if (value.Length > ManagedSnapshot.MaxTagCodeUnits) throw new InvalidOperationException("scan-metadata-limit");
        return value;
    }
    public static ScanTextSnapshot Decode(string value)
    {
        if (value == null || value.Length > ManagedSnapshot.MaxTagCodeUnits || !value.StartsWith(Prefix, StringComparison.Ordinal)) throw new FormatException("scan-text-metadata");
        string[] parts = value.Substring(Prefix.Length).Split(':');
        if (parts.Length != 2) throw new FormatException("scan-text-metadata");
        string payload = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(parts[1]));
        if (payload.Length < 3 || ManualStateCodec.Digest(payload) != parts[0] || "01".IndexOf(payload[0]) < 0 || "01".IndexOf(payload[1]) < 0) throw new FormatException("scan-text-checksum");
        return new ScanTextSnapshot(ManagedSnapshot.Decode(payload.Substring(2)), payload[0] == '1', payload[1] == '1');
    }
    internal static bool HasEntry(string tag, string entry)
    {
        if (ManagedSnapshot.HasEntry(tag, entry)) return true;
        if (!tag.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try { return Decode(tag).Snapshot.EntryId == entry; } catch (Exception e) when (e is FormatException || e is ArgumentException) { return false; }
    }
}

internal static class ScanAnalysis
{
    public static IReadOnlyList<Tuple<TextSpan, ManualFormulaState>> Analyze(string raw, WordDetectionSettings settings)
    {
        var result = new AnalysisEngine().Analyze(new SourceSnapshot(raw), new AnalysisOptions(InputMode.Passive,
            enabledDomains: settings.Domains, markerProfiles: settings.Profiles, maxRegions: 64));
        if (result.Diagnostics.Any(d => d.Code.Contains("LIMIT"))) throw new InvalidOperationException("scan-region-limit");
        var regions = new List<Tuple<TextSpan, ManualFormulaState>>();
        foreach (var set in result.Regions)
        {
            var formula = ManualSourceAnalysis.Analyze(set.OriginalReplacement, settings);
            // Rebase to a standalone snapshot using the same core. Reject any context-dependent change.
            if (formula.Readings == null || !set.Candidates.Select(CandidateExporter.ToMathMl).SequenceEqual(formula.Readings.Candidates.Select(CandidateExporter.ToMathMl)))
                throw new InvalidOperationException("scan-reading-changed");
            regions.Add(Tuple.Create(set.ReplacementSpan, formula));
        }
        return regions;
    }
}
