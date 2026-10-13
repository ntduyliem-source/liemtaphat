using System;
using System.Collections.Generic;
using System.Threading;
using Locus.Core.Parsing;

namespace Locus.Core.Detection
{
    internal static class PassiveRegionLocator
    {
        internal static bool HasFormulaEvidence(SourceSnapshot source, TextSpan span, CancellationToken token)
        {
            var lexical = new FormulaTokenizer().Tokenize(source, span, token);
            if (lexical.Diagnostics.Count != 0) return false;
            var tokens = new List<FormulaToken>();
            foreach (var item in lexical.Tokens)
                if (item.Kind != FormulaTokenKind.End) tokens.Add(item);
            return Complete(tokens).HasValue;
        }

        /// <summary>
        /// Finds lexical math corridors, never a successful suffix inside an opaque identifier/address.
        /// The shared tokenizer defines tokens; FormulaParser alone determines grammar and candidates.
        /// </summary>
        public static IEnumerable<TextSpan> Find(SourceSnapshot source, IReadOnlyList<ProtectedText> protectedText,
            CancellationToken cancellationToken)
        {
            string raw = source.Raw;
            var tokenizer = new FormulaTokenizer();
            var corridor = new List<FormulaToken>();
            int position = 0, protectedIndex = 0;
            bool followsOpaqueToken = false, poisonedPrefix = false, opaqueEndsOperator = false;
            while (position < raw.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (protectedIndex < protectedText.Count && protectedText[protectedIndex].Span.End <= position) protectedIndex++;
                if (protectedIndex < protectedText.Count && protectedText[protectedIndex].Span.Start <= position)
                {
                    var result = poisonedPrefix ? null : Complete(corridor); corridor.Clear();
                    poisonedPrefix = false; followsOpaqueToken = true; opaqueEndsOperator = false;
                    if (result.HasValue) yield return result.Value;
                    position = protectedText[protectedIndex++].Span.End;
                    continue;
                }
                if (FormulaDetectionSyntax.IsBoundary(raw, position))
                {
                    var result = poisonedPrefix ? null : Complete(corridor); corridor.Clear();
                    poisonedPrefix = false; followsOpaqueToken = false; opaqueEndsOperator = false;
                    if (result.HasValue) yield return result.Value;
                    position++;
                    continue;
                }
                if (char.IsWhiteSpace(raw[position])) { position++; continue; }
                int start = position;
                while (position < raw.Length && !char.IsWhiteSpace(raw[position]) && !FormulaDetectionSyntax.IsBoundary(raw, position))
                {
                    if ((position & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                    position++;
                }
                var island = tokenizer.Tokenize(source, new TextSpan(start, position), cancellationToken);
                if (island.Diagnostics.Count > 0)
                {
                    // Do not salvage x^2 from abc+x^2 or a URL-looking unsupported identifier.
                    var result = poisonedPrefix || FormulaDetectionSyntax.IsOperatorSymbol(raw[start]) ? null : Complete(corridor); corridor.Clear();
                    poisonedPrefix = false; followsOpaqueToken = true;
                    opaqueEndsOperator = FormulaDetectionSyntax.IsOperatorSymbol(raw[position - 1]);
                    if (result.HasValue) yield return result.Value;
                }
                else
                {
                    if (corridor.Count == 0 && followsOpaqueToken && island.Tokens.Count > 0 &&
                        (opaqueEndsOperator || FormulaDetectionSyntax.IsLeadingOperator(island.Tokens[0].Kind)))
                        poisonedPrefix = true;
                    foreach (var token in island.Tokens)
                        if (token.Kind != FormulaTokenKind.End) corridor.Add(token);
                    followsOpaqueToken = false;
                    opaqueEndsOperator = false;
                }
            }
            var final = poisonedPrefix ? null : Complete(corridor);
            if (final.HasValue) yield return final.Value;
        }

        private static TextSpan? Complete(List<FormulaToken> tokens)
        {
            if (tokens.Count == 0) return null;
            int operands = 0;
            bool explicitOperator = false, root = false, implicitProduct = false, powerOrRelation = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (token.Kind == FormulaTokenKind.Number || token.Kind == FormulaTokenKind.Symbol) operands++;
                root |= token.Kind == FormulaTokenKind.Root;
                explicitOperator |= token.Kind == FormulaTokenKind.Plus || token.Kind == FormulaTokenKind.Minus ||
                    token.Kind == FormulaTokenKind.Multiply || token.Kind == FormulaTokenKind.Divide;
                powerOrRelation |= token.Kind == FormulaTokenKind.Power || token.Kind == FormulaTokenKind.Equal ||
                    token.Kind == FormulaTokenKind.Less || token.Kind == FormulaTokenKind.Greater ||
                    token.Kind == FormulaTokenKind.LessEqual || token.Kind == FormulaTokenKind.GreaterEqual;
                if (i == 0 || tokens[i - 1].Span.End != token.Span.Start) continue;
                var previous = tokens[i - 1].Kind;
                implicitProduct |= previous == FormulaTokenKind.Number && token.Kind == FormulaTokenKind.Symbol ||
                    token.Kind == FormulaTokenKind.LeftParen && (previous == FormulaTokenKind.Number ||
                    previous == FormulaTokenKind.Symbol || previous == FormulaTokenKind.RightParen);
            }
            // Unary +x/-2 and a lone alias/number/letter are insufficient passive evidence.
            if (!(root && operands > 0 || powerOrRelation && operands > 0 || explicitOperator && operands >= 2 || implicitProduct)) return null;
            return new TextSpan(tokens[0].Span.Start, tokens[tokens.Count - 1].Span.End);
        }

    }
}
