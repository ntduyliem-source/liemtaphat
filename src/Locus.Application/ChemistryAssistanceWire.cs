using System.Text.Json;
using System.Text.Json.Serialization;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;

namespace Locus.Application;

public sealed record AssistanceCondition(string Key,string Value);
public sealed record ChemistryAssistanceRequest(AnalysisRequest Input,int RegionStart,AssistanceCondition[] Conditions,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)] bool UseUniqueCatalogConditions=false);
public sealed record ChemistryConditionOption(string Value,string Label);
public sealed record ChemistryConditionChoice(string Key,string Label,ChemistryConditionOption[] Options);
public sealed record ChemistryRuleInfo(string Id,string Title,string Scope,string Exclusions,string[] References,AssistanceCondition[] Conditions);
public sealed record ChemistryAssistanceResponse(string SourceId,long SourceRevision,string ContextId,string Status,string Message,string[] Proposals,string? Draft,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] ChemistryConditionChoice[]? ConditionChoices=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] ChemistryRuleInfo[]? MatchedRules=null);

/// <summary>The same bounded request runs off the UI thread on native and inside the browser worker.</summary>
public static class ChemistryAssistanceWire
{
    public const string Operation="chemistry-assistance/0.1";
    private sealed record Envelope(string Operation,ChemistryAssistanceRequest Request);
    public static string Request(ChemistryAssistanceRequest request)=>JsonSerializer.Serialize(new Envelope(Operation,request));
    public static string Run(string json)
    {
        if(json.Length>65536)throw new FormatException("Assistance request too large.");
        var request=JsonSerializer.Deserialize<Envelope>(json)??throw new FormatException("Missing request.");
        if(request.Operation!=Operation)throw new FormatException("Unsupported assistance operation.");
        return JsonSerializer.Serialize(Analyze(request.Request));
    }
    public static ChemistryAssistanceResponse Analyze(ChemistryAssistanceRequest request,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(request.Input);
        var input=request.Input;
        if(input.Raw==null||input.Raw.Length>FormulaSession.MaxSourceLength||input.Settings==null||!input.Settings.HasValidDomains||!input.Settings.HasValidMarkerProfiles||!Enum.IsDefined(input.Settings.Mode)||
            request.Conditions==null||request.Conditions.Length>16||request.Conditions.Any(c=>c==null))throw new ArgumentException("Invalid assistance input.");
        var source=new SourceSnapshot(input.Raw,input.SourceRevision);
        var context=AssistanceHistory.Context(input.Settings,request.Conditions.Select(c=>new ConditionFact(c.Key,c.Value)));
        AssistanceRegion? region=null;var domains=input.Settings.EnabledDomains;
        if(input.Settings.MarkerProfiles!=null)
        {
            var profiles=input.Settings.MarkerProfiles.ToProfiles(input.Settings.Open,input.Settings.Close);
            var scan=ProfileMarkerScanner.Scan(source,profiles,maxRegions:32,cancellationToken:token);
            var match=scan.Regions.Concat(scan.Drafts).FirstOrDefault(r=>r.ReplacementSpan.Start==request.RegionStart);
            if(match!=null&&(input.Settings.Mode!=InputMode.Explicit||match.ReplacementSpan.Start==0&&match.ReplacementSpan.End==source.Raw.Length))
            {
                if(match.Profile.Domain is not ("chemistry" or "auto"))return Reply("disabled","Vùng này đã được chỉ định Toán hoặc Lý.");
                region=new(source,match.ContentSpan,match.ReplacementSpan,match.Profile.Markers,new(match.Profile.Id,match.Profile.Domain),match.IsClosed);
                domains=match.Profile.Resolve(domains);
            }
            else if(scan.ReservedSpans.Count>0||input.Settings.Mode!=InputMode.Explicit)return Reply("invalid","Vùng cặp bọc chưa hợp lệ hoặc đã thay đổi.");
        }
        if(region==null)
        {
            if(input.Settings.Mode!=InputMode.Explicit||request.RegionStart!=0||source.Raw.Length==0)return Reply("invalid","Chọn một vùng công thức để xem hỗ trợ Hóa.");
            region=new(source,new(0,source.Raw.Length),new(0,source.Raw.Length));
        }
        var result=ChemistryAssistance.Analyze(region,context,domains,token);
        string? assumedScope=null;
        // Only the explicit fill-and-balance command opts in. Ghost suggestions still require user conditions.
        if(request.UseUniqueCatalogConditions&&result.Status=="needs-conditions")
        {
            var compatible=result.MatchedRules.Where(r=>context.Conditions.All(c=>r.Conditions.Any(f=>f.Key==c.Key&&f.Value==c.Value))).ToArray();
            if(compatible.Length==1&&!compatible[0].IsNoReaction)
            {
                context=AssistanceHistory.Context(input.Settings,compatible[0].Conditions);
                assumedScope=compatible[0].Scope;
                result=ChemistryAssistance.Analyze(region,context,domains,token);
            }
        }
        return new(source.Id,source.Revision,context.Id,result.Status,assumedScope==null?result.Message:"Điền theo trường hợp: "+assumedScope,result.Proposals.Select(AssistanceSerializer.Serialize).ToArray(),result.Draft==null?null:AssistanceSerializer.SerializeDraft(result.Draft),
            result.ConditionChoices.Count==0?null:result.ConditionChoices.Select(d=>new ChemistryConditionChoice(d.Key,d.Label,d.Options.Select(o=>new ChemistryConditionOption(o.Value,o.Label)).ToArray())).ToArray(),
            result.MatchedRules.Count==0?null:result.MatchedRules.Select(r=>new ChemistryRuleInfo(r.Id,r.Title,r.Scope,r.Exclusions,r.References.ToArray(),r.Conditions.Select(c=>new AssistanceCondition(c.Key,c.Value)).ToArray())).ToArray());
        ChemistryAssistanceResponse Reply(string status,string message)=>new(source.Id,source.Revision,context.Id,status,message,[],null);
    }
    public static ChemistryAssistanceResponse Deserialize(string json)
    {
        if(json.Length>8_000_000)throw new FormatException("Assistance response too large.");
        var result=JsonSerializer.Deserialize<ChemistryAssistanceResponse>(json)??throw new FormatException("Missing response.");
        if(result.Proposals==null||result.Proposals.Length>2||result.Proposals.Any(p=>p==null)||result.Status is not ("available" or "disabled" or "invalid" or "incomplete" or "needs-interpretation" or "needs-conditions" or "multiple" or "no-reaction" or "unsupported" or "already-balanced" or "no-solution" or "non-unique" or "limit"))throw new FormatException("Invalid response.");
        if((result.Status=="available")!=(result.Proposals.Length>0))throw new FormatException("Proposal status mismatch.");
        foreach(var snapshot in result.Proposals){var proposal=AssistanceSerializer.Deserialize(snapshot);if(proposal.Region.Source.Id!=result.SourceId||proposal.Region.Source.Revision!=result.SourceRevision||proposal.Context.Id!=result.ContextId)throw new FormatException("Proposal response identity mismatch.");}
        if(result.Draft!=null){var draft=AssistanceSerializer.DeserializeDraft(result.Draft);if(draft.Region.Source.Id!=result.SourceId||draft.Region.Source.Revision!=result.SourceRevision)throw new FormatException("Draft response identity mismatch.");}
        if(result.ConditionChoices is {} choices)
        {
            if(choices.Length>16||choices.Any(d=>d==null)||choices.Select(d=>d.Key).Distinct(StringComparer.Ordinal).Count()!=choices.Length)throw new FormatException("Invalid condition choices.");
            foreach(var choice in choices)
            {
                var known=ReactionCatalog.ConditionDefinitions.SingleOrDefault(d=>d.Key==choice.Key);
                if(known==null||choice.Label!=known.Label||choice.Options==null||choice.Options.Length!=known.Options.Count||choice.Options.Where((o,i)=>o==null||o.Value!=known.Options[i].Value||o.Label!=known.Options[i].Label).Any())throw new FormatException("Unknown condition choices.");
            }
        }
        if(result.MatchedRules is {} rules)
        {
            if(rules.Length>64||rules.Any(r=>r==null)||rules.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=rules.Length)throw new FormatException("Invalid rule metadata.");
            foreach(var rule in rules)
            {
                var known=ReactionCatalog.Rules.SingleOrDefault(r=>r.Id==rule.Id);
                if(known==null||rule.Title!=known.Title||rule.Scope!=known.Scope||rule.Exclusions!=known.Exclusions||rule.References==null||!rule.References.SequenceEqual(known.References,StringComparer.Ordinal)||rule.Conditions==null||
                    !rule.Conditions.SequenceEqual(known.Conditions.Select(c=>new AssistanceCondition(c.Key,c.Value))))throw new FormatException("Rule metadata mismatch.");
            }
        }
        if(result.Status is "needs-conditions" or "no-reaction" or "multiple"&&(result.Draft==null||result.MatchedRules is not {Length:>0}||result.ConditionChoices is not {Length:>0}))throw new FormatException("Missing catalog context.");
        return result;
    }
}
