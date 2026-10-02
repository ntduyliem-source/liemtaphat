using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Locus.Core;
using Locus.Core.Detection;
using Locus.Core.Assistance;
using Locus.Core.Plotting;

namespace Locus.Application;

public sealed record FormulaView(double FontSize = 40, double PixelScale = 2, bool WhiteBackground = false)
{
    public void Validate()
    {
        if (!double.IsFinite(FontSize) || FontSize < 16 || FontSize > 96 || !double.IsFinite(PixelScale) || PixelScale < 1 || PixelScale > 4) throw new FormatException("Unsupported image dimensions.");
    }
}

public abstract record WorkspaceDocument(Guid Id, long Revision);
public sealed record FormulaDocument(Guid Id, long Revision, FormulaState State, FormulaView View) : WorkspaceDocument(Id, Revision);
public sealed record PlotViewport(double Left = -5, double Top = 5, double Right = 5, double Bottom = -5);
public sealed record PlotCurve(string Id, string Raw, string Color = "#28705c", string LineStyle = "solid", bool Visible = true,
    double Width = 2.5, double? DomainMin = null, double? DomainMax = null, string? AstSnapshot = null,
    string? InterpretedRaw = null, string? InterpretationKind = null);
public sealed record PlotParameter(string Name, double? Value = null, double Min = -5, double Max = 5, double Step = 0.1, bool Pinned = false);
public sealed record PlotAxes(bool Grid = true, bool Axes = true, string XLabel = "x", string YLabel = "y");
public sealed record PlotDocument(Guid Id, long Revision, PlotCurve[] Curves, PlotViewport Viewport,
    PlotParameter[]? Parameters = null, PlotAxes? Axes = null, string? Grammar = null) : WorkspaceDocument(Id, Revision);
public sealed record GeometryPoint(string Id, double X, double Y, string Label, double Z = 0, GeometryRelation? Relation = null, bool Visible = true, double LabelX = 10, double LabelY = -12);
public sealed record GeometryRelation(string Kind, string[] Parents, double Value = 0, double Distance = 1, bool Attached = false);
public sealed record GeometrySegment(string Id, string Start, string End, string LineStyle = "solid", string Color = "#28705c", double Width = 2, string Kind = "segment", bool Visible = true, string Hidden = "auto", int Marks = 0);
public sealed record GeometryCircle(string Id, string Center, string Through, string Color = "#28705c", string LineStyle = "solid", double Width = 2, bool Visible = true);
public sealed record GeometryAngle(string Id, string A, string Vertex, string C, bool Reflex = false, string Unit = "degree", int Arcs = 1, double Radius = 32, string Label = "", string Color = "#ad7140", string LineStyle = "solid", bool ShowValue = true);
public sealed record GeometryFace(string Id, string[] Vertices, string Color = "#7eb69c", double Opacity = 0.14, bool Visible = true);
public sealed record GeometryLabel(string Id, double X, double Y, string Text, double Z = 0, string Color = "#344c40");
public sealed record GeometryCamera(double Azimuth = -35, double Elevation = 25, double Scale = 65, double PanX = 0, double PanY = 0);
public sealed record GeometryDocument(Guid Id, long Revision, GeometryPoint[] Points, GeometrySegment[] Segments,
    GeometryCircle[]? Circles = null, GeometryAngle[]? Angles = null, GeometryFace[]? Faces = null, GeometryLabel[]? Labels = null,
    PlotViewport? Viewport = null, int Dimension = 2, GeometryCamera? Camera = null) : WorkspaceDocument(Id, Revision);
public sealed record OpenDocumentResult(WorkspaceDocument? Document, string OriginalJson, string? UnavailableReason)
{
    public bool IsSupported => Document != null;
}

/// <summary>Versioned local interchange, independent of host. Unsupported or invalid bytes are retained by the caller.</summary>
public static class DocumentCodec
{
    public const int MaxBytes = 8_000_000;
    public const string Format = "locus-document";
    private sealed record Envelope(string Format, int Version, string Kind, Guid Id, long Revision, string Payload, string Sha256);
    private sealed record FormulaData(string Raw, long SourceRevision, FormulaSettings Settings, string? Analysis, int RegionIndex, string? CandidateId, FormulaView View,
        [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] TransformationData[]? Transformations=null,
        [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] ContentData? Content=null);
    private sealed record ContentData(RegionData[] Regions, string[] Notices,
        [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] CommandData[]? Commands=null);
    private sealed record RegionData(Guid Id, long Revision, int Origin, int Start, int End, string Window, string? Snapshot, string? SelectedId, bool KeepText, bool KeepFromAuto, string? Problem,
        [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] OverrideData? ResultOverride=null);
    private sealed record OverrideData(string BaseReadingId,string Snapshot,string CandidateId,string Provenance,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool ManagedBalance=false,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] OverrideData? BalanceBefore=null,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? ProductProposal=null);
    private sealed record ChangeData(RegionData Before,RegionData After);
    private sealed record CommandData(Guid Id,string Kind,ChangeData[] Changes);
    private sealed record TransformationData(string Proposal, FormulaData Before, string AfterRaw, long AfterRevision, EditorSelection SelectionBefore, EditorSelection SelectionAfter, bool AppendedSpace);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 96,
        Converters = { new JsonStringEnumConverter() }
    };
    public static string Serialize(WorkspaceDocument document)
    {
        ValidateIdentity(document.Id, document.Revision);
        string kind, payload;
        switch (document)
        {
            case FormulaDocument formula:
                ValidateFormula(formula.State, formula.View);
                kind = "formula";
                payload = JsonSerializer.Serialize(ToData(formula.State,formula.View), Options);
                break;
            case PlotDocument plot: ValidatePlot(plot); kind = "plot"; payload = JsonSerializer.Serialize(plot, Options); break;
            case GeometryDocument geometry: ValidateGeometry(geometry); kind = "geometry"; payload = JsonSerializer.Serialize(geometry, Options); break;
            default: throw new NotSupportedException("Unsupported document kind.");
        }
        int formatVersion = document is PlotDocument ? 10 : document is GeometryDocument ? 9 : document is FormulaDocument f ? HasManagedResults(f.State) ? 7 : HasContentCommands(f.State) ? 6 : f.State.Content != null || f.State.Transformations?.Entries.Any(t=>t.Before.Content!=null)==true ? 5 : f.State.Transformations != null ? 4 : f.State.Settings.MarkerProfiles != null ? 3 : 2 : 1;
        var json = JsonSerializer.Serialize(new Envelope(Format, formatVersion, kind, document.Id, document.Revision, payload, Hash(payload)), Options);
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new FormatException("Document too large.");
        return json;
    }
    public static OpenDocumentResult Open(string json)
    {
        try { return new(Read(json), json, null); }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException or InvalidOperationException or OverflowException or NotSupportedException)
        { return new(null, json, "Tệp chưa được hỗ trợ hoặc không hợp lệ. Giữ nguyên tệp gốc; phiên hiện tại không thay đổi."); }
    }
    private static WorkspaceDocument Read(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new FormatException("Document too large.");
        using var parsed = JsonDocument.Parse(json);
        RejectDuplicateKeys(parsed.RootElement);
        var envelope = JsonSerializer.Deserialize<Envelope>(json, Options) ?? throw new FormatException("Missing document.");
        if (envelope.Format != Format || envelope.Version is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10) || (envelope.Version is >= 2 and <= 7 && envelope.Kind != "formula") || (envelope.Version is 8 or 10 && envelope.Kind != "plot") || (envelope.Version == 9 && envelope.Kind != "geometry") || envelope.Payload == null || envelope.Sha256 != Hash(envelope.Payload)) throw new FormatException("Envelope version/checksum mismatch.");
        ValidateIdentity(envelope.Id, envelope.Revision);
        using var payload = JsonDocument.Parse(envelope.Payload); RejectDuplicateKeys(payload.RootElement);
        switch (envelope.Kind)
        {
            case "formula":
                var data = JsonSerializer.Deserialize<FormulaData>(envelope.Payload, Options) ?? throw new FormatException();
                var state = FromData(data);
                ValidateFormula(state, data.View);
                if(envelope.Version<7&&HasManagedResults(state))throw new FormatException("Managed balance/products requires version 7.");
                if(envelope.Version<6&&HasContentCommands(state))throw new FormatException("Regional result/history requires document version 6.");
                if (envelope.Version < 5 && (state.Content != null || state.Transformations?.Entries.Any(t=>t.Before.Content!=null)==true)) throw new FormatException("Content requires document version 5.");
                if (envelope.Version < 4 && state.Transformations != null) throw new FormatException("Transformation history requires document version 4.");
                if (envelope.Version < 3 && (state.Settings.MarkerProfiles != null || state.Analysis?.Regions.Any(r => r.Intent != null) == true)) throw new FormatException("Marker profiles require document version 3.");
                if (envelope.Version == 1 && (state.Settings.EnabledDomains != DetectionDomains.Math || state.Analysis?.Regions.Any(r => r.Candidates.Any(c => c.Document.Domain != "math")) == true)) throw new FormatException("Scientific data requires document version 2.");
                return new FormulaDocument(envelope.Id, envelope.Revision, state, data.View);
            case "plot":
                var plot = JsonSerializer.Deserialize<PlotDocument>(envelope.Payload, Options) ?? throw new FormatException();
                if (plot.Id != envelope.Id || plot.Revision != envelope.Revision) throw new FormatException("Identity mismatch.");
                if (envelope.Version < 8 && (plot.Parameters != null || plot.Axes != null || plot.Grammar != null || plot.Curves?.Any(c => c == null || c.AstSnapshot != null || c.Width != 2.5 || c.DomainMin != null || c.DomainMax != null || c.LineStyle == "dotted") == true)) throw new FormatException("Plot editor data requires version 8.");
                if (envelope.Version < 10 && plot.Curves?.Any(c => c != null && (c.InterpretedRaw != null || c.InterpretationKind != null)) == true) throw new FormatException("Plot interpretation data requires version 10.");
                ValidatePlot(plot); return plot;
            case "geometry":
                var geometry = JsonSerializer.Deserialize<GeometryDocument>(envelope.Payload, Options) ?? throw new FormatException();
                if (geometry.Id != envelope.Id || geometry.Revision != envelope.Revision) throw new FormatException("Identity mismatch.");
                if (envelope.Version < 9 && (geometry.Circles != null || geometry.Angles != null || geometry.Faces != null || geometry.Labels != null || geometry.Viewport != null || geometry.Dimension != 2 || geometry.Camera != null || geometry.Points?.Any(p => p == null || p.Relation != null || p.Z != 0 || !p.Visible || p.LabelX != 10 || p.LabelY != -12) == true || geometry.Segments?.Any(s=>s==null||s.Kind!="segment"||s.Color!="#28705c"||s.Width!=2||!s.Visible||s.Hidden!="auto"||s.Marks!=0)==true)) throw new FormatException("Geometry editor data requires version 9.");
                ValidateGeometry(geometry); return geometry;
            default: throw new NotSupportedException("Unknown document kind.");
        }
    }
    private static void ValidateIdentity(Guid id, long revision)
    { if (id == Guid.Empty || revision < 0 || revision == long.MaxValue) throw new FormatException("Invalid document identity/revision."); }
    private static FormulaData ToData(FormulaState state, FormulaView view) => new(state.Raw,state.SourceRevision,state.Settings,
        state.Analysis==null?null:AnalysisWire.Serialize(state.Analysis),state.RegionIndex,state.CandidateId,view,
        state.Transformations?.Entries.Select(t=>new TransformationData(AssistanceSerializer.Serialize(t.Proposal),ToData(t.Before,new()),t.After.Raw,t.After.Revision,t.SelectionBefore,t.SelectionAfter,t.AppendedSpace)).ToArray(),
        state.Content == null ? null : new(state.Content.Regions.Select(ToRegion).ToArray(),state.Content.Notices.ToArray(),
            state.Content.Commands.Count==0?null:state.Content.Commands.Select(c=>new CommandData(c.Id,c.Kind,c.Changes.Select(d=>new ChangeData(ToRegion(d.Before),ToRegion(d.After))).ToArray())).ToArray()));
    private static bool HasContentCommands(FormulaState state)=>(state.Content is {} c&&(c.Commands.Count>0||c.Regions.Any(r=>r.ResultOverride!=null)))||state.Transformations?.Entries.Any(t=>HasContentCommands(t.Before))==true;
    private static bool HasManagedResults(FormulaState state)=>(state.Content is {} c&&c.Regions.Concat(c.Commands.SelectMany(command=>command.Changes.SelectMany(change=>new[]{change.Before,change.After}))).Any(r=>r.ResultOverride is {} result&&(result.ManagedBalance||result.BalanceBefore!=null||result.ProductProposal!=null)))||state.Transformations?.Entries.Any(t=>HasManagedResults(t.Before))==true;
    private static RegionData ToRegion(ContentRegion r)=>new(r.Id,r.Revision,r.Origin,r.Start,r.End,r.Window,r.Readings==null?null:Locus.Core.Serialization.CandidateSetSerializer.Serialize(r.Readings),r.SelectedId,r.KeepText,r.KeepFromAuto,r.Problem,
        r.ResultOverride is not {} result?null:ToOverride(result));
    private static OverrideData ToOverride(ContentResultOverride result)=>new(result.BaseReadingId,Locus.Core.Serialization.CandidateSetSerializer.Serialize(result.Result),result.CandidateId,result.Provenance,result.ManagedBalance,result.BalanceBefore==null?null:ToOverride(result.BalanceBefore),result.ProductProposal);
    private static ContentResultOverride FromOverride(OverrideData result,int depth=0)
    {
        if(depth>1)throw new FormatException("Nested balance history.");
        return new(result.BaseReadingId,Locus.Core.Serialization.CandidateSetSerializer.Deserialize(result.Snapshot),result.CandidateId,result.Provenance,result.ManagedBalance,result.BalanceBefore==null?null:FromOverride(result.BalanceBefore,depth+1),result.ProductProposal);
    }
    private static ContentRegion FromRegion(RegionData r)
    {
        if(r==null)throw new FormatException("Missing region.");
        return new(r.Id,r.Revision,r.Origin,r.Start,r.End,r.Window,r.Snapshot==null?null:Locus.Core.Serialization.CandidateSetSerializer.Deserialize(r.Snapshot),r.SelectedId,r.KeepText,r.KeepFromAuto,r.Problem,
            r.ResultOverride is not {} result?null:FromOverride(result));
    }
    private static FormulaState FromData(FormulaData data)
    {
        if(data.View==null)throw new FormatException("Missing formula view.");data.View.Validate();
        AssistanceHistory? history=null;
        if(data.Transformations!=null)
        {
            if(data.Transformations.Length==0||data.Transformations.Length>AssistanceHistory.Limit||data.Transformations.Any(t=>t==null||t.Before==null||t.Before.Transformations!=null))throw new FormatException("Invalid or nested transformation history.");
            history=new(data.Transformations.Select(t=>new AcceptedTransformation(AssistanceSerializer.Deserialize(t.Proposal),FromData(t.Before),new SourceSnapshot(t.AfterRaw,t.AfterRevision),t.SelectionBefore,t.SelectionAfter,t.AppendedSpace)));
        }
        ContentDocument? content = null;
        if (data.Content is {} c)
        {
            if(c.Regions==null||c.Regions.Length>ContentDocument.RegionLimit||c.Regions.Any(r=>r==null)||c.Notices==null)throw new FormatException("Invalid content.");
            if(c.Commands is {} commands&&(commands.Length>FormulaSession.HistoryLimit||commands.Any(command=>command==null||command.Changes==null||command.Changes.Length>ContentDocument.RegionLimit||command.Changes.Any(change=>change==null))))throw new FormatException("Invalid command data.");
            content = new(data.Raw,data.SourceRevision,c.Regions.Select(FromRegion),c.Notices,
                c.Commands?.Select(command=>new ContentCommand(command.Id,command.Kind,command.Changes.Select(change=>new ContentChange(FromRegion(change.Before),FromRegion(change.After))))));
        }
        return new(data.Raw,data.SourceRevision,data.Settings,data.Analysis==null?null:AnalysisWire.Deserialize(data.Analysis),data.RegionIndex,data.CandidateId,history,content);
    }
    internal static void ValidateFormula(FormulaState state, FormulaView view)
    {
        if (state.Raw == null || state.Raw.Length > 1_048_576 || state.SourceRevision < 0 || state.SourceRevision == long.MaxValue || state.Settings == null || view == null) throw new FormatException("Invalid formula.");
        _ = new SourceSnapshot(state.Raw, state.SourceRevision); view.Validate();
        if (!state.Settings.HasValidDomains || !state.Settings.HasValidMarkerProfiles || !Enum.IsDefined(state.Settings.Mode) || state.Settings.Open == null || state.Settings.Close == null || state.Settings.Open.Length > 64 || state.Settings.Close.Length > 64) throw new FormatException("Invalid settings.");
        if (state.Analysis != null && (state.Analysis.Source.Raw != state.Raw || state.Analysis.Source.Revision != state.SourceRevision)) throw new FormatException("Source mismatch.");
        if (state.CandidateId != null && state.Candidate == null) throw new FormatException("Unknown selected candidate.");
        if (state.Content is {} content)
        {
            content.Validate();
            if(content.Raw!=state.Raw||content.Revision!=state.SourceRevision)throw new FormatException("Content source mismatch.");
            if(state.CandidateId!=null&&state.Content.Regions.ElementAtOrDefault(state.RegionIndex)?.Display?.Id!=state.CandidateId)throw new FormatException("Content selection mismatch.");
        }
        if (state.RegionIndex < 0 || (state.RegionIndex != 0 && (state.Content != null ? state.RegionIndex >= state.Content.Regions.Count : state.Region == null))) throw new FormatException("Unknown region.");
        if (state.Analysis != null && ((state.Settings.Mode == InputMode.Markers && !new MarkerConfiguration(state.Settings.Open, state.Settings.Close).IsValid) || state.Analysis.Regions.Any(r => !MarkerMatches(state.Settings, r)))) throw new FormatException("Settings/snapshot mismatch.");
    }
    private static bool MarkerMatches(FormulaSettings settings, CandidateSet region)
    {
        if (settings.MarkerProfiles == null)
            return region.Intent == null && (settings.Mode == InputMode.Markers ? region.Markers?.Open == settings.Open && region.Markers?.Close == settings.Close : region.Markers == null);
        if (region.Markers == null) return settings.Mode != InputMode.Markers && region.Intent == null;
        var profile = settings.MarkerProfiles.ToProfiles(settings.Open, settings.Close).Profiles.FirstOrDefault(p => p.Id == (region.Intent?.ProfileId ?? "common"));
        return profile != null && profile.Markers.Open == region.Markers.Open && profile.Markers.Close == region.Markers.Close && (region.Intent == null || profile.Domain == region.Intent.Domain);
    }
    internal static void ValidatePlot(PlotDocument plot)
    {
        if (plot.Curves == null || plot.Curves.Length > 32 || plot.Viewport == null || plot.Grammar != null && plot.Grammar != Locus.Core.Plotting.PlotExpression.Grammar) throw new FormatException("Invalid plot/grammar.");
        var v = plot.Viewport;
        if (!new[] { v.Left, v.Right, v.Top, v.Bottom }.All(Coordinate) || v.Right - v.Left < 1e-8 || v.Top - v.Bottom < 1e-8) throw new FormatException("Invalid viewport.");
        var ids = new HashSet<string>();
        foreach (var curve in plot.Curves)
        {
            if (curve == null || !Identifier(curve.Id) || !ids.Add(curve.Id) || curve.Raw == null || curve.Raw.Length > Locus.Core.Plotting.PlotExpression.MaxLength || curve.LineStyle is not ("solid" or "dashed" or "dotted") || curve.Color == null || !System.Text.RegularExpressions.Regex.IsMatch(curve.Color, "\\A#[0-9a-fA-F]{6}\\z") || !double.IsFinite(curve.Width) || curve.Width < 0.5 || curve.Width > 12 || curve.DomainMin.HasValue && !Coordinate(curve.DomainMin.Value) || curve.DomainMax.HasValue && !Coordinate(curve.DomainMax.Value) || curve.DomainMin >= curve.DomainMax) throw new FormatException("Invalid curve.");
            _ = new SourceSnapshot(curve.Raw);
            if ((curve.InterpretedRaw == null) != (curve.InterpretationKind == null) || curve.InterpretationKind != null && curve.InterpretationKind is not ("alternative" or "repair")) throw new FormatException("Invalid plot interpretation.");
            string selectedRaw = curve.Raw;
            if (curve.InterpretedRaw != null)
            {
                var selected = PlotInterpretationSet.Analyze(curve.Raw).FirstOrDefault(i => i.Raw == curve.InterpretedRaw && i.Kind == curve.InterpretationKind);
                if (selected == null) throw new FormatException("Unknown plot interpretation.");
                selectedRaw = selected.Raw;
            }
            if (curve.AstSnapshot != null && (plot.Grammar == null || curve.AstSnapshot != PlotExpression.Parse(selectedRaw).Snapshot)) throw new FormatException("Plot source/snapshot mismatch.");
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (plot.Parameters is {} parameters)
        {
            if (parameters.Length > 1024) throw new FormatException("Too many parameters.");
            foreach (var p in parameters)
                if (p == null || p.Name == null || !System.Text.RegularExpressions.Regex.IsMatch(p.Name, "\\A[A-Za-z](?:_[0-9]+)?\\z") || p.Name is "x" or "y" or "e" || !names.Add(p.Name) || p.Value.HasValue && !double.IsFinite(p.Value.Value) || !double.IsFinite(p.Min) || !double.IsFinite(p.Max) || !double.IsFinite(p.Max-p.Min) || p.Min >= p.Max || !double.IsFinite(p.Step) || p.Step <= 0) throw new FormatException("Invalid plot parameter.");
        }
        if (plot.Axes is {} axes && (axes.XLabel == null || axes.YLabel == null || axes.XLabel.Length > 64 || axes.YLabel.Length > 64)) throw new FormatException("Invalid axis labels.");
    }
    internal static void ValidateGeometry(GeometryDocument geometry) => GeometryEngine.Validate(geometry);
    private static bool Coordinate(double value) => double.IsFinite(value) && Math.Abs(value) <= 1_000_000;
    private static bool Identifier(string? value) => value != null && System.Text.RegularExpressions.Regex.IsMatch(value, "\\A[A-Za-z0-9_-]{1,64}\\z");
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static void RejectDuplicateKeys(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>();
            foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw new FormatException("Duplicate property."); RejectDuplicateKeys(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) RejectDuplicateKeys(child);
    }
}
