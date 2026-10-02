using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Locus.Core.Detection;

namespace Locus.Core.Assistance
{
    public sealed class ChemistryAssistanceResult
    {
        public string Status { get; }
        public string Message { get; }
        public IReadOnlyList<AssistanceProposal> Proposals { get; }
        public ReactionDraft? Draft { get; }
        public IReadOnlyList<ReactionConditionDefinition> ConditionChoices { get; }
        public IReadOnlyList<ReactionCatalogRule> MatchedRules { get; }
        internal ChemistryAssistanceResult(string status,string message,IEnumerable<AssistanceProposal>? proposals=null,ReactionDraft? draft=null,
            IEnumerable<ReactionConditionDefinition>? conditionChoices=null,IEnumerable<ReactionCatalogRule>? matchedRules=null)
        {Status=status;Message=message;Proposals=Freeze.Of(proposals);Draft=draft;ConditionChoices=Freeze.Of(conditionChoices);MatchedRules=Freeze.Of(matchedRules);if(Proposals.Count>2)throw new ArgumentException("At most two assistance proposals may accompany one direct reading.");}
    }
    public static class ChemistryAssistance
    {
        public static ChemistryAssistanceResult Analyze(AssistanceRegion region,AssistanceContext context,DetectionDomains domains,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            if((domains&DetectionDomains.Chemistry)==0)return new ChemistryAssistanceResult("disabled","Bật nhận diện Hóa hoặc dùng cặp riêng Hóa cho vùng này.");
            var draft=ReactionDraftParser.Parse(region,token);
            if(draft.Draft!=null)
            {
                var routed=DomainAnalysisEngine.Route(region.Source,draft.Draft.Reactants.ContentSpan,draft.Draft.Reactants.ReplacementSpan,domains,token,true);
                if(routed.Candidates.Count!=1||routed.Candidates[0].Document.Domain!="chemistry"||routed.Candidates[0].Diagnostics.Any(d=>d.Severity=="error"||d.Severity=="warning"))
                    return new ChemistryAssistanceResult("needs-interpretation","Viết rõ ký hiệu và chỉ định Hóa trước khi bổ sung sản phẩm.");
                return ReactionCatalog.Complete(draft.Draft,context,token);
            }
            if(!region.IsClosed)return new ChemistryAssistanceResult("incomplete","Hoàn thành nội dung và đóng cặp bọc để xét phương trình đủ hai vế.");
            var parsed=DomainAnalysisEngine.Route(region.Source,region.ContentSpan,region.ReplacementSpan,domains,token,true);
            if(parsed.Candidates.Count!=1||parsed.Candidates[0].Kind!="direct"||parsed.Candidates[0].Document.Domain!="chemistry"||parsed.Candidates[0].Diagnostics.Concat(parsed.Diagnostics).Any(d=>d.Severity=="warning"||d.Severity=="error"))
                return new ChemistryAssistanceResult("needs-interpretation","Cần phương trình Hóa rõ nghĩa trước khi cân bằng; kiểm tra chữ hoa/thường và số 0.");
            var balance=ReactionBalancer.Balance(parsed.Candidates[0].Document,token);
            if(balance.Status!=BalanceStatus.Balanced)return new ChemistryAssistanceResult(Status(balance.Status),balance.Message);
            var reading=new CandidateSet(region.Source,region.ContentSpan,region.ReplacementSpan,parsed.Candidates,parsed.Diagnostics,markers:region.Markers,intent:region.Intent);
            var proposal=new AssistanceProposal("balance",region,context,balance.Result!,new AssistanceProvenance(AssistanceVersions.Solver),reading);
            return new ChemistryAssistanceResult("available",balance.Message,new[]{proposal});
        }
        private static string Status(BalanceStatus status)=>status==BalanceStatus.AlreadyBalanced?"already-balanced":status==BalanceStatus.NoSolution?"no-solution":status==BalanceStatus.NonUnique?"non-unique":status==BalanceStatus.Limit?"limit":"invalid";
    }
}
