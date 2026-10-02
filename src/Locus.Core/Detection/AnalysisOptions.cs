using System;
using System.Collections.Generic;
using System.Linq;

namespace Locus.Core.Detection
{
    public enum InputMode { Explicit, Passive, Markers }
    public enum DetectionStatus { Accept, Reject, Deferred }
    [Flags]
    public enum DetectionDomains { None = 0, Math = 1, Physics = 2, Chemistry = 4, All = 7 }

    /// <summary>Pure content analysis settings. These settings never authorize a host document write.</summary>
    public sealed class AnalysisOptions
    {
        public InputMode InputMode { get; }
        public string Domain { get; }
        public MarkerConfiguration Markers { get; }
        public int MaxSourceLength { get; }
        public int MaxRegionLength { get; }
        public int MaxRegions { get; }
        public DetectionDomains EnabledDomains { get; }
        public MarkerProfileSet? MarkerProfiles { get; }

        public AnalysisOptions(InputMode inputMode = InputMode.Explicit, string domain = "math",
            MarkerConfiguration? markers = null, int maxSourceLength = 65536,
            int maxRegionLength = 4096, int maxRegions = 64, DetectionDomains enabledDomains = DetectionDomains.Math,
            MarkerProfileSet? markerProfiles = null)
        {
            if (!Enum.IsDefined(typeof(InputMode), inputMode)) throw new ArgumentOutOfRangeException(nameof(inputMode));
            if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("Domain is required.", nameof(domain));
            if (maxSourceLength < 1 || maxSourceLength > 1048576) throw new ArgumentOutOfRangeException(nameof(maxSourceLength));
            if (maxRegionLength < 1 || maxRegionLength > 4096) throw new ArgumentOutOfRangeException(nameof(maxRegionLength));
            if (maxRegions < 1 || maxRegions > 1024) throw new ArgumentOutOfRangeException(nameof(maxRegions));
            if ((enabledDomains & ~DetectionDomains.All) != 0) throw new ArgumentOutOfRangeException(nameof(enabledDomains));
            InputMode = inputMode; Domain = domain; Markers = markers ?? MarkerConfiguration.Default;
            MaxSourceLength = maxSourceLength; MaxRegionLength = maxRegionLength; MaxRegions = maxRegions;
            EnabledDomains = enabledDomains;
            MarkerProfiles = markerProfiles;
        }
    }

    /// <summary>Content results only: neither an IME/focus observation nor permission to write Word.</summary>
    public sealed class AnalysisResult
    {
        public SourceSnapshot Source { get; }
        public DetectionStatus Detection { get; }
        public IReadOnlyList<CandidateSet> Regions { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        public bool IsIncomplete { get; }
        public string ContentEligibility => Detection == DetectionStatus.Deferred ? "pending" :
            Regions.Count > 0 && Regions.All(r => r.ContentEligibility == "eligible") &&
            !Diagnostics.Any(d => d.Severity == "warning" || d.Severity == "error") ? "eligible" : "blocked";

        public AnalysisResult(SourceSnapshot source, DetectionStatus detection,
            IEnumerable<CandidateSet>? regions = null, IEnumerable<Diagnostic>? diagnostics = null, bool isIncomplete = false)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Detection = detection; Regions = Freeze.Of(regions); Diagnostics = Freeze.Of(diagnostics); IsIncomplete = isIncomplete;
            foreach (var region in Regions)
                if (region.Source.Id != source.Id) throw new ArgumentException("Region belongs to another snapshot.", nameof(regions));
            foreach (var diagnostic in Diagnostics) source.Validate(diagnostic.Span);
            for (int i = 1; i < Regions.Count; i++)
                if (Regions[i - 1].ReplacementSpan.End > Regions[i].ReplacementSpan.Start)
                    throw new ArgumentException("Regions must be ordered and cannot overlap.", nameof(regions));
        }
    }
}
