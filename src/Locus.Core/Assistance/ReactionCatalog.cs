using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Locus.Core.Domains;
using Locus.Core.Parsing;

namespace Locus.Core.Assistance
{
    public sealed class ReactionConditionOption
    {
        public string Value { get; }
        public string Label { get; }
        internal ReactionConditionOption(string value,string label){Value=value;Label=label;}
    }
    public sealed class ReactionConditionDefinition
    {
        public string Key { get; }
        public string Label { get; }
        public IReadOnlyList<ReactionConditionOption> Options { get; }
        internal ReactionConditionDefinition(string key,string label,params string[] choices)
        {Key=key;Label=label;Options=Freeze.Of(choices.Select(c=>{var split=c.Split('|');return new ReactionConditionOption(split[0],split[1]);}));}
    }
    /// <summary>An authored whitelist entry, never a general formula-to-structure inference rule.</summary>
    public sealed class ReactionCatalogRule
    {
        public string Id { get; }
        public string Version => AssistanceVersions.Catalog;
        public string Title { get; }
        public string Family { get; }
        public string Reactants { get; }
        public string? Products { get; }
        public bool IsNoReaction => Products==null;
        public string Scope { get; }
        public string Exclusions { get; }
        public string EvidenceKind { get; }
        public string PositiveExample => Reactants+"=";
        public string NegativeExample { get; }
        public IReadOnlyList<ConditionFact> Conditions { get; }
        public IReadOnlyList<string> References { get; }
        internal IReadOnlyList<string> Identities { get; }
        internal MathNode? ProductTree { get; }
        internal ReactionCatalogRule(string id,string title,string family,string reactants,string? products,
            string[] conditions,string scope,string exclusions,string reference,string evidenceKind,string negative)
        {
            Id=id;Title=title;Family=family;Reactants=reactants;Products=products;Scope=scope;Exclusions=exclusions;
            EvidenceKind=evidenceKind;NegativeExample=negative;
            Conditions=Freeze.Of(conditions.Select(c=>{var split=c.Split('=');return new ConditionFact(split[0],split[1]);}));
            References=Freeze.Of(new[]{reference});
            Identities=Freeze.Of(ReactionCatalog.Species(Parse(reactants)).Select(ReactionCatalog.Identity).OrderBy(s=>s,StringComparer.Ordinal));
            if(Identities.Distinct(StringComparer.Ordinal).Count()!=Identities.Count)throw new ArgumentException("Duplicate catalog reactants: "+id);
            if(products!=null)ProductTree=Parse(products);
            if(Conditions.Count==0||Conditions.Any(c=>!ReactionCatalog.ConditionDefinitions.Any(d=>d.Key==c.Key&&d.Options.Any(o=>o.Value==c.Value))))throw new ArgumentException("Unknown catalog condition: "+id);
        }
        private static MathNode Parse(string raw)
        {
            var source=new SourceSnapshot(raw);var result=new ChemistryParser().Parse(source,new TextSpan(0,raw.Length));
            if(result.Candidates.Count!=1||result.Candidates[0].Document.Root.Type=="ChemReaction")throw new ArgumentException("Invalid authored species: "+raw);
            return result.Candidates[0].Document.Root;
        }
    }
    public static partial class ReactionCatalog
    {
        public static IReadOnlyList<ReactionConditionDefinition> ConditionDefinitions { get; }=Freeze.Of(new[]{
            new ReactionConditionDefinition("medium","Môi trường","water|Trong nước, nhiệt độ phòng","dry|Khan / không có nước"),
            new ReactionConditionDefinition("extent","Mức phản ứng","neutralize|Trung hòa hết các H có tính acid","acid-complete|Acid đủ để carbonate chuyển hết thành CO₂","partial|Chỉ phản ứng một phần / chưa rõ tỉ lệ"),
            new ReactionConditionDefinition("precipitation","Điều kiện kết tủa","allowed|Nồng độ đủ tạo kết tủa; không có chất tạo phức","too-dilute|Dung dịch rất loãng / chưa biết có kết tủa"),
            new ReactionConditionDefinition("gas","Khí tạo thành","escapes|CO₂ thoát khỏi hỗn hợp","closed|CO₂ bị giữ trong hệ kín"),
            new ReactionConditionDefinition("ratio","Tỉ lệ mol NaOH : CO₂","two-to-one|2 : 1 — tạo carbonate","one-to-one|1 : 1 — tạo bicarbonate","mixed|Giữa 1 : 1 và 2 : 1 / chưa xác định"),
            new ReactionConditionDefinition("activation","Tác động","ignition|Đã mồi phản ứng cháy","strong-heat|Nung mạnh để phân hủy carbonate","heat|Đun nóng để phân hủy bicarbonate","none|Không gia nhiệt / không mồi phản ứng"),
            new ReactionConditionDefinition("oxygen","Oxy","sufficient|Đủ oxy, xét cháy hoàn toàn","limited|Thiếu oxy / cháy không hoàn toàn"),
            new ReactionConditionDefinition("acid","Acid","dilute-hcl|HCl loãng; không có chất oxy hóa khác","other|Acid đặc hoặc có chất oxy hóa khác")
        });
        private static readonly Lazy<IReadOnlyList<ReactionCatalogRule>> records=new Lazy<IReadOnlyList<ReactionCatalogRule>>(CreateRules);
        public static IReadOnlyList<ReactionCatalogRule> Rules=>records.Value;
        internal static IEnumerable<MathNode> Species(MathNode node)
        {
            if(node.Type=="ChemSum"){foreach(var child in node.Children)foreach(var species in Species(child))yield return species;}
            else yield return node.Type=="ChemCoefficient"?node.Children[1]:node;
        }
        // Structural formula spelling, not atom counts: two isomers must never become the same identity.
        internal static string Identity(MathNode species)=>ChemistryProjection.Create(species).Source.Raw;
        internal static ChemistryAssistanceResult Complete(ReactionDraft draft,AssistanceContext context,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(draft.Arrow!="arrow")return new ChemistryAssistanceResult("unsupported","Kho hiện tại chưa suy sản phẩm cho mũi tên thuận nghịch; loại mũi tên đã gõ được giữ nguyên.",draft:draft);
            var left=Species(draft.Reactants.Candidates[0].Document.Root).ToArray();
            if(left.Length>ReactionBalancer.MaxSpecies)return new ChemistryAssistanceResult("limit","Vế trái vượt giới hạn số chất.",draft:draft);
            var identities=left.Select(Identity).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
            if(identities.Distinct(StringComparer.Ordinal).Count()!=identities.Length)return new ChemistryAssistanceResult("needs-interpretation","Vế trái có chất lặp. Hãy viết rõ các chất trước khi bổ sung sản phẩm.",draft:draft);
            var matched=Rules.Where(r=>r.Identities.SequenceEqual(identities,StringComparer.Ordinal)).ToArray();
            if(matched.Length==0)return new ChemistryAssistanceResult("unsupported","Chưa có dữ liệu cho tổ hợp chất này. Điều đó không có nghĩa là không phản ứng.",draft:draft);
            var definitions=ConditionDefinitions.Where(d=>matched.Any(r=>r.Conditions.Any(c=>c.Key==d.Key))).ToArray();
            // An additional or conflicting fact cannot silently disappear from the reasoning.
            var compatible=matched.Where(r=>context.Conditions.All(c=>r.Conditions.Any(required=>required.Key==c.Key&&required.Value==c.Value))).ToArray();
            if(compatible.Length==0)return Reply("unsupported","Chưa có dữ liệu cho những điều kiện đã chọn. Hãy đối chiếu phạm vi của phản ứng.");
            if(compatible.Any(r=>r.Conditions.Any(c=>!context.Conditions.Any(f=>f.Key==c.Key))))
                return Reply("needs-conditions","Chọn điều kiện của bài toán để xét sản phẩm. Hệ số trong ô gõ không được dùng làm tỉ lệ mol.");
            if(compatible.Length!=1)return Reply("multiple","Còn nhiều khả năng theo dữ liệu hiện có; cần làm rõ điều kiện trước khi nhận kết quả.");
            var rule=compatible[0];
            if(rule.IsNoReaction)return Reply("no-reaction","Không có phản ứng ion rút gọn trong điều kiện và tổ hợp đã chọn; xem phạm vi bên dưới.");
            token.ThrowIfCancellationRequested();
            // Retain the user's reactant order and structures. Only products come from the matched record.
            MathNode sum=left[0];for(int i=1;i<left.Length;i++)sum=new MathNode("ChemSum",new[]{sum,left[i]});
            var equation=ChemistryProjection.Create(new MathNode("ChemReaction",new[]{sum,rule.ProductTree!},@operator:"arrow"));
            var balance=ReactionBalancer.Balance(equation.Candidates[0].Document,token);
            if(balance.Status!=BalanceStatus.Balanced&&balance.Status!=BalanceStatus.AlreadyBalanced)
                return Reply(balance.Status==BalanceStatus.Limit?"limit":"invalid","Dữ liệu sản phẩm chưa tạo được một phương trình bảo toàn duy nhất; nguồn vẫn được giữ.");
            var result=balance.Result??equation;
            if(!ReactionBalancer.VerifyConservation(result.Candidates[0].Document.Root,token))return Reply("invalid","Kết quả chưa vượt qua kiểm tra bảo toàn.");
            var proposal=new AssistanceProposal("complete-reaction",draft.Region,context,result,new AssistanceProvenance(AssistanceVersions.Solver,AssistanceVersions.Catalog,rule.Id,rule.References));
            return Reply("available","Có sản phẩm theo điều kiện đã chọn. Xem toàn phương trình và hệ số vế trái trước khi dùng.",new[]{proposal});
            ChemistryAssistanceResult Reply(string status,string message,IEnumerable<AssistanceProposal>? proposals=null)
                =>new ChemistryAssistanceResult(status,message,proposals,draft,definitions,matched);
        }
    }
}
