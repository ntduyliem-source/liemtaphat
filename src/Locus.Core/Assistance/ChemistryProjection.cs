using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Locus.Core.Domains;

namespace Locus.Core.Assistance
{
    public sealed class ChemistryProjectionLimitException : ArgumentException { public ChemistryProjectionLimitException() : base("Generated formula exceeds the input budget.") { } }
    /// <summary>Assigns fresh spans while projecting an existing AST; it does not parse or choose a chemical interpretation.</summary>
    public static class ChemistryProjection
    {
        public static MathNode WithoutCoefficients(MathNode node) => node.Type == "ChemCoefficient" ? WithoutCoefficients(node.Children[1]) :
            new MathNode(node.Type, node.Children.Select(WithoutCoefficients), node.Value, node.Name, node.Operator);
        public static bool SameSpecies(Candidate first, Candidate second) =>
            Create(WithoutCoefficients(first.Document.Root)).Source.Raw == Create(WithoutCoefficients(second.Document.Root)).Source.Raw;
        public static CandidateSet ProductsBeforeBalance(ReactionDraft draft, AssistanceProposal proposal)
        {
            if (proposal.Kind != "complete-reaction" || proposal.Region.Id != draft.Region.Id) throw new ArgumentException("Product draft mismatch.");
            return Create(new MathNode("ChemReaction", new[] { draft.Reactants.Candidates[0].Document.Root,
                WithoutCoefficients(proposal.Result.Candidates[0].Document.Root.Children[1]) }, @operator: proposal.Result.Candidates[0].Document.Root.Operator));
        }
        public static CandidateSet Create(MathNode root, long revision = 0)
        {
            _ = new ChemistryDocument(root);
            var text = new StringBuilder(); var projected = Write(root, text);
            if (text.Length > 4096) throw new ChemistryProjectionLimitException();
            var source = new SourceSnapshot(text.ToString(), revision); var span = new TextSpan(0, text.Length);
            var candidate = new Candidate("direct", new ChemistryDocument(projected), source, span, span, provenance: "assistance/result/0.1");
            return new CandidateSet(source, span, span, new[] { candidate });
        }
        private static MathNode Write(MathNode n, StringBuilder text)
        {
            int start = text.Length; var children = new List<MathNode>();
            switch (n.Type)
            {
                case "ChemElement": text.Append(n.Name); break;
                case "Number": text.Append(n.Value); break;
                case "ChemConcat":
                case "ChemCoefficient":
                case "ChemSubscript": foreach (var child in n.Children) children.Add(Write(child, text)); break;
                case "ChemGroup": text.Append(n.Name![0]); children.Add(Write(n.Children[0], text)); text.Append(n.Name[1]); break;
                case "ChemCharge": children.Add(Write(n.Children[0], text)); text.Append('^').Append(n.Value); break;
                case "ChemSum": children.Add(Write(n.Children[0], text)); text.Append('+'); children.Add(Write(n.Children[1], text)); break;
                case "ChemReaction": children.Add(Write(n.Children[0], text)); text.Append(n.Operator == "reversible" ? "<->" : "->"); children.Add(Write(n.Children[1], text)); break;
                default: throw new ArgumentException("Unsupported chemistry projection.");
            }
            if (text.Length > 4096) throw new ChemistryProjectionLimitException();
            return new MathNode(n.Type, children, n.Value, n.Name, n.Operator, new[] { new TextSpan(start, text.Length) });
        }
        public static MathNode Rebase(MathNode node, int offset)
        {
            var children = new List<MathNode>(); foreach (var child in node.Children) children.Add(Rebase(child, offset));
            var spans = new List<TextSpan>(); foreach (var span in node.SourceSpans) spans.Add(new TextSpan(checked(span.Start + offset), checked(span.End + offset)));
            return new MathNode(node.Type, children, node.Value, node.Name, node.Operator, spans);
        }
    }
}
