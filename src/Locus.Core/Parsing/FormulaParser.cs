using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Locus.Core.Parsing
{
    /// <summary>
    /// General parser for the versioned v0 grammar. Input is an explicitly identified
    /// region; detection and all document-write permissions belong to other components.
    /// </summary>
    public sealed class FormulaParser
    {
        private readonly bool physics;
        public FormulaParser() { }
        internal FormulaParser(bool physics) { this.physics = physics; }
        public const int MaximumRegionLength = 4096;
        public const int MaximumDepth = 64;

        public CandidateSet Parse(SourceSnapshot source, TextSpan contentSpan, TextSpan? replacementSpan = null,
            CancellationToken cancellationToken = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var replacement = replacementSpan ?? contentSpan;
            source.Validate(contentSpan);
            source.Validate(replacement);
            if (!replacement.Contains(contentSpan)) throw new ArgumentException("Replacement must contain content.", nameof(replacementSpan));
            cancellationToken.ThrowIfCancellationRequested();
            if (contentSpan.Length > MaximumRegionLength)
                return Rejected(source, contentSpan, replacement, "INPUT_TOO_LONG", contentSpan, "Vùng công thức vượt giới hạn 4096 UTF-16 code unit.");

            var tokenized = physics ? new PhysicsTokenizer().Tokenize(source, contentSpan, cancellationToken) : new FormulaTokenizer().Tokenize(source, contentSpan, cancellationToken);
            if (tokenized.Diagnostics.Count != 0)
                return new CandidateSet(source, contentSpan, replacement, Array.Empty<Candidate>(), tokenized.Diagnostics);
            try
            {
                var state = new ParserState(tokenized.Tokens, contentSpan, false, cancellationToken);
                var expression = state.ParseDocument();
                if (state.AmbiguityCount > 1)
                    return Rejected(source, contentSpan, replacement, "AMBIGUITY_LIMIT", contentSpan, "Có nhiều vị trí chia–nhân ngầm; hãy thêm ngoặc để xác định phạm vi.");

                if (state.MissingClose)
                {
                    var missing = new TextSpan(contentSpan.End, contentSpan.End);
                    if (state.AmbiguityCount != 0)
                        return new CandidateSet(source, contentSpan, replacement, Array.Empty<Candidate>(), new[]
                        {
                            new Diagnostic("MISSING_CLOSE_PAREN", "error", missing),
                            new Diagnostic("AMBIGUOUS_INCOMPLETE_EXPRESSION", "warning", contentSpan)
                        });
                    var repair = MakeCandidate("repair", expression, source, contentSpan, replacement,
                        edits: new[] { new SourceEdit(missing, ")") }, provenance: state.MissingCloseInRoot ? "repair/missing-root-close" : "repair/missing-close/ct4-1");
                    return new CandidateSet(source, contentSpan, replacement, new[] { repair },
                        new[] { new Diagnostic("MISSING_CLOSE_PAREN", "error", missing, state.MissingCloseInRoot ? "Thiếu đúng một ngoặc đóng ở cuối lời gọi căn." : "Thiếu đúng một ngoặc đóng ở cuối biểu thức.") });
                }

                var candidates = new List<Candidate>
                {
                    MakeCandidate("direct", expression, source, contentSpan, replacement, provenance: "grammar/direct")
                };
                if (state.AmbiguityCount == 1)
                {
                    var alternativeState = new ParserState(tokenized.Tokens, contentSpan, true, cancellationToken);
                    var alternative = alternativeState.ParseDocument();
                    candidates.Add(MakeCandidate("interpretation", alternative, source, contentSpan, replacement,
                        provenance: "grammar/implicit-product-in-denominator"));
                    return new CandidateSet(source, contentSpan, replacement, candidates,
                        new[] { new Diagnostic("AMBIGUOUS_IMPLICIT_DIVISION", "warning", contentSpan, "Phép chia nối tích ngầm có hai phạm vi hợp lệ trong grammar này.") });
                }

                var proposals = new List<RepairProposal>();
                FindScopeRepairs(expression, expression, proposals, cancellationToken);
                foreach (var proposal in proposals)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var proposed = MakeCandidate("repair", proposal.Expression, source, contentSpan, replacement,
                        new[] { new Diagnostic(proposal.Code, "info", contentSpan, proposal.Message) }, proposal.Edits, proposal.Provenance);
                    if (!candidates.Any(c => Export.CandidateExporter.ToLatex(c) == Export.CandidateExporter.ToLatex(proposed))) candidates.Add(proposed);
                }
                return new CandidateSet(source, contentSpan, replacement, candidates);
            }
            catch (ParseFailure failure)
            {
                return Rejected(source, contentSpan, replacement, failure.Code, failure.Span, failure.Message);
            }
        }

        private Candidate MakeCandidate(string kind, Expression expression, SourceSnapshot source, TextSpan contentSpan,
            TextSpan replacement, IEnumerable<Diagnostic>? diagnostics = null, IEnumerable<SourceEdit>? edits = null, string? provenance = null)
            => new Candidate(kind, physics ? (Locus.Core.Domains.ScientificDocument)new Locus.Core.Domains.PhysicsDocument(expression.Node) : new MathDocument(expression.Node), source, contentSpan, replacement, diagnostics, edits, provenance);

        private static CandidateSet Rejected(SourceSnapshot source, TextSpan contentSpan, TextSpan replacement,
            string code, TextSpan diagnosticSpan, string message)
            => new CandidateSet(source, contentSpan, replacement, Array.Empty<Candidate>(), new[] { new Diagnostic(code, "error", diagnosticSpan, message) });

        private static void FindScopeRepairs(Expression whole, Expression current, List<RepairProposal> proposals, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            // The budget is a maximum, not a target. Each proposal is one independent,
            // declared edit; multiple repairs are never silently combined.
            if (proposals.Count >= 2 || current.Grouped) return;
            if (current.Node.Type == "Binary" && (current.Node.Operator == "add" || current.Node.Operator == "subtract"))
            {
                var left = current.Parts[0];
                var right = current.Parts[1];
                if (ScopeOperand(left) && !right.Grouped &&
                    right.Node.Type == "Binary" && right.Node.Operator == "divide" &&
                    right.Parts.All(ScopeOperand))
                {
                    bool legacy = current.Node.Operator == "add" && left.Node.Type == "Symbol" && right.Parts.All(p => p.Node.Type == "Number");
                    var numerator = MakeExpression("Binary", new[] { left, right.Parts[0] },
                        new TextSpan(left.Span.Start, right.Parts[0].Span.End), current.Node.Operator, operatorSpan: current.OperatorSpan);
                    var rewritten = MakeExpression("Binary", new[] { numerator, right.Parts[1] }, current.Span,
                        "divide", operatorSpan: right.OperatorSpan);
                    proposals.Add(new RepairProposal(Replace(whole, current, rewritten), new[]
                    {
                        new SourceEdit(new TextSpan(left.Span.Start, left.Span.Start), "("),
                        new SourceEdit(new TextSpan(right.Parts[0].Span.End, right.Parts[0].Span.End), ")")
                    }, "SUGGEST_FRACTION_SCOPE", legacy ? "Có thể thêm ngoặc để đưa tổng vào tử số." : "Có thể thêm ngoặc để đưa toàn biểu thức vào tử số.", "repair/fraction-numerator-scope" + (legacy ? "" : "/ct4-1")));
                }
                else if (!left.Grouped && !right.Grouped && left.Node.Type == "Sqrt" && left.BareRoot &&
                    left.Parts[0].Node.Type == "Symbol" && ScopeOperand(left.Parts[0]) && ScopeOperand(right))
                {
                    bool legacy = current.Node.Operator == "add" && left.Parts[0].Node.Type == "Symbol" && right.Node.Type == "Number";
                    var radicand = MakeExpression("Binary", new[] { left.Parts[0], right },
                        new TextSpan(left.Parts[0].Span.Start, right.Span.End), current.Node.Operator, operatorSpan: current.OperatorSpan);
                    var rewritten = MakeExpression("Sqrt", new[] { radicand }, current.Span);
                    proposals.Add(new RepairProposal(Replace(whole, current, rewritten), new[]
                    {
                        new SourceEdit(new TextSpan(left.Parts[0].Span.Start, left.Parts[0].Span.Start), "("),
                        new SourceEdit(new TextSpan(current.Span.End, current.Span.End), ")")
                    }, "SUGGEST_ROOT_SCOPE", legacy ? "Có thể thêm ngoặc để căn phủ toàn bộ tổng." : "Có thể thêm ngoặc để mở rộng phạm vi căn.", "repair/bare-root-scope" + (legacy ? "" : "/ct4-1")));
                }
            }
            foreach (var part in current.Parts)
            {
                if (proposals.Count >= 2) break;
                FindScopeRepairs(whole, part, proposals, cancellation);
            }
        }

        // An explicit group is an intent boundary. A suggestion never removes or reaches through it.
        private static bool ScopeOperand(Expression value) => !value.Grouped &&
            (value.Node.Type == "Number" || value.Node.Type == "Symbol" ||
             (value.Node.Type == "Power" || value.Node.Type == "Unary" || value.Node.Type == "Sqrt" ||
              value.Node.Type == "Binary" && value.Node.Operator == "multiply") && value.Parts.All(ScopeOperand));

        private static Expression Replace(Expression whole, Expression target, Expression replacement)
        {
            if (ReferenceEquals(whole, target)) return replacement;
            if (whole.Parts.Count == 0) return whole;
            var children = whole.Parts.Select(part => Replace(part, target, replacement)).ToArray();
            if (children.Select((child, index) => ReferenceEquals(child, whole.Parts[index])).All(same => same)) return whole;
            return MakeExpression(whole.Node.Type, children, whole.Span, whole.Node.Operator, whole.Node.Value, whole.Node.Name,
                whole.Grouped, whole.BareRoot, whole.OperatorSpan);
        }

        private static Expression MakeExpression(string type, IReadOnlyList<Expression> parts, TextSpan span,
            string? op = null, string? value = null, string? name = null, bool grouped = false, bool bareRoot = false,
            TextSpan? operatorSpan = null)
        {
            var depth = parts.Count == 0 ? 1 : 1 + parts.Max(part => part.Node.Depth);
            if (depth > MaximumDepth) throw new ParseFailure("MAX_DEPTH_EXCEEDED", span, "Cấu trúc công thức vượt độ sâu 64.");
            var node = new MathNode(type, parts.Select(part => part.Node), value, name, op, new[] { span });
            return new Expression(node, parts, span, grouped, bareRoot, operatorSpan);
        }

        private sealed class Expression
        {
            internal MathNode Node { get; }
            internal IReadOnlyList<Expression> Parts { get; }
            internal TextSpan Span { get; }
            internal bool Grouped { get; }
            internal bool BareRoot { get; }
            internal TextSpan? OperatorSpan { get; }

            internal Expression(MathNode node, IReadOnlyList<Expression> parts, TextSpan span, bool grouped, bool bareRoot, TextSpan? operatorSpan)
            { Node = node; Parts = parts; Span = span; Grouped = grouped; BareRoot = bareRoot; OperatorSpan = operatorSpan; }
        }

        private sealed class RepairProposal
        {
            internal Expression Expression { get; }
            internal IReadOnlyList<SourceEdit> Edits { get; }
            internal string Code { get; }
            internal string Message { get; }
            internal string Provenance { get; }
            internal RepairProposal(Expression expression, IReadOnlyList<SourceEdit> edits, string code, string message, string provenance)
            { Expression = expression; Edits = edits; Code = code; Message = message; Provenance = provenance; }
        }

        private sealed class ParseFailure : Exception
        {
            internal string Code { get; }
            internal TextSpan Span { get; }
            internal ParseFailure(string code, TextSpan span, string message) : base(message)
            { Code = code; Span = span; }
        }

        private sealed class ParserState
        {
            private readonly IReadOnlyList<FormulaToken> _tokens;
            private readonly TextSpan _content;
            private readonly bool _tightImplicitDivision;
            private readonly CancellationToken _cancellation;
            private int _position;
            internal int AmbiguityCount { get; private set; }
            internal bool MissingClose { get; private set; }
            internal bool MissingCloseInRoot { get; private set; }
            private FormulaToken Current => _tokens[_position];
            private FormulaToken Previous => _tokens[Math.Max(0, _position - 1)];

            internal ParserState(IReadOnlyList<FormulaToken> tokens, TextSpan content, bool tightImplicitDivision, CancellationToken cancellation)
            { _tokens = tokens; _content = content; _tightImplicitDivision = tightImplicitDivision; _cancellation = cancellation; }

            internal Expression ParseDocument()
            {
                if (Current.Kind == FormulaTokenKind.End) Fail("EMPTY_EXPRESSION", "Chưa có biểu thức.");
                var expression = ParseSum(0);
                if (IsRelation(Current.Kind))
                {
                    var op = Take();
                    var right = ParseSum(0);
                    expression = MakeExpression("Relation", new[] { expression, right }, Cover(expression, right), RelationOperator(op.Kind), operatorSpan: op.Span);
                    if (IsRelation(Current.Kind)) Fail("UNSUPPORTED_RELATION_CHAIN", "Grammar v0 chỉ nhận một phép quan hệ.");
                }
                if (Current.Kind != FormulaTokenKind.End)
                    Fail(Current.Kind == FormulaTokenKind.RightParen ? "UNEXPECTED_CLOSE_PAREN" : "UNSUPPORTED_JUXTAPOSITION", "Còn token không thuộc cấu trúc hợp lệ.");
                return expression;
            }

            private Expression ParseSum(int depth)
            {
                CheckDepth(depth);
                var left = ParseProduct(depth);
                while (Current.Kind == FormulaTokenKind.Plus || Current.Kind == FormulaTokenKind.Minus)
                {
                    var op = Take();
                    var right = ParseProduct(depth);
                    left = MakeExpression("Binary", new[] { left, right }, Cover(left, right), op.Kind == FormulaTokenKind.Plus ? "add" : "subtract", operatorSpan: op.Span);
                }
                return left;
            }

            private Expression ParseProduct(int depth)
            {
                var left = ParseUnary(depth);
                while (true)
                {
                    _cancellation.ThrowIfCancellationRequested();
                    if (Current.Kind == FormulaTokenKind.Multiply || Current.Kind == FormulaTokenKind.Divide)
                    {
                        var op = Take();
                        var right = ParseUnary(depth);
                        if (op.Kind == FormulaTokenKind.Divide && CanImplicitMultiply(Previous.Kind, Current.Kind))
                        {
                            AmbiguityCount++;
                            if (_tightImplicitDivision)
                            {
                                while (CanImplicitMultiply(Previous.Kind, Current.Kind))
                                {
                                    var factor = ParseUnary(depth);
                                    right = MakeExpression("Binary", new[] { right, factor }, Cover(right, factor), "multiply");
                                }
                            }
                        }
                        left = MakeExpression("Binary", new[] { left, right }, Cover(left, right), op.Kind == FormulaTokenKind.Multiply ? "multiply" : "divide", operatorSpan: op.Span);
                    }
                    else if (CanImplicitMultiply(Previous.Kind, Current.Kind))
                    {
                        var right = ParseUnary(depth);
                        left = MakeExpression("Binary", new[] { left, right }, Cover(left, right), "multiply");
                    }
                    else break;
                }
                return left;
            }

            private Expression ParseUnary(int depth)
            {
                CheckDepth(depth);
                if (Current.Kind == FormulaTokenKind.Plus || Current.Kind == FormulaTokenKind.Minus)
                {
                    var op = Take();
                    var operand = ParseUnary(depth + 1);
                    return MakeExpression("Unary", new[] { operand }, new TextSpan(op.Span.Start, operand.Span.End),
                        op.Kind == FormulaTokenKind.Plus ? "plus" : "minus", operatorSpan: op.Span);
                }
                if (Current.Kind == FormulaTokenKind.Root && PeekKind(1) != FormulaTokenKind.LeftParen)
                {
                    var root = Take();
                    if (Current.Kind != FormulaTokenKind.Number && Current.Kind != FormulaTokenKind.Symbol)
                        Fail(Current.Kind == FormulaTokenKind.End ? "MISSING_OPERAND" : "UNSUPPORTED_BARE_ROOT_SCOPE", "Căn trần cần một số hoặc biến; dùng ngoặc cho biểu thức khác.");
                    var radicand = ParseAtom(depth + 1);
                    if (Current.Kind == FormulaTokenKind.Power)
                    {
                        var op = Take();
                        var exponent = ParseUnary(depth + 1);
                        radicand = MakeExpression("Power", new[] { radicand, exponent }, Cover(radicand, exponent), operatorSpan: op.Span);
                    }
                    return MakeExpression("Sqrt", new[] { radicand }, new TextSpan(root.Span.Start, radicand.Span.End), bareRoot: true);
                }
                return ParsePower(depth);
            }

            private Expression ParsePower(int depth)
            {
                var left = ParseAtom(depth);
                if (Current.Kind == FormulaTokenKind.Power)
                {
                    var op = Take();
                    var exponent = ParseUnary(depth + 1);
                    return MakeExpression("Power", new[] { left, exponent }, Cover(left, exponent), operatorSpan: op.Span);
                }
                return left;
            }

            private Expression ParseAtom(int depth)
            {
                CheckDepth(depth);
                var token = Current;
                if (token.Kind == FormulaTokenKind.Number || token.Kind == FormulaTokenKind.Symbol)
                {
                    Take();
                    if (token.EmbeddedNode != null) return FromEmbedded(token.EmbeddedNode);
                    return MakeExpression(token.Kind == FormulaTokenKind.Number ? "Number" : "Symbol", Array.Empty<Expression>(), token.Span,
                        value: token.Kind == FormulaTokenKind.Number ? token.NormalizedText : null,
                        name: token.Kind == FormulaTokenKind.Symbol ? token.NormalizedText : null);
                }
                if (token.Kind == FormulaTokenKind.LeftParen)
                {
                    Take();
                    var child = ParseSum(depth + 1);
                    int end;
                    if (Current.Kind == FormulaTokenKind.RightParen) end = Take().Span.End;
                    else if (Current.Kind == FormulaTokenKind.End && !MissingClose) { MissingClose = true; end = _content.End; }
                    else { Fail("MISSING_CLOSE_PAREN", "Không thể sửa nhiều ngoặc hoặc đoán ranh giới nhóm.", new TextSpan(_content.End, _content.End)); end = _content.End; }
                    return MakeExpression(child.Node.Type, child.Parts, new TextSpan(token.Span.Start, end),
                        child.Node.Operator, child.Node.Value, child.Node.Name, grouped: true, operatorSpan: child.OperatorSpan);
                }
                if (token.Kind == FormulaTokenKind.Root)
                {
                    Take();
                    if (Current.Kind != FormulaTokenKind.LeftParen) Fail("MISSING_OPERAND", "Căn cần toán hạng hoặc ngoặc.");
                    Take();
                    var radicand = ParseSum(depth + 1);
                    int end;
                    if (Current.Kind == FormulaTokenKind.RightParen) end = Take().Span.End;
                    else if (Current.Kind == FormulaTokenKind.End && !MissingClose)
                    {
                        MissingClose = true; MissingCloseInRoot = true;
                        end = _content.End;
                    }
                    else
                    {
                        Fail("MISSING_CLOSE_PAREN", "Không thể sửa nhiều ngoặc hoặc đoán ranh giới căn.", new TextSpan(_content.End, _content.End));
                        end = _content.End;
                    }
                    return MakeExpression("Sqrt", new[] { radicand }, new TextSpan(token.Span.Start, end));
                }
                if (token.Kind == FormulaTokenKind.End || token.Kind == FormulaTokenKind.RightParen)
                    Fail("MISSING_OPERAND", "Thiếu toán hạng.");
                Fail("UNSUPPORTED_OPERATOR_SEQUENCE", "Dãy toán tử không hợp lệ.");
                throw new InvalidOperationException("Unreachable parser state.");
            }

            private FormulaToken Take()
            {
                _cancellation.ThrowIfCancellationRequested();
                var token = Current;
                if (_position + 1 < _tokens.Count) _position++;
                return token;
            }
            private static Expression FromEmbedded(MathNode node) => new Expression(node, node.Children.Select(FromEmbedded).ToArray(), node.SourceSpans[0], false, false, null);

            private FormulaTokenKind PeekKind(int distance) => _tokens[Math.Min(_position + distance, _tokens.Count - 1)].Kind;
            private void CheckDepth(int depth)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (depth >= MaximumDepth) Fail("MAX_DEPTH_EXCEEDED", "Vượt giới hạn độ sâu của grammar.");
            }
            private void Fail(string code, string message, TextSpan? span = null) => throw new ParseFailure(code, span ?? _content, message);
            private static TextSpan Cover(Expression left, Expression right) => new TextSpan(left.Span.Start, right.Span.End);
            private static bool CanImplicitMultiply(FormulaTokenKind left, FormulaTokenKind right) =>
                left == FormulaTokenKind.Number && (right == FormulaTokenKind.Symbol || right == FormulaTokenKind.LeftParen) ||
                left == FormulaTokenKind.Symbol && right == FormulaTokenKind.LeftParen ||
                left == FormulaTokenKind.RightParen && right == FormulaTokenKind.LeftParen;
            private static bool IsRelation(FormulaTokenKind kind) => kind == FormulaTokenKind.Equal || kind == FormulaTokenKind.Less ||
                kind == FormulaTokenKind.Greater || kind == FormulaTokenKind.LessEqual || kind == FormulaTokenKind.GreaterEqual;
            private static string RelationOperator(FormulaTokenKind kind)
            {
                switch (kind)
                {
                    case FormulaTokenKind.Equal: return "eq";
                    case FormulaTokenKind.Less: return "lt";
                    case FormulaTokenKind.Greater: return "gt";
                    case FormulaTokenKind.LessEqual: return "le";
                    case FormulaTokenKind.GreaterEqual: return "ge";
                    default: throw new ArgumentOutOfRangeException(nameof(kind));
                }
            }
        }
    }
}
