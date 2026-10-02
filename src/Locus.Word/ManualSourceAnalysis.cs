using System;
using System.Linq;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;

namespace Locus.Word;

public static class ManualSourceAnalysis
{
    public static ManualFormulaState Analyze(string raw, WordDetectionSettings settings)
    {
        var source = new SourceSnapshot(raw);
        var analysis = new AnalysisEngine().Analyze(source, new AnalysisOptions(InputMode.Explicit, enabledDomains: settings.Domains, markerProfiles: settings.Profiles));
        if (analysis.Regions.Count == 1 && analysis.Regions[0].OriginalReplacement == raw && analysis.Regions[0].Candidates.Count > 0)
            return ManualFormulaState.FromReadings(analysis.Regions[0]);
        // A selected closed draft is previewable, but cannot be converted until a product is explicitly accepted.
        var scan = ProfileMarkerScanner.Scan(source, settings.Profiles);
        if (scan.Diagnostics.Any(d => d.Severity == "error") || scan.Drafts.Count != 0) throw new InvalidOperationException("select-one-complete-formula");
        var domains = settings.Domains;
        AssistanceRegion region;
        if (scan.ReservedSpans.Count == 0)
            region = new AssistanceRegion(source, new TextSpan(0, raw.Length), new TextSpan(0, raw.Length));
        else if (scan.Regions.Count == 1 && scan.Regions[0].ReplacementSpan.Equals(new TextSpan(0, raw.Length)))
        {
            var found = scan.Regions[0]; domains = found.Profile.Resolve(domains);
            if (found.Profile.Domain != "auto" && found.Profile.Domain != "chemistry") throw new InvalidOperationException("select-one-complete-formula");
            region = new AssistanceRegion(source, found.ContentSpan, found.ReplacementSpan, found.Profile.Markers, new RegionIntent(found.Profile.Id, found.Profile.Domain));
        }
        else throw new InvalidOperationException("select-one-complete-formula");
        var assistance = ChemistryAssistance.Analyze(region, new AssistanceContext(settings.Fingerprint), domains);
        return assistance.Draft != null ? new ManualFormulaState(null, assistance.Draft) : throw new InvalidOperationException("select-one-complete-formula");
    }
}
