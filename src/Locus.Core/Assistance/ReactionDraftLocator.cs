using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Locus.Core.Detection;
using Locus.Core.Parsing;

namespace Locus.Core.Assistance
{
    /// <summary>Find complete reactant spans ending in '=' without consuming the surrounding prose.</summary>
    public static class ReactionDraftLocator
    {
        public static IReadOnlyList<TextSpan> Find(SourceSnapshot source, CancellationToken token = default)
        {
            var raw = source.Raw; var result = new List<TextSpan>();
            if (raw.Length > FormulaParser.MaximumRegionLength) return result;
            var protectedText = ProtectedTextRecognizer.Find(raw, token, true);
            int previousEnd = 0;
            for (int p = 0; p < raw.Length && result.Count < 256; p++)
            {
                token.ThrowIfCancellationRequested();
                int separatorLength = raw[p] == '=' || raw[p] == '→' ? 1 : p + 1 < raw.Length && raw[p] == '-' && raw[p + 1] == '>' ? 2 : 0;
                if (separatorLength == 0 || protectedText.Any(t => t.Span.Start <= p && p < t.Span.End)) continue;
                int end = p + separatorLength;
                if (end < raw.Length && !char.IsWhiteSpace(raw[end]) && ".,;!?".IndexOf(raw[end]) < 0) continue;
                int next = end; while (next < raw.Length && raw[next] != '\r' && raw[next] != '\n' && char.IsWhiteSpace(raw[next])) next++;
                int wordEnd = next; while (wordEnd < raw.Length && !char.IsWhiteSpace(raw[wordEnd]) && ".,;!?".IndexOf(raw[wordEnd]) < 0) wordEnd++;
                // A product following a space still belongs to a complete equation.
                if (wordEnd > next && new ChemistryParser(true).Parse(source, new TextSpan(next, wordEnd), cancellationToken: token).Candidates.Count > 0) continue;
                int left = p;
                while (left > previousEnd && ChemicalCharacter(raw[left - 1])) left--;
                int attempts = 0;
                for (int start = left; start < p && attempts < 128; start++)
                {
                    if (char.IsWhiteSpace(raw[start]) || start > 0 && (char.IsLetterOrDigit(raw[start - 1]) || "_+^-".IndexOf(raw[start - 1]) >= 0)) continue;
                    int before = start; while (before > left && char.IsWhiteSpace(raw[before - 1])) before--;
                    if (before > left && "+-^".IndexOf(raw[before - 1]) >= 0) continue;
                    if (protectedText.Any(t => t.Span.Start < end && t.Span.End > start)) continue;
                    attempts++; var span = new TextSpan(start, end);
                    var draft = ReactionDraftParser.Parse(new AssistanceRegion(source, span, span), token).Draft;
                    if (draft == null || draft.Reactants.Candidates.Count != 1 ||
                        draft.Reactants.Candidates[0].Diagnostics.Concat(draft.Reactants.Diagnostics).Any(d => d.Severity == "warning" || d.Severity == "error")) continue;
                    result.Add(span); previousEnd = end; break;
                }
                p = end - 1;
            }
            return result;
        }
        private static bool ChemicalCharacter(char c) => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || char.IsDigit(c) ||
            "()[]+^-⁺⁻".IndexOf(c) >= 0 || c == ' ' || c == '\t';
    }
}
