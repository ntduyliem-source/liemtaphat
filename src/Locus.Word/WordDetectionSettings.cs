using System;
using System.Linq;
using System.Web.Script.Serialization;
using Locus.Core;
using Locus.Core.Detection;

namespace Locus.Word;

public sealed class WordDetectionSettings
{
    public MarkerProfileSet Profiles { get; }
    public DetectionDomains Domains { get; }
    public MarkerConfiguration Common => Profiles.Profiles.Single(p => p.Id == "common").Markers;
    public string Fingerprint => ManualStateCodec.Digest(Serialize());
    public WordDetectionSettings(MarkerProfileSet profiles, DetectionDomains domains)
    {
        if (!profiles.IsValid || (domains & ~DetectionDomains.All) != 0) throw new ArgumentException(profiles.ValidationError ?? "invalid-detection-settings");
        Profiles = profiles; Domains = domains;
    }
    public static WordDetectionSettings Default => Upgrade(MarkerConfiguration.Default, DetectionDomains.Math);
    public static WordDetectionSettings Upgrade(MarkerConfiguration common, DetectionDomains domains)
    {
        var profiles = new[] { new MarkerProfile("common", "auto", common.Open, common.Close),
            new MarkerProfile("math", "math", "toan-[", "]"), new MarkerProfile("physics", "physics", "ly-[", "]"), new MarkerProfile("chemistry", "chemistry", "hoa-[", "]") };
        for (int i = 1; i < profiles.Length; i++)
            if (!new MarkerProfileSet(new[] { profiles[0], profiles[i] }).IsValid)
                profiles[i] = new MarkerProfile(profiles[i].Id, profiles[i].Domain, profiles[i].Markers.Open, profiles[i].Markers.Close, false);
        return new WordDetectionSettings(new MarkerProfileSet(profiles), domains);
    }
    public WordDetectionSettings WithCommon(string open, string close, DetectionDomains domains) =>
        new WordDetectionSettings(new MarkerProfileSet(Profiles.Profiles.Select(p => p.Id == "common" ? new MarkerProfile("common", "auto", open, close) : p)), domains);
    public string Serialize() => new JavaScriptSerializer().Serialize(new Data { Version = 1, Domains = (int)Domains,
        Profiles = Profiles.Profiles.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new ProfileData { Id=p.Id, Domain=p.Domain, Open=p.Markers.Open, Close=p.Markers.Close, Enabled=p.Enabled }).ToArray() });
    public static WordDetectionSettings Deserialize(string value)
    {
        if (value == null || value.Length > 16000) throw new FormatException("Invalid detection settings.");
        var json = new JavaScriptSerializer();
        if (value.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            var old = json.Deserialize<string[]>(value);
            if (old == null || old.Length is not (2 or 3) || old.Length == 3 && !int.TryParse(old[2], out _)) throw new FormatException("Invalid legacy settings.");
            return Upgrade(new MarkerConfiguration(old[0], old[1]), old.Length == 3 ? (DetectionDomains)int.Parse(old[2]) : DetectionDomains.Math);
        }
        var data = json.Deserialize<Data>(value);
        if (data == null || data.Version != 1 || data.Profiles == null || data.Profiles.Any(p => p == null)) throw new FormatException("Invalid detection settings.");
        return new WordDetectionSettings(new MarkerProfileSet(data.Profiles.Select(p => new MarkerProfile(p.Id, p.Domain, p.Open, p.Close, p.Enabled))), (DetectionDomains)data.Domains);
    }
    public sealed class Data { public int Version { get; set; } public int Domains { get; set; } public ProfileData[] Profiles { get; set; } = Array.Empty<ProfileData>(); }
    public sealed class ProfileData { public string Id { get; set; } = ""; public string Domain { get; set; } = ""; public string Open { get; set; } = ""; public string Close { get; set; } = ""; public bool Enabled { get; set; } }
}
