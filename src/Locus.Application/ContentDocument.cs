using System.Collections.ObjectModel;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Export;

namespace Locus.Application;

/// <summary>A formula snapshot uses coordinates in its bounded source window, never DOM coordinates.</summary>
public sealed record ContentRegion(Guid Id, long Revision, int Origin, int Start, int End,
    string Window, CandidateSet? Readings, string? SelectedId, bool KeepText = false,
    bool KeepFromAuto = false, string? Problem = null, ContentResultOverride? ResultOverride = null)
{
    public Candidate? Selected => Readings?.Candidates.FirstOrDefault(c => c.Id == SelectedId);
    public Candidate? Display => KeepText ? null : ResultOverride?.Candidate ?? Selected;
    public string Raw => Window.Substring(Start - Origin, End - Start);
}

public sealed record ContentBlock(int Start, int End, ContentRegion? Region);
public sealed record ContentSelection(int Start, int End, IReadOnlyList<Guid> Whole, IReadOnlyList<Guid> Partial);

/// <summary>Immutable text + formula document. Selection, reads and decisions refer to stable region IDs.</summary>
public sealed class ContentDocument
{
    public const int AnalysisLimit = 100_000;
    public const int RegionLimit = 256;
    public string Raw { get; }
    public long Revision { get; }
    public IReadOnlyList<ContentRegion> Regions { get; }
    public IReadOnlyList<string> Notices { get; }
    public IReadOnlyList<ContentCommand> Commands { get; }
    public ContentDocument(string raw, long revision, IEnumerable<ContentRegion> regions, IEnumerable<string>? notices = null, IEnumerable<ContentCommand>? commands = null)
    {
        Raw = raw; Revision = revision;
        Regions = new ReadOnlyCollection<ContentRegion>(regions.ToArray());
        Notices = new ReadOnlyCollection<string>((notices ?? []).Distinct().ToArray());
        Commands = new ReadOnlyCollection<ContentCommand>((commands ?? []).ToArray());
        Validate();
    }
    public IEnumerable<ContentBlock> Blocks()
    {
        int end = 0;
        foreach (var region in Regions)
        {
            if (region.Start > end) yield return new(end, region.Start, null);
            yield return new(region.Start, region.End, region); end = region.End;
        }
        if (end < Raw.Length) yield return new(end, Raw.Length, null);
    }
    public ContentSelection Select(int anchor, int focus)
    {
        int start = Math.Min(anchor, focus), end = Math.Max(anchor, focus);
        new SourceSnapshot(Raw).Validate(new TextSpan(start, end));
        var whole = new List<Guid>(); var partial = new List<Guid>();
        if (start != end) foreach (var region in Regions)
        {
            if (region.End <= start || region.Start >= end) continue;
            (region.Start >= start && region.End <= end ? whole : partial).Add(region.Id);
        }
        return new(start, end, whole.AsReadOnly(), partial.AsReadOnly());
    }
    public string ResultText(int start, int end)
    {
        Select(start, end);
        var text = new System.Text.StringBuilder(); int cursor = start;
        foreach (var region in Regions.Where(r => r.Start >= start && r.End <= end))
        {
            text.Append(Raw, cursor, region.Start - cursor);
            // Text export is explicit LaTeX for formula regions; never pretend this is native Word.
            text.Append(region.Display is {} candidate ? CandidateExporter.ToLatex(candidate) : region.Raw);
            cursor = region.End;
        }
        return text.Append(Raw, cursor, end - cursor).ToString();
    }
    public ContentDocument Replace(ContentRegion replacement) => new(Raw, Revision,
        Regions.Select(r => r.Id == replacement.Id ? replacement : r), Notices, Commands);
    public ContentDocument Change(ContentRegion replacement,string kind)
    {
        var before=Regions.Single(r=>r.Id==replacement.Id);
        var command=new ContentCommand(Guid.NewGuid(),kind,[new(before,replacement)]);
        return new(Raw,Revision,Regions.Select(r=>r.Id==replacement.Id?replacement:r),Notices,
            Commands.Append(command).TakeLast(FormulaSession.HistoryLimit));
    }
    public void Validate()
    {
        if (Raw == null || Raw.Length > 1_048_576 || Revision < 0 || Revision == long.MaxValue || Regions.Count > RegionLimit || Notices.Count > 64 || Notices.Any(n => n == null || n.Length > 512)) throw new FormatException("Invalid content document.");
        var source = new SourceSnapshot(Raw, Revision); int end = 0; var ids = new HashSet<Guid>();
        if(Commands.Count>FormulaSession.HistoryLimit||Commands.Any(c=>c==null)||Commands.Select(c=>c.Id).Distinct().Count()!=Commands.Count)throw new FormatException("Invalid content history.");
        foreach (var r in Regions)
        {
            if (r == null || r.Id == Guid.Empty || !ids.Add(r.Id) || r.Revision < 0 || r.Revision == long.MaxValue || r.Window == null || r.Window.Length > FormulaSession.MaxSourceLength ||
                r.Origin < 0 || r.Start < r.Origin || r.End <= r.Start || r.End > Raw.Length || r.Start < end || (long)r.Origin + r.Window.Length > Raw.Length || r.End > r.Origin + r.Window.Length ||
                !Raw.AsSpan(r.Origin, r.Window.Length).SequenceEqual(r.Window) || r.Problem?.Length > 512) throw new FormatException("Invalid content region.");
            source.Validate(new TextSpan(r.Start, r.End));
            if (r.Readings is {} readings && (readings.Source.Raw != r.Window || readings.Source.Revision != Revision || readings.ReplacementSpan.Start + r.Origin != r.Start || readings.ReplacementSpan.End + r.Origin != r.End)) throw new FormatException("Region snapshot/source mismatch.");
            if (r.SelectedId != null && r.Selected == null) throw new FormatException("Unknown region reading.");
            r.ResultOverride?.Validate(r);
            end = r.End;
        }
    }

    internal static ContentRegion FromSet(CandidateSet set, int origin)
    {
        var direct = set.Candidates.Where(c => c.Kind != "repair").ToArray();
        var chosen = direct.Length == 1 && direct[0].Kind == "direct" && !set.Diagnostics.Any(d => d.Severity == "error") ? direct[0].Id : null;
        return new(Guid.NewGuid(), 0, origin, origin + set.ReplacementSpan.Start, origin + set.ReplacementSpan.End, set.Source.Raw, set, chosen,
            Problem: chosen == null ? "Chọn cách đọc trong Chi tiết công thức; nội dung đang được giữ là text." : null);
    }

    /// <summary>Only untouched prefix/suffix spans retain decisions. Equal repeated text is not an identity.</summary>
    internal static IEnumerable<ContentRegion> Reconcile(string raw, IEnumerable<ContentRegion> regions, ContentDocument? previous)
    {
        if (previous == null) return regions;
        int prefix = 0, suffix = 0;
        while (prefix < raw.Length && prefix < previous.Raw.Length && raw[prefix] == previous.Raw[prefix]) prefix++;
        while (suffix < raw.Length - prefix && suffix < previous.Raw.Length - prefix && raw[raw.Length - 1 - suffix] == previous.Raw[previous.Raw.Length - 1 - suffix]) suffix++;
        int delta = raw.Length - previous.Raw.Length;
        return regions.Select(current =>
        {
            var old = previous.Regions.FirstOrDefault(r =>
                (r.End <= prefix && r.Start == current.Start && r.End == current.End ||
                 r.Start >= previous.Raw.Length - suffix && r.Start + delta == current.Start && r.End + delta == current.End) && r.Raw == current.Raw);
            if (old == null) return current;
            string? selected = current.SelectedId;
            if (old.SelectedId != null && old.Readings != null && current.Readings != null)
            {
                var before = old.Selected!;
                selected = current.Readings.Candidates.FirstOrDefault(c => c.Kind == before.Kind && c.Document.Domain == before.Document.Domain &&
                    CandidateExporter.ToLatex(c) == CandidateExporter.ToLatex(before))?.Id;
            }
            return current with { Id = old.Id, Revision = old.Revision, SelectedId = selected, KeepText = old.KeepText, KeepFromAuto = old.KeepFromAuto,
                ResultOverride = old.ResultOverride is {} result && (selected!=null||result.ProductProposal!=null) ? result.Reanchor(selected??"draft:"+old.Id) : null };
        }).ToArray();
    }
}

public static class ContentAnalyzer
{
    public static async Task<ContentDocument> AnalyzeAsync(AnalysisRequest request, IAnalysisScheduler scheduler, ContentDocument? previous, CancellationToken token)
    {
        var raw = request.Raw; var regions = new List<ContentRegion>(); var notices = new List<string>();
        if (raw.Length > ContentDocument.AnalysisLimit)
            return new(raw, request.SourceRevision, [], ["Đoạn vượt 100.000 ký tự; nguồn giữ nguyên. Chọn đoạn ngắn hơn để xử lý."]);
        var settings = request.Settings;
        var profiles = settings.MarkerProfiles ?? MarkerPreferences.Migrate(settings.Open, settings.Close).Profiles;
        var scan = ProfileMarkerScanner.Scan(new(raw, request.SourceRevision), profiles.ToProfiles(settings.Open, settings.Close), maxRegions: ContentDocument.RegionLimit, cancellationToken: token);
        if (scan.LimitExceeded) return new(raw, request.SourceRevision, [], ["Có quá nhiều vùng trong một đoạn. Nguồn giữ nguyên."]);
        // Prefer the exact single-input path, including existing SC1 case and marker behavior.
        if (raw.Length <= FormulaSession.MaxSourceLength && !raw.Contains('\n') && !raw.Contains('\r') && settings.Mode != InputMode.Markers)
        {
            var one = await scheduler.AnalyzeAsync(request with { Settings = settings with { Mode = InputMode.Explicit } }, token);
            token.ThrowIfCancellationRequested(); Verify(one, raw, request.SourceRevision);
            if (one.Regions.Count == 1 && raw[..one.Regions[0].ReplacementSpan.Start].Trim().Length == 0 && raw[one.Regions[0].ReplacementSpan.End..].Trim().Length == 0)
                return new(raw, request.SourceRevision, ContentDocument.Reconcile(raw, [ContentDocument.FromSet(one.Regions[0], 0)], previous),commands:previous?.Commands);
        }
        int cursor = 0;
        foreach (var reserved in scan.ReservedSpans)
        {
            if (settings.Mode != InputMode.Markers) await Gap(cursor, reserved.Start);
            await Window(reserved.Start, reserved.End, InputMode.Explicit, true);
            cursor = reserved.End;
        }
        if (settings.Mode != InputMode.Markers) await Gap(cursor, raw.Length);
        token.ThrowIfCancellationRequested();
        return new(raw, request.SourceRevision, ContentDocument.Reconcile(raw, regions, previous), notices,previous?.Commands);

        async Task Gap(int start, int end)
        {
            while (start < end)
            {
                token.ThrowIfCancellationRequested();
                int stop = end;
                if (end - start > FormulaSession.MaxSourceLength)
                {
                    stop = start + FormulaSession.MaxSourceLength;
                    while (stop > start && raw[stop-1] != '\r' && raw[stop-1] != '\n') stop--;
                    if (stop == start)
                    {
                        stop = start + FormulaSession.MaxSourceLength;
                        while (stop < end && raw[stop] != '\r' && raw[stop] != '\n') stop++;
                    }
                }
                await Window(start, stop, InputMode.Passive, false);
                start = stop;
            }
        }
        async Task Window(int start, int end, InputMode mode, bool marked)
        {
            if (start >= end || string.IsNullOrWhiteSpace(raw[start..end])) return;
            if (end - start > FormulaSession.MaxSourceLength) { Notice("Một dòng/vùng vượt 4.096 ký tự nên được giữ là text; tách dòng để xử lý tiếp."); return; }
            if (regions.Count >= ContentDocument.RegionLimit) { Notice("Đã tới giới hạn 256 vùng; phần còn lại được giữ là text."); return; }
            var window = raw[start..end];
            var result = await scheduler.AnalyzeAsync(new(window, request.SourceRevision, settings with { Mode = mode, MarkerProfiles = profiles }), token);
            token.ThrowIfCancellationRequested(); Verify(result, window, request.SourceRevision);
            foreach (var set in result.Regions)
            {
                if (regions.Count >= ContentDocument.RegionLimit) { Notice("Đã tới giới hạn 256 vùng; phần còn lại được giữ là text."); break; }
                regions.Add(ContentDocument.FromSet(set, start));
            }
            if (marked && result.Regions.Count == 0)
                regions.Add(new(Guid.NewGuid(), 0, start, start, end, window, null, null, Problem: "Chưa đọc được vùng này. Sửa nguồn hoặc giữ nguyên text."));
            if (result.Diagnostics.Any(d => d.Code.Contains("LIMIT", StringComparison.Ordinal))) Notice("Một số vùng vượt giới hạn phân tích nên được giữ là text.");
        }
        void Notice(string message) { if (!notices.Contains(message)) notices.Add(message); }
    }
    private static void Verify(AnalysisResult result, string raw, long revision)
    { if (result.Source.Raw != raw || result.Source.Revision != revision) throw new FormatException("Analysis window mismatch."); }
}
