using Locus.Core;
using Locus.Core.Detection;

namespace Locus.Application;

public sealed record MarkerLiteral(string Open, string Close, bool Enabled = true);

/// <summary>Fixed identities make settings value-comparable; arrays cannot invalidate equality or leases.</summary>
public sealed record MarkerPreferences(int Version, MarkerLiteral Math, MarkerLiteral Physics, MarkerLiteral Chemistry)
{
    public static MarkerPreferences Default => new(1, new("toan-[", "]"), new("ly-[", "]"), new("hoa-[", "]"));
    public MarkerProfileSet ToProfiles(string open, string close)
    {
        if (Version != 1 || Math == null || Physics == null || Chemistry == null) throw new ArgumentException("Unsupported marker preferences.");
        return new MarkerProfileSet(new[]
        {
            new MarkerProfile("common", "auto", open, close),
            new MarkerProfile("math", "math", Math.Open, Math.Close, Math.Enabled),
            new MarkerProfile("physics", "physics", Physics.Open, Physics.Close, Physics.Enabled),
            new MarkerProfile("chemistry", "chemistry", Chemistry.Open, Chemistry.Close, Chemistry.Enabled)
        });
    }
    public static (MarkerPreferences Profiles, string Notice) Migrate(string open, string close)
    {
        if (!new MarkerConfiguration(open, close).IsValid) throw new ArgumentException("Cặp bọc hiện tại chưa hợp lệ; sửa cặp chung trước khi thêm cặp theo môn.");
        var original = Default;
        var result = new MarkerPreferences(1, original.Math with { Enabled = false }, original.Physics with { Enabled = false }, original.Chemistry with { Enabled = false });
        var notices = new List<string>();
        foreach (var id in new[] { "math", "physics", "chemistry" })
        {
            var proposal = id == "math" ? result with { Math = original.Math } : id == "physics" ? result with { Physics = original.Physics } : result with { Chemistry = original.Chemistry };
            if (proposal.ToProfiles(open, close).IsValid) result = proposal;
            else notices.Add(id == "math" ? "Toán" : id == "physics" ? "Lý" : "Hóa");
        }
        return (result, notices.Count == 0 ? "" : "Đã giữ cặp chung. Cặp " + string.Join(", ", notices) + " chưa bật vì trùng dấu; có thể sửa trong cài đặt cặp bọc.");
    }
}
