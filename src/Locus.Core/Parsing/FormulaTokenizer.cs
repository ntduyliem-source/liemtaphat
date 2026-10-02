using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Locus.Core.Parsing
{
    public sealed class FormulaToken
    {
        public FormulaTokenKind Kind { get; }
        public string Text { get; }
        public string NormalizedText { get; }
        public TextSpan Span { get; }
        public bool IsAlias { get; }
        public bool HasWhitespaceBefore { get; }
        internal MathNode? EmbeddedNode { get; }

        internal FormulaToken(FormulaTokenKind kind, string text, string normalizedText, TextSpan span, bool isAlias, bool hasWhitespaceBefore, MathNode? embeddedNode = null)
        {
            Kind = kind;
            Text = text;
            NormalizedText = normalizedText;
            Span = span;
            IsAlias = isAlias;
            HasWhitespaceBefore = hasWhitespaceBefore;
            EmbeddedNode = embeddedNode;
        }
    }

    public sealed class TokenizationResult
    {
        public IReadOnlyList<FormulaToken> Tokens { get; }
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        internal TokenizationResult(List<FormulaToken> tokens, List<Diagnostic> diagnostics)
        {
            Tokens = new ReadOnlyCollection<FormulaToken>(tokens);
            Diagnostics = new ReadOnlyCollection<Diagnostic>(diagnostics);
        }
    }

    /// <summary>Tokenizes raw UTF-16; NFC is applied only to token text, never to offsets.</summary>
    public sealed class FormulaTokenizer
    {
        public TokenizationResult Tokenize(SourceSnapshot source, TextSpan contentSpan, CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateSpan(source.Raw, contentSpan, nameof(contentSpan));
            cancellationToken.ThrowIfCancellationRequested();
            var raw = source.Raw;
            var tokens = new List<FormulaToken>();
            var diagnostics = new List<Diagnostic>();
            if (contentSpan.Length > FormulaParser.MaximumRegionLength)
            {
                diagnostics.Add(new Diagnostic("INPUT_TOO_LONG", "error", contentSpan, "Vùng token hóa vượt giới hạn 4096 UTF-16 code unit."));
                tokens.Add(new FormulaToken(FormulaTokenKind.End, string.Empty, string.Empty, new TextSpan(contentSpan.End, contentSpan.End), false, false));
                return new TokenizationResult(tokens, diagnostics);
            }
            var offset = contentSpan.Start;
            var whitespace = false;
            while (offset < contentSpan.End)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var start = offset;
                var ch = raw[offset];
                if (ch == '\r' || ch == '\n')
                {
                    AddError(diagnostics, "UNSUPPORTED_MULTILINE_EXPRESSION", contentSpan, "Biểu thức nhiều dòng chưa thuộc grammar v0.");
                    break;
                }
                if (char.IsWhiteSpace(ch))
                {
                    whitespace = true;
                    offset++;
                    continue;
                }
                if (FormulaLexicon.IsAsciiDigit(ch))
                {
                    var separators = 0;
                    var hasComma = false;
                    var hasDot = false;
                    while (offset < contentSpan.End)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (FormulaLexicon.IsAsciiDigit(raw[offset])) { offset++; continue; }
                        if ((raw[offset] == '.' || raw[offset] == ',') && offset + 1 < contentSpan.End && FormulaLexicon.IsAsciiDigit(raw[offset + 1]))
                        {
                            separators++;
                            hasComma |= raw[offset] == ',';
                            hasDot |= raw[offset] == '.';
                            offset++;
                            continue;
                        }
                        break;
                    }
                    if (separators > 1)
                    {
                        AddError(diagnostics, hasComma && hasDot ? "MIXED_DECIMAL_SEPARATORS" : "INVALID_NUMBER", contentSpan, "Không hỗ trợ nhiều dấu phân cách trong một số.");
                        break;
                    }
                    // Do not reinterpret unsupported scientific notation as an
                    // implicit product involving variable e/E followed by a sum.
                    // An explicit product such as 2*e-3 remains ordinary algebra.
                    if (LooksLikeScientificSuffix(raw, offset, contentSpan.End))
                    {
                        AddError(diagnostics, "UNSUPPORTED_SCIENTIFIC_NOTATION", contentSpan, "Số mũ khoa học chưa thuộc grammar v0; dùng phép toán tường minh nếu đây là biểu thức đại số.");
                        break;
                    }
                    var literal = raw.Substring(start, offset - start);
                    tokens.Add(new FormulaToken(FormulaTokenKind.Number, literal, CanonicalNumber(literal), new TextSpan(start, offset), false, whitespace));
                    whitespace = false;
                    continue;
                }
                if (FormulaLexicon.IsWordCharacter(ch))
                {
                    while (offset < contentSpan.End && FormulaLexicon.IsWordCharacter(raw[offset]))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        offset++;
                    }
                    var literal = raw.Substring(start, offset - start);
                    var normalized = literal.Normalize(NormalizationForm.FormC);
                    FormulaTokenKind kind;
                    var keyword = FormulaLexicon.TryGetKeyword(normalized, out kind);
                    var followedByDigits = offset < contentSpan.End && FormulaLexicon.IsAsciiDigit(raw[offset]);
                    if (followedByDigits && (!keyword || kind != FormulaTokenKind.Root))
                    {
                        while (offset < contentSpan.End && (FormulaLexicon.IsWordCharacter(raw[offset]) || FormulaLexicon.IsAsciiDigit(raw[offset]))) offset++;
                        AddError(diagnostics, "UNSUPPORTED_IDENTIFIER", contentSpan, "Tên biến ghép chữ và số chưa thuộc grammar v0.");
                        break;
                    }
                    if (!keyword)
                    {
                        if (FormulaLexicon.IsAsciiSymbol(normalized)) kind = FormulaTokenKind.Symbol;
                        else
                        {
                            var next = offset;
                            while (next < contentSpan.End && char.IsWhiteSpace(raw[next]) && raw[next] != '\r' && raw[next] != '\n') next++;
                            var code = next < contentSpan.End && raw[next] == '(' ? "UNSUPPORTED_FUNCTION" :
                                IsNonAsciiSingleSymbol(normalized) ? "UNSUPPORTED_SYMBOL" : "UNSUPPORTED_IDENTIFIER";
                            AddError(diagnostics, code, contentSpan, "Từ hoặc ký hiệu chưa thuộc grammar v0.");
                            break;
                        }
                    }
                    tokens.Add(new FormulaToken(kind, literal, normalized, new TextSpan(start, offset), keyword, whitespace));
                    whitespace = false;
                    continue;
                }
                FormulaTokenKind punctuation;
                var width = 1;
                switch (ch)
                {
                    case '+': punctuation = FormulaTokenKind.Plus; break;
                    case '-': case '\u2212': punctuation = FormulaTokenKind.Minus; break;
                    case '*': case '×': case '·': punctuation = FormulaTokenKind.Multiply; break;
                    case '/': case '÷': punctuation = FormulaTokenKind.Divide; break;
                    case '^': punctuation = FormulaTokenKind.Power; break;
                    case '√': punctuation = FormulaTokenKind.Root; break;
                    case '=': punctuation = FormulaTokenKind.Equal; break;
                    case '<':
                        punctuation = offset + 1 < contentSpan.End && raw[offset + 1] == '=' ? FormulaTokenKind.LessEqual : FormulaTokenKind.Less;
                        if (punctuation == FormulaTokenKind.LessEqual) width = 2;
                        break;
                    case '>':
                        punctuation = offset + 1 < contentSpan.End && raw[offset + 1] == '=' ? FormulaTokenKind.GreaterEqual : FormulaTokenKind.Greater;
                        if (punctuation == FormulaTokenKind.GreaterEqual) width = 2;
                        break;
                    case '≤': punctuation = FormulaTokenKind.LessEqual; break;
                    case '≥': punctuation = FormulaTokenKind.GreaterEqual; break;
                    case '(': punctuation = FormulaTokenKind.LeftParen; break;
                    case ')': punctuation = FormulaTokenKind.RightParen; break;
                    default:
                        var category = char.GetUnicodeCategory(ch);
                        var code = ch == '_' ? "UNSUPPORTED_SUBSCRIPT" : ch == ',' ? "UNSUPPORTED_LIST" : ch == '.' ? "INVALID_NUMBER" :
                            category == UnicodeCategory.Format ? "UNSUPPORTED_INVISIBLE_CHARACTER" : "UNSUPPORTED_SYMBOL";
                        AddError(diagnostics, code, contentSpan, "Ký tự không được tự xóa hoặc diễn giải ngoài grammar v0.");
                        punctuation = FormulaTokenKind.Unknown;
                        break;
                }
                if (punctuation == FormulaTokenKind.Unknown) break;
                offset += width;
                tokens.Add(new FormulaToken(punctuation, raw.Substring(start, width), raw.Substring(start, width), new TextSpan(start, offset), false, whitespace));
                whitespace = false;
            }
            tokens.Add(new FormulaToken(FormulaTokenKind.End, string.Empty, string.Empty, new TextSpan(contentSpan.End, contentSpan.End), false, whitespace));
            return new TokenizationResult(tokens, diagnostics);
        }

        internal static void ValidateSpan(string raw, TextSpan span, string parameter)
        {
            if (span.Start < 0 || span.End < span.Start || span.End > raw.Length) throw new ArgumentOutOfRangeException(parameter);
            if (!IsScalarBoundary(raw, span.Start) || !IsScalarBoundary(raw, span.End))
                throw new ArgumentException("A source span must not split a UTF-16 surrogate pair.", parameter);
        }

        private static bool IsScalarBoundary(string raw, int index) => index == 0 || index == raw.Length ||
            !(char.IsHighSurrogate(raw[index - 1]) && char.IsLowSurrogate(raw[index]));

        private static bool IsNonAsciiSingleSymbol(string normalized) => normalized.Length == 1 && !FormulaLexicon.IsAsciiSymbol(normalized);

        private static bool LooksLikeScientificSuffix(string raw, int offset, int end)
        {
            if (offset >= end || (raw[offset] != 'e' && raw[offset] != 'E')) return false;
            offset++;
            if (offset < end && (raw[offset] == '+' || raw[offset] == '-' || raw[offset] == '\u2212')) offset++;
            return offset < end && FormulaLexicon.IsAsciiDigit(raw[offset]);
        }

        private static string CanonicalNumber(string literal)
        {
            var number = literal.Replace(',', '.');
            var index = 0;
            while (index + 1 < number.Length && number[index] == '0' && number[index + 1] != '.') index++;
            return number.Substring(index);
        }

        private static void AddError(List<Diagnostic> diagnostics, string code, TextSpan span, string message)
        {
            diagnostics.Add(new Diagnostic(code, "error", span, message));
        }
    }
}
