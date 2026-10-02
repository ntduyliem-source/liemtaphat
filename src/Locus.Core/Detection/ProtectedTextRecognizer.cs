using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Locus.Core.Parsing;

namespace Locus.Core.Detection
{
    internal sealed class ProtectedText
    {
        public TextSpan Span { get; }
        public string Code { get; }
        public ProtectedText(TextSpan span, string code) { Span = span; Code = code; }
    }

    /// <summary>Recognizes opaque address/path tokens before any mathematical substring search.</summary>
    internal static class ProtectedTextRecognizer
    {
        public static List<ProtectedText> Find(string raw, CancellationToken cancellationToken, bool protectDates = false)
        {
            var result = new List<ProtectedText>();
            int i = 0;
            while (i < raw.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (char.IsWhiteSpace(raw[i])) { i++; continue; }
                int start = i;
                // A quoted path can contain spaces. It remains opaque through its closing quote or line end.
                if ((raw[i] == '\'' || raw[i] == '"') && IsPathPrefix(raw, i + 1))
                {
                    char quote = raw[i++];
                    while (i < raw.Length && raw[i] != quote && raw[i] != '\r' && raw[i] != '\n')
                    { if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested(); i++; }
                    if (i < raw.Length && raw[i] == quote) i++;
                    result.Add(new ProtectedText(new TextSpan(start, i), "PROTECTED_PATH"));
                    continue;
                }
                while (i < raw.Length && !char.IsWhiteSpace(raw[i]))
                { if ((i & 255) == 0) cancellationToken.ThrowIfCancellationRequested(); i++; }
                string? code = Classify(raw, start, i);
                if (code == null && protectDates && LooksLikeDate(raw, start, i)) code = "PROTECTED_DATE";
                if (code != null) result.Add(new ProtectedText(new TextSpan(start, i), code));
            }
            return result;
        }

        // Only passive prose uses this guard. Explicit input and wrappers can still
        // express 28/09 as division, and a date-shaped operand inside x+28/09 is math.
        private static bool LooksLikeDate(string raw, int start, int end)
        {
            int left = start, right = end;
            while (left < right && "\"'(<[".IndexOf(raw[left]) >= 0) left++;
            while (right > left && "\"')>],;:.!?".IndexOf(raw[right - 1]) >= 0) right--;
            if (right - left < 3 || right - left > 10) return false;
            int before = start, after = right;
            while (before > 0 && char.IsWhiteSpace(raw[before - 1]) && raw[before - 1] != '\r' && raw[before - 1] != '\n') before--;
            while (after < raw.Length && char.IsWhiteSpace(raw[after]) && raw[after] != '\r' && raw[after] != '\n') after++;
            if (before > 0 && "+-*/^=<>×÷−".IndexOf(raw[before - 1]) >= 0 ||
                after < raw.Length && "+-*/^=<>×÷−".IndexOf(raw[after]) >= 0) return false;
            int wordStart = before;
            while (wordStart > 0 && (char.IsLetter(raw[wordStart - 1]) || CharUnicodeInfo.GetUnicodeCategory(raw[wordStart - 1]) == UnicodeCategory.NonSpacingMark)) wordStart--;
            if (wordStart < before && FormulaLexicon.IsSupportedWord(raw.Substring(wordStart, before - wordStart).Normalize(NormalizationForm.FormC))) return false;
            int wordEnd = after;
            while (wordEnd < raw.Length && (char.IsLetter(raw[wordEnd]) || CharUnicodeInfo.GetUnicodeCategory(raw[wordEnd]) == UnicodeCategory.NonSpacingMark)) wordEnd++;
            if (wordEnd > after && FormulaLexicon.IsSupportedWord(raw.Substring(after, wordEnd - after).Normalize(NormalizationForm.FormC))) return false;
            var text = raw.Substring(left, right - left);
            char separator = text.IndexOf('/') >= 0 ? '/' : text.IndexOf('-') >= 0 ? '-' : '.';
            var parts = text.Split(separator);
            if (parts.Length < 2 || parts.Length > 3) return false;
            var values = new int[parts.Length];
            for (int p = 0; p < parts.Length; p++)
            {
                if (parts[p].Length == 0 || parts[p].Length > 4) return false;
                foreach (char c in parts[p]) if (c < '0' || c > '9') return false;
                if (!int.TryParse(parts[p], NumberStyles.None, CultureInfo.InvariantCulture, out values[p])) return false;
            }
            if (parts.Length == 3)
            {
                bool iso = parts[0].Length == 4;
                int day = iso ? values[2] : values[0], month = values[1];
                string year = iso ? parts[0] : parts[2];
                return day >= 1 && day <= 31 && month >= 1 && month <= 12 &&
                    (year.Length == 2 || year.Length == 4) && (iso ? parts[2].Length : parts[0].Length) <= 2 && parts[1].Length <= 2;
            }
            if (separator != '/' || parts[0].Length > 2 || parts[1].Length > 2 ||
                values[0] < 1 || values[0] > 31 || values[1] < 1 || values[1] > 12) return false;
            bool padded = parts[0].Length == 2 && parts[0][0] == '0' || parts[1].Length == 2 && parts[1][0] == '0';
            if (padded) return true;
            int cueEnd = start;
            while (cueEnd > 0 && (char.IsWhiteSpace(raw[cueEnd - 1]) && raw[cueEnd - 1] != '\r' && raw[cueEnd - 1] != '\n' || raw[cueEnd - 1] == ':')) cueEnd--;
            int cueStart = cueEnd;
            while (cueStart > 0 && (char.IsLetter(raw[cueStart - 1]) || CharUnicodeInfo.GetUnicodeCategory(raw[cueStart - 1]) == UnicodeCategory.NonSpacingMark)) cueStart--;
            string cue = raw.Substring(cueStart, cueEnd - cueStart).Normalize(NormalizationForm.FormC);
            return string.Equals(cue, "ngày", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cue, "ngay", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cue, "date", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cue, "dated", StringComparison.OrdinalIgnoreCase);
        }

        private static string? Classify(string text, int start, int end)
        {
            int contentStart = start;
            while (contentStart < end && (text[contentStart] == '"' || text[contentStart] == '\'' || text[contentStart] == '(' || text[contentStart] == '<')) contentStart++;
            if (contentStart == end) return null;
            // Schemes are detected inside enclosing punctuation, including Markdown link syntax.
            int scheme = text.IndexOf("://", contentStart, end - contentStart, StringComparison.Ordinal);
            if (scheme > contentStart)
            {
                int left = scheme - 1;
                while (left >= contentStart && (IsAsciiLetter(text[left]) || char.IsDigit(text[left]) || text[left] == '+' || text[left] == '-' || text[left] == '.')) left--;
                if (left + 1 < scheme && IsAsciiLetter(text[left + 1])) return "PROTECTED_URL";
            }
            if (StartsWith(text, contentStart, end, "www.") || StartsWith(text, contentStart, end, "mailto:")) return "PROTECTED_URL";
            int at = text.IndexOf('@', contentStart, end - contentStart);
            if (at > contentStart && at + 1 < end && (char.IsLetterOrDigit(text[at + 1]) || text[at + 1] == '[')) return "PROTECTED_EMAIL";
            if (IsPathPrefix(text, contentStart)) return "PROTECTED_PATH";
            // A directory name longer than a single variable makes a relative slash path opaque.
            // x/y and 2/3x deliberately remain mathematical input candidates.
            int slash = text.IndexOf('/', contentStart, end - contentStart);
            if (slash > contentStart + 1)
            {
                bool wordDirectory = IsAsciiLetter(text[contentStart]);
                for (int j = contentStart; j < slash && wordDirectory; j++)
                    wordDirectory = char.IsLetterOrDigit(text[j]) || text[j] == '_' || text[j] == '-' || text[j] == '.';
                string firstPart = text.Substring(contentStart, slash - contentStart);
                if (wordDirectory && !FormulaLexicon.IsSupportedWord(firstPart) && !FormulaLexicon.IsCompactRootNumber(firstPart)) return "PROTECTED_PATH";
            }
            return null;
        }

        private static bool IsPathPrefix(string text, int start)
        {
            int left = text.Length - start;
            if (left <= 0) return false;
            if (left >= 3 && IsAsciiLetter(text[start]) && text[start + 1] == ':' && (text[start + 2] == '\\' || text[start + 2] == '/')) return true;
            if (left >= 2 && text[start] == '\\' && text[start + 1] == '\\') return true;
            if (text[start] == '/' && left >= 2 && (char.IsLetter(text[start + 1]) || text[start + 1] == '~')) return true;
            if (left >= 2 && (text[start] == '.' || text[start] == '~') && text[start + 1] == '/') return true;
            return left >= 3 && text[start] == '.' && text[start + 1] == '.' && text[start + 2] == '/';
        }
        private static bool StartsWith(string source, int start, int end, string value) =>
            end - start >= value.Length && string.Compare(source, start, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;
        private static bool IsAsciiLetter(char value) => value >= 'a' && value <= 'z' || value >= 'A' && value <= 'Z';
    }
}
