using System;
using System.Collections.Generic;
using System.Linq;

namespace Locus.Core.Detection
{
    public static class MarkerVersions
    {
        public const string Core = "locus-core/0.3";
        public const string Grammar = "sc1-markers/0.1";
        public const string Snapshot = "locus-candidate-set/0.3";
    }

    /// <summary>A literal wrapper declares the interpretation of this region, never permission to write.</summary>
    public sealed class MarkerProfile
    {
        public string Id { get; }
        public string Domain { get; }
        public MarkerConfiguration Markers { get; }
        public bool Enabled { get; }
        public MarkerProfile(string id, string domain, string open, string close, bool enabled = true)
        {
            if (!ValidIdentity(id, domain)) throw new ArgumentException("Invalid marker identity/domain.");
            Id = id; Domain = domain; Markers = new MarkerConfiguration(open, close); Enabled = enabled;
        }
        internal static bool ValidIdentity(string id, string domain) =>
            id == "common" && domain == "auto" || id == domain && (domain == "math" || domain == "physics" || domain == "chemistry");
        public DetectionDomains Resolve(DetectionDomains automatic) => Domain == "auto" ? automatic :
            Domain == "math" ? DetectionDomains.Math : Domain == "physics" ? DetectionDomains.Physics : DetectionDomains.Chemistry;
    }

    public sealed class MarkerProfileSet
    {
        public IReadOnlyList<MarkerProfile> Profiles { get; }
        public MarkerProfileSet(IEnumerable<MarkerProfile> profiles)
        {
            Profiles = Freeze.Of(profiles ?? throw new ArgumentNullException(nameof(profiles)));
            if (Profiles.Count < 1 || Profiles.Count > 4 || Profiles.Any(p => p == null) ||
                Profiles.Select(p => p.Id).Distinct().Count() != Profiles.Count || !Profiles.Any(p => p.Id == "common" && p.Enabled))
                throw new ArgumentException("One enabled common wrapper and at most three named wrappers are required.");
        }
        public string? ValidationError
        {
            get
            {
                foreach (var profile in Profiles)
                    if (!profile.Markers.IsValid) return "Cặp " + profile.Id + " có dấu mở/đóng chưa hợp lệ.";
                var active = Profiles.Where(p => p.Enabled).ToArray();
                for (int i = 0; i < active.Length; i++)
                    for (int j = i + 1; j < active.Length; j++)
                    {
                        var a = active[i].Markers; var b = active[j].Markers;
                        if (Prefix(a.Open, b.Open) || Contains(a.Open, b.Open) || Contains(b.Open, a.Open) ||
                            Overlap(a.Open, b.Open) || Overlap(b.Open, a.Open) ||
                            Prefix(a.Open, b.Close) || Prefix(b.Open, a.Close))
                            return "Cặp " + active[i].Id + " và " + active[j].Id + " có dấu xung đột.";
                    }
                return null;
            }
        }
        public bool IsValid => ValidationError == null;
        private static bool Prefix(string a, string b) => a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal);
        private static bool Contains(string a, string b) => a.IndexOf(b, StringComparison.Ordinal) >= 0;
        private static bool Overlap(string a, string b)
        {
            for (int n = 1; n < Math.Min(a.Length, b.Length); n++)
                if (string.Compare(a, a.Length - n, b, 0, n, StringComparison.Ordinal) == 0) return true;
            return false;
        }
    }

    /// <summary>Immutable provenance retained with a complete region, independent of current preferences.</summary>
    public sealed class RegionIntent
    {
        public string ProfileId { get; }
        public string Domain { get; }
        public string GrammarVersion => MarkerVersions.Grammar;
        public RegionIntent(string profileId, string domain)
        {
            if (!MarkerProfile.ValidIdentity(profileId, domain)) throw new ArgumentException("Invalid region intent.");
            ProfileId = profileId; Domain = domain;
        }
    }
}
