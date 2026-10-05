using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;

namespace Locus.Application;

public static class ContentReactionDrafts
{
    public static bool IsDraft(string raw, FormulaSettings settings, long revision)
    {
        if (raw.Length == 0 || raw.Length > FormulaSession.MaxSourceLength) return false;
        var source = new SourceSnapshot(raw, revision);
        var profiles = settings.MarkerProfiles ?? MarkerPreferences.Migrate(settings.Open, settings.Close).Profiles;
        var scan = ProfileMarkerScanner.Scan(source, profiles.ToProfiles(settings.Open, settings.Close));
        AssistanceRegion region;
        if (scan.ReservedSpans.Count > 0)
        {
            var marker = scan.Regions.Count == 1 ? scan.Regions[0] : null;
            if (marker == null || !marker.IsClosed || marker.ReplacementSpan.Start != 0 || marker.ReplacementSpan.End != raw.Length ||
                marker.Profile.Domain is not ("chemistry" or "auto") || (marker.Profile.Resolve(settings.EnabledDomains) & DetectionDomains.Chemistry) == 0) return false;
            region = new(source, marker.ContentSpan, marker.ReplacementSpan, marker.Profile.Markers, new(marker.Profile.Id, marker.Profile.Domain));
        }
        else
        {
            if ((settings.EnabledDomains & DetectionDomains.Chemistry) == 0) return false;
            region = new(source, new(0, raw.Length), new(0, raw.Length));
        }
        var draft = ReactionDraftParser.Parse(region).Draft;
        return draft != null && draft.Reactants.Candidates.Count == 1 &&
            !draft.Reactants.Diagnostics.Concat(draft.Reactants.Candidates[0].Diagnostics).Any(d => d.Severity is "warning" or "error");
    }
}
