using System.Text.Json;
using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Serialization;

namespace Locus.Application;

public sealed record ContentBalanceRequest(string Snapshot,string CandidateId);
public sealed record ContentBalanceResponse(string CandidateId,string Status,string Message,string? Result);
public interface IContentBalanceScheduler
{
    Task<ContentBalanceResponse> BalanceAsync(ContentBalanceRequest request,CancellationToken token);
}
public static class ContentBalanceWire
{
    public const string Operation="content-balance/1";
    private sealed record Envelope(string Operation,ContentBalanceRequest Request);
    public static string Request(ContentBalanceRequest request)=>JsonSerializer.Serialize(new Envelope(Operation,request));
    public static string Run(string json)
    {
        if(json.Length>2_000_000)throw new FormatException("Balance request too large.");
        var envelope=JsonSerializer.Deserialize<Envelope>(json)??throw new FormatException("Missing request.");
        if(envelope.Operation!=Operation)throw new FormatException("Unknown operation.");
        return JsonSerializer.Serialize(Analyze(envelope.Request));
    }
    public static ContentBalanceResponse Analyze(ContentBalanceRequest request,CancellationToken token=default)
    {
        if(request?.Snapshot==null||request.Snapshot.Length>2_000_000)throw new FormatException("Invalid balance request.");
        var set=CandidateSetSerializer.Deserialize(request.Snapshot);var candidate=set.Candidates.Single(c=>c.Id==request.CandidateId);
        if(candidate.Document.Domain=="chemistry"&&candidate.Document.Root.Type=="ChemReaction"&&ReactionBalancer.VerifyConservation(candidate.Document.Root,token))
            return new(candidate.Id,"AlreadyBalanced","Phương trình đã cân bằng; Locus chưa đổi hệ số.",null);
        var result=ReactionBalancer.Balance(candidate.Document,token);
        return new(candidate.Id,result.Status.ToString(),result.Message,result.Result==null?null:CandidateSetSerializer.Serialize(result.Result));
    }
    public static ContentBalanceResponse Deserialize(string json)
    {
        if(json.Length>2_000_000)throw new FormatException("Balance response too large.");
        var result=JsonSerializer.Deserialize<ContentBalanceResponse>(json)??throw new FormatException("Missing response.");
        if(!Enum.TryParse<BalanceStatus>(result.Status,out var status)||!Enum.IsDefined(status)||status.ToString()!=result.Status||string.IsNullOrEmpty(result.CandidateId)||result.Message==null||result.Message.Length>512||
            (result.Status=="Balanced")!=(result.Result!=null))throw new FormatException("Invalid balance response.");
        return result;
    }
    public static MathNode WithoutCoefficients(MathNode node)=>ChemistryProjection.WithoutCoefficients(node);
    public static bool SameSpecies(Candidate first,Candidate second)=>ChemistryProjection.SameSpecies(first,second);
}
