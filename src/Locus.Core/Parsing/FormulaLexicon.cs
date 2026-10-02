using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Locus.Core.Parsing
{
    /// <summary>Tokens in the versioned Vietnamese math grammar, shared with detection.</summary>
    public enum FormulaTokenKind
    {
        Number, Symbol, Plus, Minus, Multiply, Divide, Power, Root,
        Equal, Less, Greater, LessEqual, GreaterEqual,
        LeftParen, RightParen, End, Unknown
    }

    public static class FormulaLexicon
    {
        private static readonly Dictionary<string, FormulaTokenKind> Keywords =
            new Dictionary<string, FormulaTokenKind>(StringComparer.Ordinal)
            {
                ["cộng"] = FormulaTokenKind.Plus, ["cong"] = FormulaTokenKind.Plus,
                ["trừ"] = FormulaTokenKind.Minus, ["tru"] = FormulaTokenKind.Minus,
                ["nhân"] = FormulaTokenKind.Multiply, ["nhan"] = FormulaTokenKind.Multiply,
                ["chia"] = FormulaTokenKind.Divide, ["trên"] = FormulaTokenKind.Divide,
                ["tren"] = FormulaTokenKind.Divide,
                ["mũ"] = FormulaTokenKind.Power, ["mu"] = FormulaTokenKind.Power,
                ["sqrt"] = FormulaTokenKind.Root, ["căn"] = FormulaTokenKind.Root,
                ["can"] = FormulaTokenKind.Root,
                ["bằng"] = FormulaTokenKind.Equal, ["bang"] = FormulaTokenKind.Equal
            };

        public static bool TryGetKeyword(string word, out FormulaTokenKind kind)
        {
            kind = FormulaTokenKind.Unknown;
            return word != null && Keywords.TryGetValue(word.Normalize(NormalizationForm.FormC), out kind);
        }

        public static bool IsSupportedWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            var normalized = word.Normalize(NormalizationForm.FormC);
            FormulaTokenKind kind;
            return IsAsciiSymbol(normalized) || Keywords.TryGetValue(normalized, out kind);
        }

        public static bool IsCompactRootNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var normalized = text.Normalize(NormalizationForm.FormC);
            var offset = 0;
            while (offset < normalized.Length && !IsAsciiDigit(normalized[offset])) offset++;
            if (offset == 0 || offset == normalized.Length) return false;
            FormulaTokenKind kind;
            if (!Keywords.TryGetValue(normalized.Substring(0, offset), out kind) || kind != FormulaTokenKind.Root) return false;
            var separatorSeen = false;
            for (var index = offset; index < normalized.Length; index++)
            {
                var ch = normalized[index];
                if (IsAsciiDigit(ch)) continue;
                if ((ch == '.' || ch == ',') && !separatorSeen && index > offset && index + 1 < normalized.Length && IsAsciiDigit(normalized[index + 1]))
                {
                    separatorSeen = true;
                    continue;
                }
                return false;
            }
            return true;
        }

        internal static bool IsAsciiDigit(char ch) => ch >= '0' && ch <= '9';
        internal static bool IsAsciiSymbol(string text) => text.Length == 1 &&
            ((text[0] >= 'a' && text[0] <= 'z') || (text[0] >= 'A' && text[0] <= 'Z'));

        internal static bool IsWordCharacter(char ch)
        {
            var category = char.GetUnicodeCategory(ch);
            return char.IsLetter(ch) || category == UnicodeCategory.NonSpacingMark ||
                category == UnicodeCategory.SpacingCombiningMark || category == UnicodeCategory.EnclosingMark;
        }
    }
}
