using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Locus.Core.Plotting
{
    /// <summary>A bounded, explicit interpretation of plot source. Repairs are never selected automatically.</summary>
    public sealed class PlotInterpretation
    {
        public string Id { get; }
        public string Kind { get; }
        public string Raw { get; }
        public PlotExpression Expression { get; }

        internal PlotInterpretation(string source, string kind, string raw, PlotExpression expression)
        {
            Kind = kind;
            Raw = raw;
            Expression = expression;
            Id = StableId(source + "\n" + kind + "\n" + raw + "\n" + expression.Snapshot);
        }

        private static string StableId(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var text = new StringBuilder("plot-");
                for (var i = 0; i < 12; i++) text.Append(bytes[i].ToString("x2"));
                return text.ToString();
            }
        }
    }

    public static class PlotInterpretationSet
    {
        public const int Limit = 3;
        private static readonly Regex AmbiguousProduct = new Regex(
            @"\A(?<lead>\s*(?:y\s*=\s*)?)(?<a>(?:[0-9]+(?:[\.,][0-9]+)?|[A-Za-z](?:_[0-9]+)?))\s*/\s*(?<b>(?:[0-9]+(?:[\.,][0-9]+)?|[A-Za-z](?:_[0-9]+)?))\s*(?<c>[A-Za-z](?:_[0-9]+)?)(?<tail>\s*)\z",
            RegexOptions.CultureInvariant);

        public static IReadOnlyList<PlotInterpretation> Analyze(string raw, CancellationToken token = default(CancellationToken))
        {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            var results = new List<PlotInterpretation>();
            var snapshots = new HashSet<string>(StringComparer.Ordinal);

            PlotExpression? direct = null;
            try { direct = PlotExpression.Parse(raw, token); }
            catch (PlotParseException) { }

            if (direct != null)
            {
                Add(results, snapshots, raw, "direct", raw, token);
                var defensive = DefensiveFraction(raw, direct.Root);
                if (defensive != null) Add(results, snapshots, raw, "alternative", defensive, token);
                return results;
            }

            var ambiguous = AmbiguousProduct.Match(raw);
            if (ambiguous.Success)
            {
                var lead = ambiguous.Groups["lead"].Value;
                var a = ambiguous.Groups["a"].Value;
                var b = ambiguous.Groups["b"].Value;
                var c = ambiguous.Groups["c"].Value;
                var tail = ambiguous.Groups["tail"].Value;
                Add(results, snapshots, raw, "alternative", lead + "(" + a + "/" + b + ")*" + c + tail, token);
                Add(results, snapshots, raw, "alternative", lead + a + "/(" + b + "*" + c + ")" + tail, token);
            }

            var missing = MissingClosingParentheses(raw);
            if (missing > 0 && missing <= 4)
                Add(results, snapshots, raw, "repair", raw + new string(')', missing), token);

            return results;
        }

        private static string? DefensiveFraction(string raw, PlotNode root)
        {
            if (root.Kind != "binary" || (root.Value != "+" && root.Value != "-") || root.Children.Count != 2) return null;
            var fraction = root.Children[1];
            if (fraction.Kind != "binary" || fraction.Value != "/" || fraction.Children.Count != 2) return null;
            var numerator = fraction.Children[0];
            var denominator = fraction.Children[1];
            if (root.Start < 0 || numerator.End < root.Start || denominator.Start < numerator.End || denominator.End > raw.Length) return null;
            return raw.Substring(0, root.Start)
                + "(" + raw.Substring(root.Start, numerator.End - root.Start) + ")/("
                + raw.Substring(denominator.Start, denominator.End - denominator.Start) + ")"
                + raw.Substring(root.End);
        }

        private static int MissingClosingParentheses(string raw)
        {
            var balance = 0;
            foreach (var ch in raw)
            {
                if (ch == '(') balance++;
                else if (ch == ')' && --balance < 0) return 0;
            }
            return balance;
        }

        private static void Add(List<PlotInterpretation> results, HashSet<string> snapshots, string source, string kind, string raw, CancellationToken token)
        {
            if (results.Count >= Limit) return;
            try
            {
                var expression = PlotExpression.Parse(raw, token);
                if (snapshots.Add(expression.Snapshot)) results.Add(new PlotInterpretation(source, kind, raw, expression));
            }
            catch (PlotParseException) { }
        }
    }
}
