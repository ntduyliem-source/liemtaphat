using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using Locus.Core.Parsing;

namespace Locus.Core.Plotting
{
    /// <summary>Versioned, real-valued y=f(x) grammar. No script evaluation or implicit typo repair.</summary>
    public sealed class PlotExpression
    {
        public const string Grammar = "locus-plot/1";
        public const int MaxLength = 32768;
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, PlotExpression> Cache = new Dictionary<string, PlotExpression>(StringComparer.Ordinal);
        private static readonly Queue<string> CacheOrder = new Queue<string>();
        public string Raw { get; }
        public PlotNode Root { get; }
        public IReadOnlyList<string> Parameters { get; }
        public string Snapshot => Grammar + ":" + Root.Snapshot;
        public string MathMl => "<math xmlns=\"http://www.w3.org/1998/Math/MathML\"><mrow><mi>y</mi><mo>=</mo>" + Root.MathMl + "</mrow></math>";
        public string Latex => "y=" + Root.Latex;
        private PlotExpression(string raw, PlotNode root)
        { Raw = raw; Root = root; Parameters = root.Symbols().Where(n => n != "x" && n != "pi" && n != "e").Distinct(StringComparer.Ordinal).ToArray(); }
        public static PlotExpression Parse(string raw, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (raw == null || raw.Length > MaxLength) throw new PlotParseException("Hàm vượt giới hạn 32768 ký tự.", 0, raw?.Length ?? 0);
            lock (CacheLock) { if (Cache.TryGetValue(raw, out var cached)) return cached; }
            _ = new SourceSnapshot(raw);
            var parsed = new PlotExpression(raw, new Reader(raw, token).Read());
            lock (CacheLock)
            {
                if (!Cache.ContainsKey(raw))
                {
                    while (Cache.Count >= 32) Cache.Remove(CacheOrder.Dequeue());
                    Cache.Add(raw, parsed); CacheOrder.Enqueue(raw);
                }
            }
            return parsed;
        }
        public PlotNode Bind(IReadOnlyDictionary<string, double> values) => Root.Bind(values);

        private sealed class Token
        {
            internal string Kind, Text; internal int Start, End;
            internal Token(string kind, string text, int start, int end) { Kind = kind; Text = text; Start = start; End = end; }
        }
        private sealed class Reader
        {
            private readonly string raw; private readonly CancellationToken cancellation;
            private readonly List<Token> tokens = new List<Token>(); private int cursor, depth, nodes;
            private Token Current => tokens[cursor];
            internal Reader(string raw, CancellationToken token) { this.raw = raw; cancellation = token; Lex(); }
            private void Lex()
            {
                int p = 0;
                while (p < raw.Length)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (char.IsWhiteSpace(raw[p])) { p++; continue; }
                    int start = p; char ch = raw[p]; string kind, text;
                    if (ch >= '0' && ch <= '9' || ch == '.' && p + 1 < raw.Length && char.IsDigit(raw[p + 1]))
                    {
                        p++; while (p < raw.Length && (char.IsDigit(raw[p]) || raw[p] == '.' || raw[p] == ',')) p++;
                        text = raw.Substring(start, p - start).Replace(',', '.');
                        if (!double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double number) || double.IsInfinity(number)) Fail("Số không hợp lệ.", start, p);
                        kind = "number";
                    }
                    else if (FormulaLexicon.IsWordCharacter(ch))
                    {
                        p++; while (p < raw.Length && FormulaLexicon.IsWordCharacter(raw[p])) p++;
                        text = raw.Substring(start, p - start).Normalize(NormalizationForm.FormC);
                        if (FormulaLexicon.TryGetKeyword(text, out var keyword))
                        {
                            kind = keyword == FormulaTokenKind.Root ? "function" : keyword == FormulaTokenKind.Plus ? "+" : keyword == FormulaTokenKind.Minus ? "-" : keyword == FormulaTokenKind.Multiply ? "*" : keyword == FormulaTokenKind.Divide ? "/" : keyword == FormulaTokenKind.Power ? "^" : "=";
                            if (kind == "function") text = "sqrt";
                        }
                        else if (text == "sin" || text == "cos" || text == "tan" || text == "abs" || text == "ln" || text == "exp") kind = "function";
                        else
                        {
                            if (text == "π") text = "pi";
                            if (text != "pi" && !FormulaLexicon.IsAsciiSymbol(text)) Fail("Tên chưa hỗ trợ. Dùng một chữ hoặc chỉ số như a_100; kiểm tra tên sin, cos…", start, p);
                            kind = "symbol";
                            if (p < raw.Length && raw[p] == '_')
                            {
                                int underscore = p++; while (p < raw.Length && raw[p] >= '0' && raw[p] <= '9') p++;
                                if (p == underscore + 1 || text.Length != 1 || text == "x" || text == "y") Fail("Chỉ số tham số cần chữ và số, ví dụ a_1.", start, p);
                                text += raw.Substring(underscore, p - underscore);
                            }
                            if (text == "y") kind = "y";
                        }
                    }
                    else
                    {
                        p++; text = ch.ToString();
                        kind = ch == '−' ? "-" : ch == '×' || ch == '·' ? "*" : ch == '÷' ? "/" : text;
                        if ("+-*/^()=".IndexOf(kind, StringComparison.Ordinal) < 0) Fail("Ký tự chưa hỗ trợ trong hàm y=f(x).", start, p);
                    }
                    tokens.Add(new Token(kind, text, start, p));
                    if (tokens.Count > 4096) Fail("Hàm vượt giới hạn 4096 thành phần.", start, p);
                }
                tokens.Add(new Token("end", "", p, p));
            }
            internal PlotNode Read()
            {
                if (Current.Kind == "y" && tokens.Count > 1 && tokens[1].Kind == "=") cursor = 2;
                var value = Expression(0);
                if (Current.Kind != "end") Fail(Current.Kind == "=" || Current.Kind == "y" ? "Hiện hỗ trợ y=f(x). Phương trình ẩn, tham số và bất phương trình sẽ có công cụ riêng." : "Cần toán tử hoặc dấu ngoặc rõ ràng.", Current.Start, Current.End);
                return value;
            }
            private PlotNode Expression(int minimum)
            {
                cancellation.ThrowIfCancellationRequested();
                if (++depth > 96) Fail("Quá nhiều ngoặc hoặc lũy thừa lồng nhau.", Current.Start, Current.End);
                Token first = Current; cursor++; PlotNode left;
                if (first.Kind == "number") left = Node("number", first.Text, first.Start, first.End);
                else if (first.Kind == "symbol") left = Node("symbol", first.Text, first.Start, first.End);
                else if (first.Kind == "+" || first.Kind == "-") { var child = Expression(25); left = Node("unary", first.Kind, first.Start, child.End, child); }
                else if (first.Kind == "(") { left = Expression(0); ExpectClose(); }
                else if (first.Kind == "function")
                {
                    PlotNode child;
                    if (Current.Kind == "(") { cursor++; child = Expression(0); ExpectClose(); }
                    else if (first.Text == "sqrt") child = Expression(25);
                    else { Fail("Tên hàm cần ngoặc, ví dụ sin(x).", first.Start, first.End); throw new InvalidOperationException(); }
                    left = Node("function", first.Text, first.Start, tokens[cursor - 1].End, child);
                }
                else { Fail(first.Kind == "y" ? "y là trục phụ thuộc; hiện chỉ hỗ trợ y=f(x)." : "Cần một số, x, tham số hoặc biểu thức trong ngoặc.", first.Start, first.End); throw new InvalidOperationException(); }
                while (true)
                {
                    var op = Current; bool implicitProduct = op.Kind == "symbol" || op.Kind == "(" || op.Kind == "function";
                    int precedence = op.Kind == "+" || op.Kind == "-" ? 10 : op.Kind == "*" || op.Kind == "/" || implicitProduct ? 20 : op.Kind == "^" ? 30 : -1;
                    if (precedence < minimum) break;
                    if (implicitProduct && left.Kind == "binary" && left.Value == "/") Fail("Phạm vi mẫu số chưa rõ. Viết (a/b)*x hoặc a/(b*x).", op.Start, op.End);
                    if (!implicitProduct) cursor++;
                    var right = Expression(precedence + (op.Kind == "^" ? 0 : 1));
                    left = Node("binary", implicitProduct ? "*" : op.Kind, left.Start, right.End, left, right);
                }
                depth--; return left;
            }
            private PlotNode Node(string kind, string value, int start, int end, params PlotNode[] children)
            { if (++nodes > 4096) Fail("Hàm quá lớn.", start, end); return new PlotNode(kind, value, start, end, children); }
            private void ExpectClose() { if (Current.Kind != ")") Fail("Thiếu ngoặc đóng; nguồn được giữ để bạn sửa.", Current.Start, Current.End); cursor++; }
            private static void Fail(string message, int start, int end) => throw new PlotParseException(message, start, end);
        }
    }

    public sealed class PlotParseException : FormatException
    {
        public int Start { get; } public int End { get; }
        public PlotParseException(string message, int start, int end) : base(message) { Start = start; End = end; }
    }
    public sealed class PlotNode
    {
        public string Kind { get; } public string Value { get; } public int Start { get; } public int End { get; }
        public IReadOnlyList<PlotNode> Children { get; }
        internal int Depth { get; }
        internal bool Oscillatory { get; }
        private readonly double number;
        internal PlotNode(string kind, string value, int start, int end, params PlotNode[] children)
        {
            Depth = children.Length == 0 ? 1 : children.Max(c => c.Depth) + 1;
            if (Depth > 512) throw new PlotParseException("Cấu trúc quá sâu; chia thành các nhóm nhỏ trong ngoặc.", start, end);
            Kind = kind; Value = value; Start = start; End = end; Children = Array.AsReadOnly(children);
            Oscillatory = kind == "function" && (value == "sin" || value == "cos" || value == "tan") || children.Any(c => c.Oscillatory);
            if (kind == "number") number = double.Parse(value, CultureInfo.InvariantCulture);
        }
        public string Snapshot => Kind + "[" + Value + "@" + Start + ":" + End + "](" + string.Join(",", Children.Select(c => c.Snapshot)) + ")";
        internal IEnumerable<string> Symbols() { if (Kind == "symbol") yield return Value; foreach (var child in Children) foreach (string s in child.Symbols()) yield return s; }
        public PlotNode Bind(IReadOnlyDictionary<string, double> values)
        {
            if (Kind == "symbol" && Value != "x")
            {
                double value = Value == "pi" ? Math.PI : Value == "e" ? Math.E : values.TryGetValue(Value, out double supplied) ? supplied : throw new ArgumentException("Thiếu giá trị: " + Value);
                if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Giá trị tham số cần hữu hạn.");
                return new PlotNode("number", value.ToString("R", CultureInfo.InvariantCulture), Start, End);
            }
            var bound = new PlotNode(Kind, Value, Start, End, Children.Select(c => c.Bind(values)).ToArray());
            if (Kind != "number" && Kind != "symbol" && bound.Children.All(c => c.Kind == "number"))
                return new PlotNode("number", bound.Evaluate(0).ToString("R", CultureInfo.InvariantCulture), Start, End);
            return bound;
        }
        public double Evaluate(double x)
        {
            if (Kind == "number") return number;
            if (Kind == "symbol") return Value == "x" ? x : double.NaN;
            double a = Children[0].Evaluate(x);
            if (Kind == "unary") return Value == "-" ? -a : a;
            if (Kind == "function") return Value == "sqrt" ? Math.Sqrt(a) : Value == "sin" ? Math.Sin(a) : Value == "cos" ? Math.Cos(a) : Value == "tan" ? Math.Tan(a) : Value == "abs" ? Math.Abs(a) : Value == "ln" ? Math.Log(a) : Math.Exp(a);
            double b = Children[1].Evaluate(x);
            return Value == "+" ? a + b : Value == "-" ? a - b : Value == "*" ? a * b : Value == "/" ? a / b : a == 0 && b == 0 ? double.NaN : Math.Pow(a, b);
        }
        private static string E(string value) => SecurityElement.Escape(value) ?? "";
        private int Precedence => Kind == "unary" ? 25 : Kind != "binary" ? 100 : Value == "+" || Value == "-" ? 10 : Value == "^" ? 30 : 20;
        private string MathMlChild(int index, bool equalNeedsParentheses = false)
        {
            var child = Children[index]; string text = child.MathMl;
            return child.Precedence < Precedence || equalNeedsParentheses && child.Precedence == Precedence ? "<mrow><mo>(</mo>" + text + "<mo>)</mo></mrow>" : text;
        }
        public string MathMl
        {
            get
            {
                if (Kind == "number") return "<mn>" + E(Value) + "</mn>";
                if (Kind == "symbol") { var parts = Value.Split('_'); return parts.Length == 2 ? "<msub><mi>" + E(parts[0]) + "</mi><mn>" + E(parts[1]) + "</mn></msub>" : "<mi>" + E(Value == "pi" ? "π" : Value) + "</mi>"; }
                if (Kind == "unary") return "<mrow><mo>" + Value + "</mo>" + MathMlChild(0) + "</mrow>";
                if (Kind == "function") return Value == "sqrt" ? "<msqrt>" + Children[0].MathMl + "</msqrt>" : "<mrow><mi>" + Value + "</mi><mo>(</mo>" + Children[0].MathMl + "<mo>)</mo></mrow>";
                if (Value == "/") return "<mfrac>" + Children[0].MathMl + Children[1].MathMl + "</mfrac>";
                if (Value == "^") return "<msup>" + MathMlChild(0, true) + Children[1].MathMl + "</msup>";
                return "<mrow>" + MathMlChild(0) + "<mo>" + (Value == "*" ? "·" : Value) + "</mo>" + MathMlChild(1, Value == "-") + "</mrow>";
            }
        }
        public string Latex
        {
            get
            {
                if (Kind == "number") return Value;
                if (Kind == "symbol") return Value == "pi" ? "\\pi" : Value.Contains("_") ? Value.Split('_')[0] + "_{" + Value.Split('_')[1] + "}" : Value;
                string a = Children[0].Latex;
                if (Kind == "unary") return Value + "(" + a + ")";
                if (Kind == "function") return Value == "sqrt" ? "\\sqrt{" + a + "}" : "\\operatorname{" + Value + "}(" + a + ")";
                string b = Children[1].Latex;
                return Value == "/" ? "\\frac{" + a + "}{" + b + "}" : Value == "^" ? "(" + a + ")^{" + b + "}" : "(" + a + (Value == "*" ? "\\cdot " : Value) + b + ")";
            }
        }
    }
}
