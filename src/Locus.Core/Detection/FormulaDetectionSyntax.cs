using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Locus.Core.Parsing;

namespace Locus.Core.Detection
{
    /// <summary>Shared lexical boundaries for all passive detectors, without extending any domain grammar.</summary>
    internal static class FormulaDetectionSyntax
    {
        private static readonly HashSet<string> UnsupportedFunctions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "sin", "cos", "tan", "cot", "sec", "csc", "asin", "acos", "atan", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "log", "ln", "exp", "abs", "lim", "int" };

        internal static bool IsBoundary(string raw, int offset)
        {
            char current = raw[offset];
            if (current == '.' || current == ',')
                return !(offset > 0 && offset + 1 < raw.Length && FormulaLexicon.IsAsciiDigit(raw[offset - 1]) && FormulaLexicon.IsAsciiDigit(raw[offset + 1]));
            return current == '\r' || current == '\n' || current == ';' || current == ':' || current == '!' || current == '?' ||
                current == '"' || current == '\'' || current == '\u201c' || current == '\u201d' || current == '\u2018' || current == '\u2019';
        }

        internal static bool IsOperatorSymbol(char value) => value == '+' || value == '-' || value == '\u2212' ||
            value == '*' || value == '/' || value == '^' || value == '=' || value == '<' || value == '>' ||
            value == '\u00d7' || value == '\u00b7' || value == '\u00f7' || value == '\u2264' || value == '\u2265';

        internal static bool IsLeadingOperator(FormulaTokenKind kind) => kind == FormulaTokenKind.Plus ||
            kind == FormulaTokenKind.Minus || kind == FormulaTokenKind.Multiply || kind == FormulaTokenKind.Divide ||
            kind == FormulaTokenKind.Power || kind == FormulaTokenKind.Equal || kind == FormulaTokenKind.Less ||
            kind == FormulaTokenKind.Greater || kind == FormulaTokenKind.LessEqual || kind == FormulaTokenKind.GreaterEqual;

        internal static IEnumerable<TextSpan> UnsupportedFunctionCalls(string raw, CancellationToken cancellationToken)
        {
            int position = 0;
            while (position < raw.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!FormulaLexicon.IsWordCharacter(raw[position])) { position++; continue; }
                int start = position;
                while (position < raw.Length && FormulaLexicon.IsWordCharacter(raw[position])) position++;
                if (start > 0 && (char.IsLetterOrDigit(raw[start - 1]) || raw[start - 1] == '_') ||
                    !UnsupportedFunctions.Contains(raw.Substring(start, position - start).Normalize(NormalizationForm.FormC))) continue;
                int open = position;
                while (open < raw.Length && char.IsWhiteSpace(raw[open]) && raw[open] != '\r' && raw[open] != '\n') open++;
                if (open == raw.Length || raw[open] != '(') continue;
                int end = open + 1, depth = 1;
                while (end < raw.Length && raw[end] != '\r' && raw[end] != '\n')
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    char current = raw[end++];
                    if (current == '(') depth++;
                    else if (current == ')' && --depth == 0) break;
                }
                // Incomplete calls remain opaque through their line; do not salvage an argument suffix.
                yield return new TextSpan(start, end);
                position = end;
            }
        }
    }
}
