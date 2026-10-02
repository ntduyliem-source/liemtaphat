using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Locus.Core.Parsing;
using Locus.Core.Export;

namespace Locus.Core.Detection
{
    internal static class DomainAnalysisEngine
    {
        internal static AnalysisResult Analyze(SourceSnapshot source, AnalysisOptions options, CancellationToken token, bool chemistryAliases = false)
        {
            var all=new TextSpan(0,source.Raw.Length);
            if(options.EnabledDomains==DetectionDomains.None)return new AnalysisResult(source,DetectionStatus.Reject,diagnostics:new[]{new Diagnostic("DETECTION_DISABLED","info",all,"Bật ít nhất một mục nhận diện để xử lý nguồn mới.")});
            var protectedText=ProtectedTextRecognizer.Find(source.Raw,token,options.InputMode==InputMode.Passive);
            if((options.EnabledDomains&DetectionDomains.Physics)!=0)
                protectedText.RemoveAll(t=>QuantityUnitContext(source,t,token));
            var regions=new List<CandidateSet>();var diagnostics=new List<Diagnostic>();
            void Parse(TextSpan content,TextSpan replacement,MarkerConfiguration? markers)
            {
                token.ThrowIfCancellationRequested();
                if(content.Length>options.MaxRegionLength){diagnostics.Add(new Diagnostic("REGION_LENGTH_LIMIT","error",content));return;}
                var result=Route(source,content,replacement,options.EnabledDomains,token,chemistryAliases);
                if(result.Candidates.Count==0){diagnostics.AddRange(result.Diagnostics);return;}
                if(options.InputMode==InputMode.Passive && !HasPassiveEvidence(result,source,content,token))return;
                regions.Add(new CandidateSet(source,content,replacement,result.Candidates,result.Diagnostics,result.SelectedCandidateId,markers));
            }
            if(options.InputMode==InputMode.Markers)
            {
                var located=MarkerRegionLocator.Find(source,options,protectedText,token);diagnostics.AddRange(located.Diagnostics);
                if(located.LimitExceeded)return new AnalysisResult(source,DetectionStatus.Reject,diagnostics:diagnostics);
                foreach(var r in located.Regions)Parse(r.ContentSpan,r.ReplacementSpan,options.Markers);
            }
            else if(options.InputMode==InputMode.Explicit)
            {
                if(protectedText.Count>0)diagnostics.AddRange(protectedText.Select(t=>new Diagnostic(t.Code,"error",t.Span)));
                else Parse(all,all,null);
            }
            else
            {
                int count=0;
                foreach(var span in Corridors(source,protectedText,options.EnabledDomains,token,chemistryAliases))
                {
                    if(++count>options.MaxRegions)return new AnalysisResult(source,DetectionStatus.Reject,diagnostics:new[]{new Diagnostic("REGION_COUNT_LIMIT","error",all)});
                    Parse(span,span,null);
                }
            }
            if(regions.Count==0&&diagnostics.Count==0)diagnostics.Add(new Diagnostic("INSUFFICIENT_FORMULA_EVIDENCE","info",all));
            token.ThrowIfCancellationRequested();
            return new AnalysisResult(source,regions.Count>0?DetectionStatus.Accept:DetectionStatus.Reject,regions,diagnostics,
                diagnostics.Any(d=>d.Code.Contains("MISSING")||d.Code.Contains("UNCLOSED")));
        }

        internal static CandidateSet Route(SourceSnapshot source,TextSpan content,TextSpan replacement,DetectionDomains enabled,CancellationToken token,bool chemistryAliases = false)
        {
            var sets=new List<CandidateSet>();
            if((enabled&DetectionDomains.Physics)!=0)sets.Add(new FormulaParser(true).Parse(source,content,replacement,token));
            if((enabled&DetectionDomains.Chemistry)!=0)sets.Add(new ChemistryParser(chemistryAliases).Parse(source,content,replacement,token));
            if((enabled&DetectionDomains.Math)!=0)sets.Add(new FormulaParser().Parse(source,content,replacement,token));
            // Keep historical math candidates when science added no extra structure.
            var math=sets.FirstOrDefault(s=>s.Candidates.Any(c=>c.Document.Domain=="math"));
            if(math!=null)sets.RemoveAll(s=>s.Candidates.Count>0 && s.Candidates.All(c=>c.Document.Domain=="physics" && !Extended(c.Document.Root)));
            var successful=sets.Where(s=>s.Candidates.Count>0).ToList();
            if(successful.Count==0)return new CandidateSet(source,content,replacement,Array.Empty<Candidate>(),sets.SelectMany(s=>s.Diagnostics).GroupBy(d=>d.Code).Select(g=>g.First()).Take(16));
            if(successful.Count==1)return successful[0];
            var candidates=new List<Candidate>();var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var candidate in successful.SelectMany(s=>s.Candidates).OrderBy(c=>c.Kind=="repair"?2:c.Kind=="interpretation"?1:0))
            {
                token.ThrowIfCancellationRequested();var key=CandidateExporter.ToMathMl(candidate);
                if(!keys.Add(key))continue;
                var kind=candidate.Kind=="direct"&&candidates.Count>0?"interpretation":candidate.Kind;
                candidates.Add(kind==candidate.Kind?candidate:new Candidate(kind,candidate.Document,source,content,replacement,candidate.Diagnostics,candidate.Edits,"domain-alternative/"+candidate.Provenance));
                if(candidates.Count==3)break;
            }
            var diagnostics=successful.SelectMany(s=>s.Diagnostics).GroupBy(d=>d.Code).Select(g=>g.First()).ToList();
            if(candidates.Count(c=>c.Kind!="repair")>1)diagnostics.Add(new Diagnostic("AMBIGUOUS_DOMAIN","warning",content,"Có nhiều cách hiểu theo những mục nhận diện đã bật."));
            return new CandidateSet(source,content,replacement,candidates,diagnostics);
        }
        private static bool Extended(MathNode n)=>Locus.Core.Domains.ScientificNodes.IsExtended(n.Type)||n.Children.Any(Extended);

        // Enabling another domain must not turn ordinary numbers/letters into formulas in prose.
        // Explicit inputs and wrappers still accept these values through the normal parser.
        internal static bool HasPassiveEvidence(CandidateSet set, SourceSnapshot source, TextSpan content, CancellationToken token)
        {
            if (set.Candidates.Any(c => c.Document.Domain == "chemistry" && !c.Diagnostics.Any(d => d.Code == "CHEMISTRY_WEAK_CONTEXT") ||
                c.Document.Domain == "physics" && Extended(c.Document.Root))) return true;
            return PassiveRegionLocator.HasFormulaEvidence(source, content, token);
        }

        // A whitelisted unit after a numeric value is different from an opaque relative path.
        // URL/email and rooted paths never qualify; bare kg/m still remains protected.
        internal static bool QuantityUnitContext(SourceSnapshot source,ProtectedText item,CancellationToken token)
        {
            if(item.Code!="PROTECTED_PATH")return false;
            var raw=source.Raw;int p=item.Span.Start;
            while(p>0&&char.IsWhiteSpace(raw[p-1])&&raw[p-1]!='\r'&&raw[p-1]!='\n')p--;
            if(p==item.Span.Start||p==0||raw[p-1]<'0'||raw[p-1]>'9')return false;
            while(p>0&&(raw[p-1]>='0'&&raw[p-1]<='9'||raw[p-1]=='.'||raw[p-1]==','))p--;
            if(p>0&&(char.IsLetterOrDigit(raw[p-1])||raw[p-1]=='_'||raw[p-1]=='/'||raw[p-1]=='\\'))return false;
            int end=item.Span.End;
            while(end>item.Span.Start)
            {
                if(new PhysicsTokenizer().IsUnitFragment(source,new TextSpan(item.Span.Start,end),token))return true;
                if(!".,;!?:)]}".Contains(raw[end-1]))break;
                end--;
            }
            return false;
        }

        internal static IEnumerable<TextSpan> Corridors(SourceSnapshot source,IReadOnlyList<ProtectedText> protectedText,DetectionDomains domains,CancellationToken token,bool chemistryAliases = false)
        {
            var raw=source.Raw;int p=0,start=-1,finish=0;bool poisoned=false,previousOpaque=false,opaqueOperator=false;
            while(p<raw.Length)
            {
                token.ThrowIfCancellationRequested();var protect=protectedText.FirstOrDefault(t=>t.Span.Start<=p&&p<t.Span.End);
                if(protect!=null||Boundary(raw,p))
                {
                    if(start>=0&&!poisoned)yield return new TextSpan(start,finish);
                    start=-1;poisoned=false;previousOpaque=protect!=null;opaqueOperator=false;p=protect?.Span.End??p+1;continue;
                }
                if(char.IsWhiteSpace(raw[p])){p++;continue;}
                int a=p;while(p<raw.Length&&!char.IsWhiteSpace(raw[p])&&!Boundary(raw,p))p++;
                var span=new TextSpan(a,p);var text=source.Slice(span);
                bool allowed=new FormulaTokenizer().Tokenize(source,span,token).Diagnostics.Count==0;
                if(!allowed&&(domains&DetectionDomains.Physics)!=0)allowed=new PhysicsTokenizer().Tokenize(source,span,token).Diagnostics.Count==0 || new PhysicsTokenizer().IsUnitFragment(source,span,token);
                if(!allowed&&(domains&DetectionDomains.Chemistry)!=0)allowed=text=="->"||text=="<->"||text=="→"||text=="⇌"||new ChemistryParser(chemistryAliases).Parse(source,span,cancellationToken:token).Candidates.Count>0;
                if(allowed)
                {
                    if(start<0){start=a;poisoned=previousOpaque&&(opaqueOperator||Operator(raw[a]));}
                    finish=p;previousOpaque=false;
                }
                else
                {
                    if(start>=0&&!poisoned&&!Operator(raw[a]))yield return new TextSpan(start,finish);
                    start=-1;poisoned=false;previousOpaque=true;opaqueOperator=Operator(raw[p-1]);
                }
            }
            if(start>=0&&!poisoned)yield return new TextSpan(start,finish);
        }
        private static bool Operator(char c)=>"+-*/^=<>×÷−".Contains(c);
        private static bool Boundary(string text,int i)=>text[i]=='\r'||text[i]=='\n'||text[i]==';'||text[i]==':'||text[i]=='!'||text[i]=='?'||
            (text[i]=='.'||text[i]==',')&&!(i>0&&i+1<text.Length&&char.IsDigit(text[i-1])&&char.IsDigit(text[i+1]));
    }
}
