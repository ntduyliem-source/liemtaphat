using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Locus.Core.Domains;

namespace Locus.Core.Parsing
{
    /// <summary>Science atoms feed the same arithmetic grammar; every atom refers to original UTF-16.</summary>
    internal sealed class PhysicsTokenizer
    {
        private SourceSnapshot source = null!;
        private string raw = "";
        private int p, end;
        private CancellationToken token;
        private readonly List<FormulaToken> tokens = new List<FormulaToken>();
        internal bool IsUnitFragment(SourceSnapshot s, TextSpan span, CancellationToken cancellation)
        {
            source=s;raw=s.Raw;p=span.Start;end=span.End;token=cancellation;
            try { _=UnitExpression(0);Skip();return p==end; }
            catch(Failure){return false;}
            catch(ArgumentException){return false;}
        }
        internal TokenizationResult Tokenize(SourceSnapshot s, TextSpan span, CancellationToken cancellation)
        {
            source=s;raw=s.Raw;p=span.Start;end=span.End;token=cancellation;
            var errors=new List<Diagnostic>();
            try
            {
                while(p<end)
                {
                    token.ThrowIfCancellationRequested();int before=p;Skip();bool space=p>before;if(p==end)break;
                    int start=p;
                    if(raw[p]=='\r'||raw[p]=='\n')Fail("UNSUPPORTED_MULTILINE_EXPRESSION",start);
                    if(Digit(raw[p]))
                    {
                        while(p<end&&(Digit(raw[p])||(raw[p]=='.'||raw[p]==',')&&p+1<end&&Digit(raw[p+1])))p++;
                        if(p<end&&(raw[p]=='e'||raw[p]=='E') && p+1<end && (Digit(raw[p+1])||raw[p+1]=='+'||raw[p+1]=='-'))Fail("UNSUPPORTED_SCIENTIFIC_NOTATION",start);
                        var number=Basic(start,p).Single();int afterNumber=p;Skip();
                        if(p>afterNumber && StartsUnit(p))
                        {
                            var unit=UnitExpression(0);
                            var n=new MathNode("Number",value:number.NormalizedText,sourceSpans:new[]{number.Span});
                            Add(new MathNode("Quantity",new[]{n,unit},sourceSpans:new[]{new TextSpan(start,p)}),start,space);
                        }
                        else {p=afterNumber;tokens.Add(Copy(number,space));}
                    }
                    else if(FormulaLexicon.IsWordCharacter(raw[p])||raw[p]=='°')
                    {
                        while(p<end&&FormulaLexicon.IsWordCharacter(raw[p]))p++;
                        if(p==start)p++;
                        var word=raw.Substring(start,p-start).Normalize(NormalizationForm.FormC);
                        if(word=="vec"||word=="vector")
                        {
                            Skip();if(p==end||raw[p]!='(')Fail("VECTOR_ARGUMENT_REQUIRED",start);p++;Skip();
                            var symbol=Symbol();Skip();if(p==end||raw[p]!=')')Fail("INVALID_VECTOR_ARGUMENT",start);p++;
                            var vector=new MathNode("Vector",new[]{symbol},sourceSpans:new[]{new TextSpan(start,p)});
                            Add(Index(vector,start),start,space);
                        }
                        else if(word=="unit")
                        {
                            Skip();if(p==end||raw[p]!='(')Fail("UNIT_ARGUMENT_REQUIRED",start);p++;Skip();
                            var unit=UnitExpression(0);Skip();if(p==end||raw[p]!=')')Fail("INVALID_UNIT_ARGUMENT",start);p++;
                            Add(unit,start,space);
                        }
                        else if(ScientificSymbols.GreekAliases.TryGetValue(word,out var glyph)||ScientificSymbols.IsGreek(word))
                        {
                            var n=new MathNode("Greek",name:glyph??word,sourceSpans:new[]{new TextSpan(start,p)});Add(Index(n,start),start,space);
                        }
                        else if(word.Length==1&&FormulaLexicon.IsAsciiSymbol(word)&&p<end&&(raw[p]=='_'||SubDigit(raw[p])>=0))
                        {
                            var n=new MathNode("Symbol",name:word,sourceSpans:new[]{new TextSpan(start,p)});Add(Index(n,start),start,space);
                        }
                        else
                        {
                            if(p<end&&Digit(raw[p]) && !(FormulaLexicon.TryGetKeyword(word,out var kind)&&kind==FormulaTokenKind.Root))
                                while(p<end&&(FormulaLexicon.IsWordCharacter(raw[p])||Digit(raw[p])))p++;
                            foreach(var t in Basic(start,p))tokens.Add(Copy(t,space));
                        }
                    }
                    else
                    {
                        p++;if((raw[start]=='<'||raw[start]=='>')&&p<end&&raw[p]=='=')p++;
                        foreach(var t in Basic(start,p))tokens.Add(Copy(t,space));
                    }
                }
            }
            catch(Failure failure){errors.Add(new Diagnostic(failure.Code,"error",new TextSpan(failure.Start,Math.Min(end,Math.Max(failure.Start+1,p))),failure.Code));}
            catch(ArgumentException){errors.Add(new Diagnostic("INVALID_PHYSICS_STRUCTURE","error",span));}
            tokens.Add(new FormulaToken(FormulaTokenKind.End,"","",new TextSpan(end,end),false,false));
            return new TokenizationResult(tokens,errors);
        }
        private List<FormulaToken> Basic(int start,int finish)
        {
            var result=new FormulaTokenizer().Tokenize(source,new TextSpan(start,finish),token);
            if(result.Diagnostics.Count>0)Fail(result.Diagnostics[0].Code,start);
            return result.Tokens.Where(t=>t.Kind!=FormulaTokenKind.End).ToList();
        }
        private static FormulaToken Copy(FormulaToken t,bool space)=>new FormulaToken(t.Kind,t.Text,t.NormalizedText,t.Span,t.IsAlias,space);
        private void Add(MathNode node,int start,bool space)=>tokens.Add(new FormulaToken(FormulaTokenKind.Symbol,raw.Substring(start,p-start),raw.Substring(start,p-start),new TextSpan(start,p),true,space,node));
        private MathNode Symbol()
        {
            int start=p;while(p<end&&FormulaLexicon.IsWordCharacter(raw[p]))p++;
            string word=raw.Substring(start,p-start).Normalize(NormalizationForm.FormC);MathNode n;
            if(ScientificSymbols.GreekAliases.TryGetValue(word,out var glyph)||ScientificSymbols.IsGreek(word)) n=new MathNode("Greek",name:glyph??word,sourceSpans:new[]{new TextSpan(start,p)});
            else if(FormulaLexicon.IsAsciiSymbol(word))n=new MathNode("Symbol",name:word,sourceSpans:new[]{new TextSpan(start,p)});
            else{Fail("INVALID_PHYSICS_SYMBOL",start);throw new InvalidOperationException();}
            return Index(n,start);
        }
        private MathNode Index(MathNode basis,int start)
        {
            if(p>=end||raw[p]!='_'&&SubDigit(raw[p])<0)return basis;
            int indexStart=p;bool unicode=raw[p]!='_';if(!unicode)p++;
            bool brace=p<end&&raw[p]=='{';if(brace)p++;int valueStart=p;var text=new StringBuilder();
            while(p<end&&(unicode?SubDigit(raw[p])>=0:Digit(raw[p]))){text.Append(unicode?(char)('0'+SubDigit(raw[p])):raw[p]);p++;}
            string type="Number";
            if(text.Length==0&&!unicode&&p<end&&FormulaLexicon.IsAsciiSymbol(raw[p].ToString())){type="Symbol";text.Append(raw[p++]);}
            if(text.Length==0)Fail("INVALID_SUBSCRIPT",indexStart);
            int valueEnd=p;if(brace){if(p>=end||raw[p]!='}')Fail("UNCLOSED_SUBSCRIPT",indexStart);p++;}
            var child=new MathNode(type,value:type=="Number"?text.ToString():null,name:type=="Symbol"?text.ToString():null,sourceSpans:new[]{new TextSpan(valueStart,valueEnd)});
            return new MathNode("Subscript",new[]{basis,child},sourceSpans:new[]{new TextSpan(start,p)});
        }
        private bool StartsUnit(int offset)
        {
            int finish=offset;while(finish<end&&(char.IsLetter(raw[finish])||raw[finish]=='°'))finish++;
            return finish>offset && ScientificSymbols.IsUnit(raw.Substring(offset,finish-offset));
        }
        private MathNode UnitExpression(int depth)
        {
            if(depth>=32)Fail("UNIT_DEPTH_LIMIT",p);
            var left=UnitFactor(depth);bool divided=false;int count=0;
            while(p<end)
            {
                token.ThrowIfCancellationRequested();int before=p;Skip();if(p==end){p=before;break;}
                var op=raw[p];bool implicitProduct=p>before&&StartsUnit(p);
                if(op!='/'&&op!='*'&&op!='·'&&!implicitProduct){p=before;break;}
                if(++count>64)Fail("UNIT_TERM_LIMIT",p);
                if(op=='/'&&divided)Fail("AMBIGUOUS_UNIT_DIVISION",p);
                if(!implicitProduct)p++;Skip();var right=UnitFactor(depth);
                left=new MathNode("Binary",new[]{left,right},@operator:op=='/'?"divide":"multiply",sourceSpans:new[]{new TextSpan(left.SourceSpans[0].Start,p)});
                if(op=='/')divided=true;
            }
            return left;
        }
        private MathNode UnitFactor(int depth)
        {
            int start=p;MathNode n;
            if(p<end&&raw[p]=='(')
            {
                p++;Skip();n=UnitExpression(depth+1);Skip();if(p>=end||raw[p]!=')')Fail("UNCLOSED_UNIT_GROUP",start);p++;
                return new MathNode(n.Type,n.Children,n.Value,n.Name,n.Operator,new[]{new TextSpan(start,p)});
            }
            while(p<end&&(char.IsLetter(raw[p])||raw[p]=='°'))p++;
            var name=raw.Substring(start,p-start);if(!ScientificSymbols.IsUnit(name))Fail("UNKNOWN_UNIT",start);
            n=new MathNode("Unit",name:name,sourceSpans:new[]{new TextSpan(start,p)});
            if(p<end&&(raw[p]=='^'||SupDigit(raw[p])>=0||raw[p]=='⁻'))
            {
                bool unicode=raw[p]!='^';if(!unicode)p++;int exponentStart=p;
                bool negative=p<end&&(raw[p]=='-'||raw[p]=='−'||raw[p]=='⁻');if(negative)p++;
                int numberStart=p;var text=new StringBuilder();
                while(p<end&&(unicode?SupDigit(raw[p])>=0:Digit(raw[p]))){text.Append(unicode?(char)('0'+SupDigit(raw[p])):raw[p]);p++;}
                if(text.Length==0)Fail("INVALID_UNIT_EXPONENT",exponentStart);
                MathNode exponent=new MathNode("Number",value:text.ToString(),sourceSpans:new[]{new TextSpan(numberStart,p)});
                if(negative)exponent=new MathNode("Unary",new[]{exponent},@operator:"minus",sourceSpans:new[]{new TextSpan(exponentStart,p)});
                n=new MathNode("Power",new[]{n,exponent},sourceSpans:new[]{new TextSpan(start,p)});
            }
            return n;
        }
        private void Skip(){while(p<end&&char.IsWhiteSpace(raw[p])&&raw[p]!='\r'&&raw[p]!='\n'){token.ThrowIfCancellationRequested();p++;}}
        private static bool Digit(char c)=>c>='0'&&c<='9';
        private static int SubDigit(char c)=>"₀₁₂₃₄₅₆₇₈₉".IndexOf(c);
        private static int SupDigit(char c)=>"⁰¹²³⁴⁵⁶⁷⁸⁹".IndexOf(c);
        private static void Fail(string code,int start)=>throw new Failure(code,start);
        private sealed class Failure:Exception{internal readonly string Code;internal readonly int Start;internal Failure(string code,int start){Code=code;Start=start;}}
    }
}
