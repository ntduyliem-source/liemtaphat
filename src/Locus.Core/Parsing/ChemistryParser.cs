using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Locus.Core.Domains;

namespace Locus.Core.Parsing
{
    public sealed class ChemistryParser
    {
        private readonly bool allowAliases;
        public ChemistryParser(bool allowAliases = false) { this.allowAliases = allowAliases; }
        public CandidateSet Parse(SourceSnapshot source, TextSpan content, TextSpan? replacement = null, CancellationToken cancellationToken = default)
        {
            source.Validate(content); source.Validate(replacement ?? content);
            var replace = replacement ?? content;
            if (!replace.Contains(content)) throw new ArgumentException("Replacement must contain content.");
            cancellationToken.ThrowIfCancellationRequested();
            if (content.Length > 4096) return Reject(source, content, replace, "REGION_LENGTH_LIMIT", content);
            try
            {
                var state = new State(source, content, cancellationToken, allowAliases);
                var root = state.Parse();
                var diagnostic = new List<Diagnostic>();
                if (!state.StrongEvidence) diagnostic.Add(new Diagnostic("CHEMISTRY_WEAK_CONTEXT", "warning", content, "Chuỗi chữ có thể là ký hiệu hóa học; hãy kiểm tra cách hiểu."));
                if (state.ZeroInAlias) diagnostic.Add(new Diagnostic("CHEMISTRY_ZERO_IS_DIGIT", "warning", content, "Số 0 vẫn là chữ số, không phải chữ O. Viết rõ H2O nếu bạn muốn nhập nước."));
                ScientificDocument document = state.UsedAliases ? (ScientificDocument)new SmartChemistryDocument(root) : new ChemistryDocument(root);
                var candidate = new Candidate("direct", document, source, content, replace, diagnostic, provenance: state.UsedAliases ? "chemistry/sc1-alias" : "chemistry/direct");
                return new CandidateSet(source, content, replace, new[] { candidate });
            }
            catch (Failure f) { return Reject(source, content, replace, f.Code, f.Span); }
            catch (ArgumentException) { return Reject(source, content, replace, "CHEMISTRY_STRUCTURE_LIMIT", content); }
        }
        private static CandidateSet Reject(SourceSnapshot s, TextSpan c, TextSpan r, string code, TextSpan span) => new CandidateSet(s,c,r,Array.Empty<Candidate>(),new[] {new Diagnostic(code,"error",span,
            code=="AMBIGUOUS_CHEMISTRY_CASE"?"Có nhiều cách tách ký hiệu nguyên tố. Hãy viết rõ chữ hoa/thường, ví dụ Co hoặc CO.":code=="CHEMISTRY_CASE_BUDGET"?"Chuỗi ký hiệu quá dài để xác định cách viết hoa. Hãy viết rõ ký hiệu nguyên tố.":null)});
        private sealed class Failure : Exception
        {
            internal readonly string Code; internal readonly TextSpan Span;
            internal Failure(string code, TextSpan span) { Code=code; Span=span; }
        }
        private sealed class State
        {
            private readonly string raw; private readonly TextSpan span; private readonly CancellationToken token; private readonly bool aliases; private int p;
            internal bool StrongEvidence { get; private set; }
            internal bool UsedAliases { get; private set; }
            internal bool ZeroInAlias { get; private set; }
            internal State(SourceSnapshot source, TextSpan content, CancellationToken cancellation, bool aliases) {raw=source.Raw;span=content;p=span.Start;token=cancellation;this.aliases=aliases;}
            private char Current => p < span.End ? raw[p] : '\0';
            private void Skip() {while (p<span.End && char.IsWhiteSpace(raw[p]) && raw[p]!='\r' && raw[p]!='\n') { token.ThrowIfCancellationRequested();p++;}}
            private bool Match(string text) => p+text.Length<=span.End && string.CompareOrdinal(raw,p,text,0,text.Length)==0;
            private void Fail(string code)
            {
                int end = Math.Min(span.End, p + 1);
                if (end < span.End && end > p && char.IsHighSurrogate(raw[end - 1]) && char.IsLowSurrogate(raw[end])) end++;
                throw new Failure(code, new TextSpan(p, end));
            }
            private MathNode Node(string type, int start, params MathNode[] children) => new MathNode(type,children,sourceSpans:new[]{new TextSpan(start,p)});
            private MathNode Join(string type, MathNode a, MathNode b) => new MathNode(type,new[]{a,b},sourceSpans:new[]{new TextSpan(a.SourceSpans[0].Start,b.SourceSpans[0].End)});
            internal MathNode Parse()
            {
                Skip();var start=p; var left=Side();Skip();
                string? op=null;
                if(Match("<->")){op="reversible";p+=3;} else if(Match("->")){op="arrow";p+=2;} else if(Current=='→'){op="arrow";p++;} else if(Current=='⇌'){op="reversible";p++;}
                else if(aliases&&Current=='='){op="arrow";p++;UsedAliases=true;}
                if(op!=null){StrongEvidence=true;Skip();var right=Side();left=new MathNode("ChemReaction",new[]{left,right},@operator:op,sourceSpans:new[]{new TextSpan(start,p)});}
                Skip();if(p!=span.End) Fail("INVALID_CHEMISTRY_SUFFIX");
                return left;
            }
            private MathNode Side()
            {
                var left=Species();Skip();int count=0;
                while(Current=='+'){if(++count>64)Fail("CHEMISTRY_TERM_LIMIT");StrongEvidence=true;p++;Skip();left=Join("ChemSum",left,Species());Skip();}
                return left;
            }
            private MathNode Species()
            {
                token.ThrowIfCancellationRequested();Skip();var start=p; MathNode? coefficient=null;
                if(Current>='0'&&Current<='9'){coefficient=Integer(false);StrongEvidence=true;Skip();}
                var formula=Formula(0);Skip();
                if(Current=='^' || IsSup(Current))
                {
                    StrongEvidence=true;bool unicode=Current!='^';if(!unicode)p++;
                    bool brace=Current=='{';if(brace)p++;
                    var charge=new StringBuilder();
                    while(p<span.End && (unicode ? SupDigit(Current)>=0 : Current>='0'&&Current<='9')) {charge.Append(unicode?(char)('0'+SupDigit(Current)):Current);p++;}
                    if(unicode ? Current!='⁺'&&Current!='⁻' : Current!='+'&&Current!='-'&&Current!='−')Fail("EXPLICIT_CHARGE_REQUIRED");
                    charge.Append(Current=='+'||Current=='⁺'?'+':'-');p++;
                    if(brace){if(Current!='}')Fail("UNCLOSED_CHARGE");p++;}
                    var text=charge.ToString();if(text[0]=='0')Fail("INVALID_CHARGE");
                    if(text.StartsWith("1",StringComparison.Ordinal)&&text.Length==2)text=text.Substring(1);
                    formula=new MathNode("ChemCharge",new[]{formula},value:text,sourceSpans:new[]{new TextSpan(start,p)});
                }
                if(coefficient!=null) formula=Node("ChemCoefficient",start,coefficient,formula);
                return formula;
            }
            private MathNode Formula(int depth)
            {
                if(depth>=48)Fail("CHEMISTRY_DEPTH_LIMIT");
                MathNode? result=null;int count=0;
                while(p<span.End)
                {
                    token.ThrowIfCancellationRequested();var start=p;MathNode part;
                    if(Current>='A'&&Current<='Z')
                    {
                        var element=raw[p++].ToString();if(Current>='a'&&Current<='z')element+=raw[p++];
                        if(!ScientificSymbols.IsElement(element))Fail("UNKNOWN_ELEMENT");
                        part=new MathNode("ChemElement",name:element,sourceSpans:new[]{new TextSpan(start,p)});
                    }
                    else if(aliases&&Current>='a'&&Current<='z')
                    {
                        var element=LowerElement();UsedAliases=true;
                        part=new MathNode("ChemElement",name:element,sourceSpans:new[]{new TextSpan(start,p)});
                    }
                    else if(Current=='('||Current=='[')
                    {
                        StrongEvidence=true;var open=Current;p++;var child=Formula(depth+1);
                        if(Current!=(open=='('?')':']'))Fail("UNCLOSED_CHEMISTRY_GROUP");p++;
                        part=new MathNode("ChemGroup",new[]{child},name:open=='('?"()":"[]",sourceSpans:new[]{new TextSpan(start,p)});
                    }
                    else break;
                    if(Current>='0'&&Current<='9'||SubDigit(Current)>=0){var countNode=Integer(SubDigit(Current)>=0);StrongEvidence=true;if(UsedAliases&&countNode.Value!.Contains("0"))ZeroInAlias=true;part=Node("ChemSubscript",start,part,countNode);}
                    result=result==null?part:Join("ChemConcat",result,part);
                    if(++count>64)Fail("CHEMISTRY_TERM_LIMIT");
                }
                if(result==null)Fail("MISSING_CHEMISTRY_SPECIES");
                return result!;
            }
            private string LowerElement()
            {
                int end=p;while(end<span.End&&raw[end]>='a'&&raw[end]<='z')end++;
                int length=end-p;if(length>64)Fail("CHEMISTRY_CASE_BUDGET");
                var ways=new int[length+1];var first=new string?[length];ways[length]=1;
                for(int i=length-1;i>=0;i--)
                {
                    token.ThrowIfCancellationRequested();
                    for(int size=1;size<=2&&i+size<=length;size++)
                    {
                        var symbol=char.ToUpperInvariant(raw[p+i]).ToString()+(size==2?raw[p+i+1].ToString():"");
                        if(!ScientificSymbols.IsElement(symbol)||ways[i+size]==0)continue;
                        first[i]=symbol;ways[i]=Math.Min(2,ways[i]+ways[i+size]);
                    }
                }
                if(ways[0]==0)throw new Failure("UNKNOWN_ELEMENT",new TextSpan(p,end));
                if(ways[0]>1)throw new Failure("AMBIGUOUS_CHEMISTRY_CASE",new TextSpan(p,end));
                var result=first[0]!;p+=result.Length;return result;
            }
            private MathNode Integer(bool sub)
            {
                var start=p;var value=new StringBuilder();
                while(p<span.End && (sub?SubDigit(Current)>=0:Current>='0'&&Current<='9')){value.Append(sub?(char)('0'+SubDigit(Current)):Current);p++;}
                if(value.Length==0||value.Length>9||value[0]=='0')Fail("INVALID_CHEMISTRY_INTEGER");
                return new MathNode("Number",value:value.ToString(),sourceSpans:new[]{new TextSpan(start,p)});
            }
            private static int SubDigit(char c)=>"₀₁₂₃₄₅₆₇₈₉".IndexOf(c);
            private static int SupDigit(char c)=>"⁰¹²³⁴⁵⁶⁷⁸⁹".IndexOf(c);
            private static bool IsSup(char c)=>SupDigit(c)>=0||c=='⁺'||c=='⁻';
        }
    }
}
