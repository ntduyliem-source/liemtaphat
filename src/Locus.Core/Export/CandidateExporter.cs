using System;
using System.Text.RegularExpressions;
using System.Xml;

namespace Locus.Core.Export
{
    /// <summary>All projections identify the same immutable candidate; no projection reparses source.</summary>
    public sealed class CandidateExports
    {
        public string CandidateId { get; }
        public string SourceId { get; }
        public string MathMl { get; }
        public string Omml { get; }
        public string Latex { get; }
        public string OriginalText { get; }
        public string OriginalContent { get; }

        internal CandidateExports(Candidate candidate, string mathMl, string omml, string latex,
            string originalText, string originalContent)
        {
            CandidateId = candidate.Id;
            SourceId = candidate.Source.Id;
            MathMl = mathMl;
            Omml = omml;
            Latex = latex;
            OriginalText = originalText;
            OriginalContent = originalContent;
        }
    }

    public static class CandidateExporter
    {
        public const string MathMlNamespace = "http://www.w3.org/1998/Math/MathML";
        public const string OmmlNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/math";
        private const int MaxDepth = 128;
        private static readonly Regex DecimalNumber = new Regex(@"\A[0-9]+(?:\.[0-9]+)?\z", RegexOptions.CultureInvariant);

        public static CandidateExports Export(Candidate candidate)
        {
            RequireCandidate(candidate);
            return new CandidateExports(candidate, ToMathMl(candidate), ToOmml(candidate),
                ToLatex(candidate), ToOriginalText(candidate), Slice(candidate, candidate.ContentSpan));
        }

        public static string ToMathMl(Candidate candidate)
        {
            RequireCandidate(candidate);
            return "<math xmlns=\"" + MathMlNamespace + "\" display=\"inline\">" +
                Render(candidate.Document.Root, Format.MathMl, 0) + "</math>";
        }

        /// <summary>Native OMML fragment. A Word host owns packaging and the document transaction.</summary>
        public static string ToOmml(Candidate candidate)
        {
            RequireCandidate(candidate);
            return "<m:oMath xmlns:m=\"" + OmmlNamespace + "\">" +
                Render(candidate.Document.Root, Format.Omml, 0) + "</m:oMath>";
        }

        public static string ToLatex(Candidate candidate)
        {
            RequireCandidate(candidate);
            return Render(candidate.Document.Root, Format.Latex, 0);
        }

        /// <summary>Exact replacement source, including its original configured markers.</summary>
        public static string ToOriginalText(Candidate candidate)
        {
            RequireCandidate(candidate);
            return Slice(candidate, candidate.ReplacementSpan);
        }

        private static string Slice(Candidate candidate, TextSpan span)
        {
            if (span.Start < 0 || span.End < span.Start || span.End > candidate.Source.Raw.Length)
                throw new ArgumentException("Candidate source span is outside its source snapshot.", nameof(candidate));
            return candidate.Source.Raw.Substring(span.Start, span.End - span.Start);
        }

        private static void RequireCandidate(Candidate candidate)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (candidate.Source == null || candidate.Document == null || candidate.Document.Root == null)
                throw new ArgumentException("Candidate is missing its source or mathematical document.", nameof(candidate));
        }

        private enum Format { MathMl, Omml, Latex }

        private static string Render(MathNode node, Format format, int depth)
        {
            if (node == null || depth > MaxDepth) throw new ArgumentException("Invalid or excessively deep mathematical tree.");
            if (Locus.Core.Domains.ScientificNodes.IsExtended(node.Type)) return RenderScientific(node, format, depth);
            switch (node.Type)
            {
                case "Number":
                    Arity(node, 0);
                    if (node.Value == null || !DecimalNumber.IsMatch(node.Value))
                        throw new ArgumentException("M1 numbers must be unsigned canonical decimal strings.");
                    return Token(node.Value, format, true);
                case "Symbol":
                    Arity(node, 0);
                    if (node.Name == null || node.Name.Length != 1 ||
                        !((node.Name[0] >= 'a' && node.Name[0] <= 'z') || (node.Name[0] >= 'A' && node.Name[0] <= 'Z')))
                        throw new ArgumentException("M1 symbols must be one ASCII Latin letter.");
                    return Token(node.Name, format, false);
                case "Unary":
                    Arity(node, 1);
                    if (node.Operator != "plus" && node.Operator != "minus") throw new ArgumentException("Unknown unary operator.");
                    var operand = Child(node.Children[0], format, depth, Precedence(node.Children[0]) < 3 || node.Children[0].Type == "Unary");
                    return Row(Operator(node.Operator == "plus" ? "+" : "-", format) + operand, format);
                case "Binary":
                    Arity(node, 2);
                    if (node.Operator == "divide")
                        return Fraction(Render(node.Children[0], format, depth + 1), Render(node.Children[1], format, depth + 1), format);
                    if (node.Operator != "add" && node.Operator != "subtract" && node.Operator != "multiply")
                        throw new ArgumentException("Unknown binary operator.");
                    int precedence = Precedence(node);
                    bool leftGroup = Precedence(node.Children[0]) < precedence;
                    bool rightGroup = Precedence(node.Children[1]) < precedence ||
                        (Precedence(node.Children[1]) == precedence && !IsFraction(node.Children[1])) || node.Children[1].Type == "Unary";
                    string op = node.Operator == "add" ? "+" : node.Operator == "subtract" ? "-" : "multiply";
                    return Row(Child(node.Children[0], format, depth, leftGroup) + Operator(op, format) +
                        Child(node.Children[1], format, depth, rightGroup), format);
                case "Power":
                    Arity(node, 2);
                    return Power(Child(node.Children[0], format, depth, Precedence(node.Children[0]) <= 4),
                        Render(node.Children[1], format, depth + 1), format);
                case "Sqrt":
                    Arity(node, 1);
                    return Root(Render(node.Children[0], format, depth + 1), format);
                case "Relation":
                    Arity(node, 2);
                    string relation;
                    switch (node.Operator)
                    {
                        case "eq": relation = "="; break;
                        case "lt": relation = "<"; break;
                        case "gt": relation = ">"; break;
                        case "le": relation = "le"; break;
                        case "ge": relation = "ge"; break;
                        default: throw new ArgumentException("Unknown relation operator.");
                    }
                    return Row(Child(node.Children[0], format, depth, node.Children[0].Type == "Relation") +
                        Operator(relation, format) + Child(node.Children[1], format, depth, node.Children[1].Type == "Relation"), format);
                default: throw new ArgumentException("Unsupported mathematical node type: " + node.Type);
            }
        }

        private static bool IsFraction(MathNode node) => node.Type == "Binary" && node.Operator == "divide";
        private static string RenderScientific(MathNode node, Format format, int depth)
        {
            string At(int index) => Render(node.Children[index], format, depth + 1);
            switch (node.Type)
            {
                case "ChemElement": case "Unit": return Upright(node.Name!, format);
                case "Greek": return format == Format.Latex ? GreekLatex(node.Name!) : Token(node.Name!, format, false);
                case "ChemConcat": return Row(At(0) + At(1), format);
                case "ChemGroup":
                    if (node.Name == "()") return Group(At(0), format);
                    return format == Format.Latex ? @"\left[" + At(0) + @"\right]" : format == Format.MathMl ? "<mrow><mo>[</mo>" + At(0) + "<mo>]</mo></mrow>" : "<m:d><m:dPr><m:begChr m:val=\"[\"/><m:endChr m:val=\"]\"/></m:dPr><m:e>" + At(0) + "</m:e></m:d>";
                case "ChemSubscript": case "Subscript": return Subscript(At(0), At(1), format);
                case "ChemCharge": return Power(At(0), Upright(node.Value!, format), format);
                case "ChemCoefficient": return Row(At(0) + Space(format) + At(1), format);
                case "ChemSum": return Row(At(0) + Operator("+", format) + At(1), format);
                case "ChemReaction":
                    var arrow = node.Operator == "arrow" ? "→" : "⇌";
                    return Row(At(0) + (format == Format.Latex ? node.Operator == "arrow" ? @" \rightarrow " : @" \rightleftharpoons " : Operator(arrow, format)) + At(1), format);
                case "Quantity": return Row(At(0) + Space(format) + At(1), format);
                case "Vector":
                    return format == Format.Latex ? @"\vec{" + At(0) + "}" : format == Format.MathMl ? "<mover accent=\"true\">" + Row(At(0), format) + "<mo>→</mo></mover>" : "<m:acc><m:accPr><m:chr m:val=\"⃗\"/></m:accPr><m:e>" + At(0) + "</m:e></m:acc>";
                default: throw new ArgumentException("Unsupported scientific node.");
            }
        }
        private static string Subscript(string basis, string index, Format format) => format == Format.Latex ? "{" + basis + "}_{" + index + "}" :
            format == Format.MathMl ? "<msub>" + Row(basis, format) + Row(index, format) + "</msub>" : "<m:sSub><m:e>" + basis + "</m:e><m:sub>" + index + "</m:sub></m:sSub>";
        private static string Space(Format format) => format == Format.Latex ? @"\," : format == Format.MathMl ? "<mspace width=\"0.1667em\"/>" : "<m:r><m:t xml:space=\"preserve\"> </m:t></m:r>";
        private static string Upright(string text, Format format) => format == Format.Latex ? @"\mathrm{" + text.Replace("μ", @"\mu ").Replace("µ", @"\mu ").Replace("Ω", @"\Omega ").Replace("°", @"{}^{\circ}") + "}" :
            format == Format.MathMl ? "<mi mathvariant=\"normal\">" + Escape(text) + "</mi>" : "<m:r><m:rPr><m:sty m:val=\"p\"/></m:rPr><m:t>" + Escape(text) + "</m:t></m:r>";
        private static string GreekLatex(string glyph)
        {
            foreach (var pair in Locus.Core.Domains.ScientificSymbols.GreekAliases)
                if (pair.Value == glyph) return "\\" + (pair.Key == "micro" ? "mu" : pair.Key) + " ";
            return glyph;
        }
        private static int Precedence(MathNode node)
        {
            if (node.Type == "Relation") return 0;
            if (node.Type == "Binary") return node.Operator == "add" || node.Operator == "subtract" ? 1 : 2;
            if (node.Type == "Unary") return 3;
            if (node.Type == "Power") return 4;
            return 5;
        }

        private static void Arity(MathNode node, int count)
        {
            if (node.Children == null || node.Children.Count != count) throw new ArgumentException("Invalid arity for " + node.Type + ".");
        }

        private static string Child(MathNode node, Format format, int depth, bool grouped)
        {
            string value = Render(node, format, depth + 1);
            return grouped ? Group(value, format) : value;
        }

        private static string Escape(string text)
        {
            XmlConvert.VerifyXmlChars(text);
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string Token(string text, Format format, bool number)
        {
            if (format == Format.Latex) return text;
            if (format == Format.Omml) return "<m:r><m:t>" + Escape(text) + "</m:t></m:r>";
            string tag = number ? "mn" : "mi";
            return "<" + tag + ">" + Escape(text) + "</" + tag + ">";
        }

        private static string Operator(string text, Format format)
        {
            if (format == Format.Latex)
                return text == "multiply" ? @" \cdot " : text == "le" ? @" \le " : text == "ge" ? @" \ge " : text;
            string glyph = text == "multiply" ? "\u00b7" : text == "le" ? "\u2264" : text == "ge" ? "\u2265" : text == "-" ? "\u2212" : text;
            return format == Format.MathMl ? "<mo>" + Escape(glyph) + "</mo>" : Token(glyph, format, false);
        }

        private static string Row(string text, Format format) => format == Format.MathMl ? "<mrow>" + text + "</mrow>" : text;
        private static string Group(string text, Format format)
        {
            if (format == Format.Latex) return @"\left(" + text + @"\right)";
            if (format == Format.MathMl) return "<mrow><mo>(</mo>" + text + "<mo>)</mo></mrow>";
            return "<m:d><m:dPr><m:begChr m:val=\"(\"/><m:endChr m:val=\")\"/></m:dPr><m:e>" + text + "</m:e></m:d>";
        }

        private static string Fraction(string numerator, string denominator, Format format)
        {
            if (format == Format.Latex) return @"\frac{" + numerator + "}{" + denominator + "}";
            if (format == Format.MathMl) return "<mfrac>" + Row(numerator, format) + Row(denominator, format) + "</mfrac>";
            return "<m:f><m:num>" + numerator + "</m:num><m:den>" + denominator + "</m:den></m:f>";
        }

        private static string Power(string basis, string exponent, Format format)
        {
            if (format == Format.Latex) return "{" + basis + "}^{" + exponent + "}";
            if (format == Format.MathMl) return "<msup>" + Row(basis, format) + Row(exponent, format) + "</msup>";
            return "<m:sSup><m:e>" + basis + "</m:e><m:sup>" + exponent + "</m:sup></m:sSup>";
        }

        private static string Root(string radicand, Format format)
        {
            if (format == Format.Latex) return @"\sqrt{" + radicand + "}";
            if (format == Format.MathMl) return "<msqrt>" + radicand + "</msqrt>";
            return "<m:rad><m:radPr><m:degHide m:val=\"1\"/></m:radPr><m:deg/><m:e>" + radicand + "</m:e></m:rad>";
        }
    }
}
