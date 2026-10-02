using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Locus.Core.Domains
{
    public static class DomainVersions
    {
        public const string Core = "locus-core/0.2";
        public const string Grammar = "vi-science-e3/0.1";
        public const string Snapshot = "locus-candidate-set/0.2";
    }

    public abstract class ScientificDocument
    {
        public MathNode Root { get; }
        public abstract string Domain { get; }
        public abstract string SchemaVersion { get; }
        public virtual string GrammarVersion => DomainVersions.Grammar;
        public virtual string CoreVersion => DomainVersions.Core;
        protected ScientificDocument(MathNode root) { Root = root ?? throw new ArgumentNullException(nameof(root)); }
        public static ScientificDocument Create(string domain, MathNode root, string? grammarVersion = null)
        {
            if (domain == "chemistry" && grammarVersion == Assistance.SmartChemistryVersions.Grammar) return new SmartChemistryDocument(root);
            switch (domain)
            {
                case "math": return new MathDocument(root);
                case "chemistry": return new ChemistryDocument(root);
                case "physics": return new PhysicsDocument(root);
                default: throw new ArgumentException("Unsupported document domain.");
            }
        }
    }

    public sealed class SmartChemistryDocument : ScientificDocument
    {
        public override string Domain => "chemistry";
        public override string SchemaVersion => "chemistry-document/0.1";
        public override string CoreVersion => Assistance.SmartChemistryVersions.Core;
        public override string GrammarVersion => Assistance.SmartChemistryVersions.Grammar;
        public SmartChemistryDocument(MathNode root) : base(root) { ScientificNodes.ValidateDocument(root, Domain); }
    }

    public sealed class ChemistryDocument : ScientificDocument
    {
        public override string Domain => "chemistry";
        public override string SchemaVersion => "chemistry-document/0.1";
        public ChemistryDocument(MathNode root) : base(root)
        { ScientificNodes.ValidateDocument(root, Domain); }
    }

    public sealed class PhysicsDocument : ScientificDocument
    {
        public override string Domain => "physics";
        public override string SchemaVersion => "physics-document/0.1";
        public PhysicsDocument(MathNode root) : base(root)
        { ScientificNodes.ValidateDocument(root, Domain); }
    }

    public static class ScientificSymbols
    {
        private static readonly HashSet<string> Elements = new HashSet<string>(
            "H He Li Be B C N O F Ne Na Mg Al Si P S Cl Ar K Ca Sc Ti V Cr Mn Fe Co Ni Cu Zn Ga Ge As Se Br Kr Rb Sr Y Zr Nb Mo Tc Ru Rh Pd Ag Cd In Sn Sb Te I Xe Cs Ba La Ce Pr Nd Pm Sm Eu Gd Tb Dy Ho Er Tm Yb Lu Hf Ta W Re Os Ir Pt Au Hg Tl Pb Bi Po At Rn Fr Ra Ac Th Pa U Np Pu Am Cm Bk Cf Es Fm Md No Lr Rf Db Sg Bh Hs Mt Ds Rg Cn Nh Fl Mc Lv Ts Og".Split(' '), StringComparer.Ordinal);
        private static readonly HashSet<string> BaseUnits = new HashSet<string>(
            "m s kg g A K mol cd Hz N Pa J W C V F Ω S Wb T H lm lx Bq Gy Sv kat rad sr min h d L l eV °C".Split(' '), StringComparer.Ordinal);
        private static readonly string[] Prefixes = { "da", "Y", "Z", "E", "P", "T", "G", "M", "k", "h", "d", "c", "m", "μ", "µ", "n", "p", "f", "a", "z", "y", "R", "Q", "r", "q" };
        private static readonly HashSet<string> Prefixable = new HashSet<string>(
            "m s g A K mol cd Hz N Pa J W C V F Ω S Wb T H lm lx Bq Gy Sv kat rad sr L l eV".Split(' '), StringComparer.Ordinal);
        internal static readonly Dictionary<string, string> GreekAliases = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["alpha"]="α", ["beta"]="β", ["gamma"]="γ", ["delta"]="δ", ["epsilon"]="ε", ["theta"]="θ",
            ["lambda"]="λ", ["micro"]="μ", ["nu"]="ν", ["pi"]="π", ["rho"]="ρ", ["sigma"]="σ",
            ["tau"]="τ", ["phi"]="φ", ["chi"]="χ", ["psi"]="ψ", ["omega"]="ω",
            ["Gamma"]="Γ", ["Delta"]="Δ", ["Theta"]="Θ", ["Lambda"]="Λ", ["Pi"]="Π", ["Sigma"]="Σ", ["Phi"]="Φ", ["Psi"]="Ψ", ["Omega"]="Ω"
        };
        public static bool IsElement(string? name) => name != null && Elements.Contains(name);
        public static bool IsGreek(string? name) => name != null && name.Length == 1 && "αβγδεζηθικλμνξοπρστυφχψωΓΔΘΛΞΠΣΥΦΨΩ".Contains(name);
        public static bool IsUnit(string? name) => name != null && (BaseUnits.Contains(name) || Prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal) && Prefixable.Contains(name.Substring(p.Length))));
    }

    internal static class ScientificNodes
    {
        private static readonly HashSet<string> MathTypes = new HashSet<string>(new[] { "Number", "Symbol", "Unary", "Binary", "Power", "Sqrt", "Relation" });
        internal static bool IsExtended(string type) => type.StartsWith("Chem", StringComparison.Ordinal) || new[] { "Greek", "Subscript", "Vector", "Unit", "Quantity" }.Contains(type);
        internal static void Validate(MathNode node)
        {
            var n = node.Children.Count;
            bool valid;
            switch (node.Type)
            {
                case "ChemElement": valid = n == 0 && ScientificSymbols.IsElement(node.Name); break;
                case "ChemConcat": valid = n == 2 && node.Children.All(IsFormula); break;
                case "ChemGroup": valid = n == 1 && IsFormula(node.Children[0]) && (node.Name == "()" || node.Name == "[]"); break;
                case "ChemSubscript": valid = n == 2 && (node.Children[0].Type == "ChemElement" || node.Children[0].Type == "ChemGroup") && PositiveInteger(node.Children[1]); break;
                case "ChemCharge": valid = n == 1 && IsFormula(node.Children[0]) && node.Value != null && Regex.IsMatch(node.Value, @"\A(?:[1-9][0-9]*)?[+-]\z"); break;
                case "ChemCoefficient": valid = n == 2 && PositiveInteger(node.Children[0]) && (IsFormula(node.Children[1]) || node.Children[1].Type == "ChemCharge"); break;
                case "ChemSum": valid = n == 2 && node.Children.All(IsSide); break;
                case "ChemReaction": valid = n == 2 && node.Children.All(IsSide) && (node.Operator == "arrow" || node.Operator == "reversible"); break;
                case "Greek": valid = n == 0 && ScientificSymbols.IsGreek(node.Name); break;
                case "Unit": valid = n == 0 && ScientificSymbols.IsUnit(node.Name); break;
                case "Subscript": valid = n == 2 && (node.Children[0].Type == "Symbol" || node.Children[0].Type == "Greek" || node.Children[0].Type == "Vector") && (node.Children[1].Type == "Symbol" || (node.Children[1].Type == "Number" && !node.Children[1].Value!.Contains('.'))); break;
                case "Vector": valid = n == 1 && (node.Children[0].Type == "Symbol" || node.Children[0].Type == "Greek" || node.Children[0].Type == "Subscript"); break;
                case "Quantity": valid = n == 2 && (node.Children[0].Type == "Number" || node.Children[0].Type == "Unary" && node.Children[0].Children[0].Type == "Number") && IsUnitTree(node.Children[1]); break;
                default: valid = false; break;
            }
            if (!valid || (node.Type != "ChemElement" && node.Type != "ChemGroup" && node.Type != "Greek" && node.Type != "Unit" && node.Name != null) ||
                (node.Type != "ChemCharge" && node.Value != null) || (node.Type != "ChemReaction" && node.Operator != null))
                throw new ArgumentException("Invalid scientific node: " + node.Type);
        }
        private static bool PositiveInteger(MathNode n) => n.Type == "Number" && n.Value != null && Regex.IsMatch(n.Value, @"\A[1-9][0-9]*\z");
        private static bool IsFormula(MathNode n) => new[] { "ChemElement", "ChemConcat", "ChemGroup", "ChemSubscript" }.Contains(n.Type);
        private static bool IsSide(MathNode n) => IsFormula(n) || n.Type == "ChemCharge" || n.Type == "ChemCoefficient" || n.Type == "ChemSum";
        internal static bool IsUnitTree(MathNode n) => n.Type == "Unit" || n.Type == "Power" && n.Children[0].Type == "Unit" && IsInteger(n.Children[1]) ||
            n.Type == "Binary" && (n.Operator == "multiply" || n.Operator == "divide") && n.Children.All(IsUnitTree);
        private static bool IsInteger(MathNode n) => n.Type == "Number" && !n.Value!.Contains('.') || n.Type == "Unary" && n.Children[0].Type == "Number" && !n.Children[0].Value!.Contains('.');
        internal static void ValidateDocument(MathNode root, string domain)
        {
            if (domain == "chemistry" && !IsSide(root) && root.Type != "ChemReaction") throw new ArgumentException("Invalid chemistry root.");
            Walk(root, domain);
        }
        private static void Walk(MathNode n, string domain)
        {
            bool valid = domain == "math" ? MathTypes.Contains(n.Type) : domain == "chemistry" ? n.Type == "Number" || n.Type.StartsWith("Chem", StringComparison.Ordinal) : MathTypes.Contains(n.Type) || IsExtended(n.Type) && !n.Type.StartsWith("Chem", StringComparison.Ordinal);
            if (!valid) throw new ArgumentException("Node does not belong to document domain.");
            foreach (var child in n.Children) Walk(child, domain);
        }
    }
}
