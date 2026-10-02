using System;
using System.Collections.Generic;
using System.Threading;
using Locus.Core.Parsing;

namespace Locus.Core.Assistance
{
    public sealed class ReactionDraftResult
    {
        public ReactionDraft? Draft { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }
        internal ReactionDraftResult(ReactionDraft? draft, IEnumerable<Diagnostic>? diagnostics = null) { Draft = draft; Diagnostics = Freeze.Of(diagnostics); }
    }
    public static class ReactionDraftParser
    {
        public static ReactionDraftResult Parse(AssistanceRegion region, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();var source=region.Source;int end=region.ContentSpan.End,start=region.ContentSpan.Start;
            int separatorStart=-1,separatorEnd=-1;string arrow="arrow";
            for(int p=start;p<end;p++)
            {
                token.ThrowIfCancellationRequested();string? literal=null;
                foreach(var possible in new[]{"<->","->","→","⇌","="})
                    if(p+possible.Length<=end&&string.CompareOrdinal(source.Raw,p,possible,0,possible.Length)==0){literal=possible;break;}
                if(literal==null)continue;
                if(separatorStart>=0)return Error("MULTIPLE_REACTION_SEPARATORS");
                separatorStart=p;separatorEnd=p+literal.Length;arrow=literal=="<->"||literal=="⇌"?"reversible":"arrow";p=separatorEnd-1;
            }
            if(separatorStart<0)return Error("MISSING_REACTION_SEPARATOR");
            if(!string.IsNullOrWhiteSpace(source.Raw.Substring(separatorEnd,end-separatorEnd)))return Error("REACTION_PRODUCTS_PRESENT");
            int leftStart=start,leftEnd=separatorStart;
            while(leftStart<leftEnd&&char.IsWhiteSpace(source.Raw[leftStart]))leftStart++;
            while(leftEnd>leftStart&&char.IsWhiteSpace(source.Raw[leftEnd-1]))leftEnd--;
            if(leftEnd==leftStart)return Error("MISSING_CHEMISTRY_SPECIES");
            var left=new ChemistryParser(true).Parse(source,new TextSpan(leftStart,leftEnd),cancellationToken:token);
            if(left.Candidates.Count!=1)return new ReactionDraftResult(null,left.Diagnostics);
            return new ReactionDraftResult(new ReactionDraft(region,left,new TextSpan(separatorStart,separatorEnd),arrow));
            ReactionDraftResult Error(string code)=>new ReactionDraftResult(null,new[]{new Diagnostic(code,"error",region.ContentSpan)});
        }
    }
}
