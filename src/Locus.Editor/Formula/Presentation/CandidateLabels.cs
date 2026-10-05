using Locus.Core;

namespace Locus.Editor.Formula.Presentation;

public static class CandidateLabels
{
    public static string Kind(Candidate c) => c.Kind == "direct" ? "Theo cú pháp đã gõ" : c.Kind == "repair" ? "Đề nghị sửa" : "Cách hiểu khác";
    public static string Explanation(Candidate c) => c.Provenance.Contains("fraction-numerator-scope") ? "Đưa cả biểu thức vào tử số" :
        c.Provenance.Contains("bare-root-scope") ? "Mở rộng phạm vi căn" :
        c.Provenance.Contains("missing-") ? "Bổ sung ngoặc đóng" : Kind(c);
}
