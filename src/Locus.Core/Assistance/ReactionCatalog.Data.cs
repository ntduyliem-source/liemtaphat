using System;
using System.Collections.Generic;
using System.Linq;

namespace Locus.Core.Assistance
{
    public static partial class ReactionCatalog
    {
        // Independently authored chemical facts. No textbook prose, image, code or external dataset is embedded.
        private const string Classifying="https://openstax.org/books/chemistry-2e/pages/4-2-classifying-chemical-reactions";
        private const string Carbonates="https://openstax.org/books/chemistry-2e/pages/18-6-occurrence-preparation-and-properties-of-carbonates";
        private const string Hydrogen="https://openstax.org/books/chemistry-2e/pages/18-5-occurrence-preparation-and-compounds-of-hydrogen";
        private const string Hydrocarbons="https://openstax.org/books/chemistry-2e/pages/20-1-hydrocarbons";
        private const string Limestone="https://edu.rsc.org/experiments/thermal-decomposition-of-calcium-carbonate/704.article";
        private const string Bicarbonate="https://edu.rsc.org/download?ac=12358";
        private static IReadOnlyList<ReactionCatalogRule> CreateRules()
        {
            var rules=new List<ReactionCatalogRule>();
            void Add(string id,string title,string family,string left,string? right,string[] conditions,string scope,string exclusions,string source,string basis,string negative)
                =>rules.Add(new ReactionCatalogRule(id,title,family,left,right,conditions,scope,exclusions,source,basis,negative));
            void Neutral(string id,string left,string right,bool example=false)=>Add(id,"Trung hòa acid–base","neutralization",left,right,
                new[]{"medium=water","extent=neutralize"},"Xét phản ứng trung hòa hoàn toàn trong nước, ở nhiệt độ phòng.",
                "Không chọn khi chỉ trung hòa một phần; không áp cho hỗn hợp có thêm chất phản ứng.",Classifying,example?"source-example":"derived-from-stated-principle","H3PO4+NaOH=");
            Neutral("n-hcl-naoh","HCl+NaOH","NaCl+H2O");
            Neutral("n-hcl-koh","HCl+KOH","KCl+H2O");
            Neutral("n-hcl-caoh","HCl+Ca(OH)2","CaCl2+H2O");
            Neutral("n-hcl-baoh","HCl+Ba(OH)2","BaCl2+H2O");
            Neutral("n-hno3-naoh","HNO3+NaOH","NaNO3+H2O");
            Neutral("n-hno3-koh","HNO3+KOH","KNO3+H2O");
            Neutral("n-hno3-caoh","HNO3+Ca(OH)2","Ca(NO3)2+H2O");
            Neutral("n-hno3-baoh","HNO3+Ba(OH)2","Ba(NO3)2+H2O",true);
            Neutral("n-h2so4-naoh","H2SO4+NaOH","Na2SO4+H2O");
            Neutral("n-h2so4-koh","H2SO4+KOH","K2SO4+H2O");
            Neutral("n-hcl-mgoh","HCl+Mg(OH)2","MgCl2+H2O",true);
            Neutral("n-hbr-naoh","HBr+NaOH","NaBr+H2O");
            Neutral("n-hbr-koh","HBr+KOH","KBr+H2O");
            Neutral("n-hi-naoh","HI+NaOH","NaI+H2O");
            void Precipitate(string id,string left,string right,bool example=false)=>Add(id,"Trao đổi tạo kết tủa","precipitation",left,right,
                new[]{"medium=water","precipitation=allowed"},"Hai dung dịch trong nước, ở nhiệt độ phòng; nồng độ đủ vượt độ tan của chất kết tủa.",
                "Không áp cho dung dịch quá loãng, có chất tạo phức, acid/base thêm vào hoặc có phản ứng phụ.",Classifying,example?"source-example":"derived-from-solubility-table","AgNO3+NaNO3=");
            Precipitate("p-agno3-nacl","AgNO3+NaCl","AgCl+NaNO3",true);
            Precipitate("p-agno3-kcl","AgNO3+KCl","AgCl+KNO3");
            Precipitate("p-agno3-nabr","AgNO3+NaBr","AgBr+NaNO3");
            Precipitate("p-agno3-ki","AgNO3+KI","AgI+KNO3");
            Precipitate("p-bacl2-na2so4","BaCl2+Na2SO4","BaSO4+NaCl");
            Precipitate("p-bacl2-k2so4","BaCl2+K2SO4","BaSO4+KCl");
            Precipitate("p-bano3-na2so4","Ba(NO3)2+Na2SO4","BaSO4+NaNO3");
            Precipitate("p-bano3-k2so4","Ba(NO3)2+K2SO4","BaSO4+KNO3",true);
            Precipitate("p-cacl2-na2co3","CaCl2+Na2CO3","CaCO3+NaCl");
            Precipitate("p-cacl2-k2co3","CaCl2+K2CO3","CaCO3+KCl");
            Precipitate("p-pbno3-ki","Pb(NO3)2+KI","PbI2+KNO3",true);
            Precipitate("p-pbno3-na2co3","Pb(NO3)2+Na2CO3","PbCO3+NaNO3");
            Precipitate("p-mgcl2-naoh","MgCl2+NaOH","Mg(OH)2+NaCl");
            void CarbonateAcid(string id,string left,string salt,bool example=false)=>Add(id,"Carbonate / bicarbonate với acid","carbonate-acid",left,salt+"+CO2+H2O",
                new[]{"medium=water","extent=acid-complete","gas=escapes"},"Acid đủ để chuyển hết carbonate/bicarbonate thành CO₂ trong nước; khí thoát khỏi hỗn hợp.",
                "Không áp cho thiếu acid, hệ kín giữ CO₂ hoặc phản ứng có sản phẩm muối acid trung gian.",Carbonates,example?"source-example":"derived-from-stated-principle","Na2CO3+HCl= (acid chỉ đủ một nửa)");
            CarbonateAcid("c-caco3-hcl","CaCO3+HCl","CaCl2",true);
            CarbonateAcid("c-na2co3-hcl","Na2CO3+HCl","NaCl");
            CarbonateAcid("c-k2co3-hcl","K2CO3+HCl","KCl");
            CarbonateAcid("c-nahco3-hcl","NaHCO3+HCl","NaCl");
            CarbonateAcid("c-khco3-hcl","KHCO3+HCl","KCl");
            CarbonateAcid("c-mgco3-hcl","MgCO3+HCl","MgCl2");
            CarbonateAcid("c-na2co3-hno3","Na2CO3+HNO3","NaNO3");
            Add("co2-naoh-carbonate","CO₂ với NaOH — carbonate","carbon-dioxide-base","CO2+NaOH","Na2CO3+H2O",new[]{"medium=water","ratio=two-to-one"},
                "Phương trình tổng quát ở điểm tỉ lệ mol NaOH:CO₂ = 2:1 trong nước.","Tỉ lệ khác có thể tạo bicarbonate hoặc hỗn hợp; không suy tỉ lệ từ hệ số nhập.",Carbonates,"derived-from-stated-principle","CO2+NaOH= (tỉ lệ mol 1:1)");
            Add("co2-naoh-bicarbonate","CO₂ với NaOH — bicarbonate","carbon-dioxide-base","CO2+NaOH","NaHCO3",new[]{"medium=water","ratio=one-to-one"},
                "Phương trình tổng quát ở điểm tỉ lệ mol NaOH:CO₂ = 1:1 trong nước.","Không áp cho NaOH dư, hỗn hợp carbonate/bicarbonate hoặc suy lượng chất từ hệ số nhập.",Carbonates,"derived-from-stated-principle","CO2+NaOH= (tỉ lệ mol 2:1)");
            Add("s-hydrogen-oxygen","Hydrogen với oxygen","synthesis","H2+O2","H2O",new[]{"activation=ignition"},
                "Hỗn hợp hydrogen và oxygen đã được mồi phản ứng; xét phương trình tạo nước.","Không khẳng định hỗn hợp tự phản ứng ở nhiệt độ phòng; không xét tốc độ hoặc hiệu suất.",Hydrogen,"source-example","H2+O2= (không mồi phản ứng)");
            foreach(var fuel in new[]{"CH4","C2H6","C3H8"})Add("b-"+fuel.ToLowerInvariant(),"Cháy hoàn toàn "+fuel,"combustion",fuel+"+O2","CO2+H2O",new[]{"activation=ignition","oxygen=sufficient"},
                "Cháy hoàn toàn của chất đã chỉ rõ; có mồi phản ứng và đủ oxy.","Không áp cho thiếu oxy, cháy không hoàn toàn, hỗn hợp nhiên liệu hay công thức hữu cơ ngoài danh sách.",Hydrocarbons,fuel=="CH4"?"source-example":"derived-from-stated-principle",fuel+"+O2= (thiếu oxy)");
            Add("d-calcium-carbonate","Nhiệt phân calcium carbonate","decomposition","CaCO3","CaO+CO2",new[]{"activation=strong-heat","gas=escapes"},
                "Nung mạnh calcium carbonate; CO₂ thoát khỏi hệ.","Không chọn ở nhiệt độ phòng hoặc khi điều kiện khí giữ cân bằng ngược.",Limestone,"source-example","CaCO3= (nhiệt độ phòng)");
            Add("d-sodium-bicarbonate","Nhiệt phân sodium bicarbonate","decomposition","NaHCO3","Na2CO3+CO2+H2O",new[]{"activation=heat","gas=escapes"},
                "Đun nóng sodium bicarbonate; khí thoát khỏi hệ.","Không áp ở nhiệt độ phòng hoặc thay NaHCO₃ bằng Na₂CO₃.",Bicarbonate,"source-example","Na2CO3=");
            Add("m-iron-hcl","Iron với HCl loãng","metal-acid","Fe+HCl","FeCl2+H2",new[]{"medium=water","acid=dilute-hcl"},
                "Sắt kim loại với dung dịch HCl loãng, không có chất oxy hóa khác.","Không áp cho acid oxy hóa hoặc chọn FeCl₃ thay FeCl₂.",Hydrogen,"source-example","Cu+HCl=");
            Add("o-calcium-water","Calcium oxide với nước","oxide-water","CaO+H2O","Ca(OH)2",new[]{"medium=water"},
                "Calcium oxide tiếp xúc với nước; xét phương trình tạo hydroxide.","Không áp cho môi trường khan hoặc suy sản phẩm oxide khác.",Limestone,"source-example","CuO+H2O=");
            Add("nr-nacl-kno3","NaCl và KNO₃ trong nước","no-net-reaction","NaCl+KNO3",null,new[]{"medium=water"},
                "Các ion vẫn tan trong nước ở nhiệt độ phòng; không có phương trình ion rút gọn cho tổ hợp này.","Không mô tả kết tinh khi cô cạn, thay đổi độ tan hoặc thêm chất khác.",Classifying,"derived-from-solubility-table","NaCl+AgNO3=");
            if(rules.Count!=45||rules.Select(r=>r.Id).Distinct(StringComparer.Ordinal).Count()!=rules.Count)throw new InvalidOperationException("Invalid catalog manifest.");
            return Freeze.Of(rules);
        }
    }
}
