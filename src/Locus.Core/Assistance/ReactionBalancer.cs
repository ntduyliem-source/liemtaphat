using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
using Locus.Core.Domains;

namespace Locus.Core.Assistance
{
    public enum BalanceStatus { AlreadyBalanced, Balanced, NoSolution, NonUnique, Limit, Invalid }
    public sealed class BalanceResult
    {
        public BalanceStatus Status { get; }
        public CandidateSet? Result { get; }
        public IReadOnlyList<string> Coefficients { get; }
        public string Message { get; }
        internal BalanceResult(BalanceStatus status, CandidateSet? result = null, IEnumerable<BigInteger>? coefficients = null)
        {
            Status=status;Result=result;Coefficients=Freeze.Of(coefficients?.Select(n=>n.ToString(CultureInfo.InvariantCulture)));
            Message=status==BalanceStatus.AlreadyBalanced?"Phương trình đã có hệ số nguyên tối giản.":status==BalanceStatus.Balanced?"Có bản cân bằng bảo toàn nguyên tố và điện tích.":
                status==BalanceStatus.NoSolution?"Không có nghiệm dương bảo toàn với các chất đã nhập.":status==BalanceStatus.NonUnique?"Chưa xác định được một tỉ lệ hệ số duy nhất; cần làm rõ các chất.":
                status==BalanceStatus.Limit?"Phương trình vượt giới hạn xử lý. Hãy tách hoặc rút gọn đầu vào.":"Cần một phương trình Hóa rõ nghĩa, đủ hai vế.";
        }
    }

    /// <summary>Exact rational nullspace, without adding, dropping or identifying chemical species.</summary>
    public static class ReactionBalancer
    {
        public const int MaxSpecies=32, MaxRows=128, MaxIntegerBits=4096;
        private sealed class BudgetException : Exception { }
        private static readonly BigInteger MagnitudeLimit=BigInteger.One<<MaxIntegerBits;
        private static BigInteger Bounded(BigInteger value) { if(BigInteger.Abs(value)>=MagnitudeLimit)throw new BudgetException();return value; }
        private readonly struct Rational
        {
            internal readonly BigInteger N;private readonly BigInteger denominator;
            internal BigInteger D=>denominator.IsZero?BigInteger.One:denominator;
            internal Rational(BigInteger n,BigInteger d)
            {
                if(d.IsZero)throw new DivideByZeroException();if(d.Sign<0){n=-n;d=-d;}
                var gcd=BigInteger.GreatestCommonDivisor(BigInteger.Abs(n),d);N=Bounded(n/gcd);denominator=Bounded(d/gcd);
            }
            public static implicit operator Rational(BigInteger n)=>new Rational(n,BigInteger.One);
            public static Rational operator -(Rational a)=>new Rational(-a.N,a.D);
            public static Rational operator -(Rational a,Rational b)
            {
                var gcd=BigInteger.GreatestCommonDivisor(a.D,b.D);return new Rational(Bounded(a.N*(b.D/gcd)-b.N*(a.D/gcd)),Bounded(a.D*(b.D/gcd)));
            }
            public static Rational operator *(Rational a,Rational b)
            {
                var x=BigInteger.GreatestCommonDivisor(BigInteger.Abs(a.N),b.D);var y=BigInteger.GreatestCommonDivisor(BigInteger.Abs(b.N),a.D);
                return new Rational(Bounded((a.N/x)*(b.N/y)),Bounded((a.D/y)*(b.D/x)));
            }
            public static Rational operator /(Rational a,Rational b)=>a*new Rational(b.D,b.N);
        }
        private sealed class Species
        {
            internal MathNode Formula;
            internal BigInteger InputCoefficient;
            internal Dictionary<string,BigInteger> Atoms=new Dictionary<string,BigInteger>(StringComparer.Ordinal);
            internal BigInteger Charge;
            internal Species(MathNode node)
            {
                InputCoefficient=node.Type=="ChemCoefficient"?Integer(node.Children[0].Value!):BigInteger.One;
                Formula=node.Type=="ChemCoefficient"?node.Children[1]:node;
            }
        }
        public static BalanceResult Balance(ScientificDocument document,CancellationToken cancellationToken=default,int deadlineMs=1000)
        {
            if(deadlineMs<1||deadlineMs>5000)throw new ArgumentOutOfRangeException(nameof(deadlineMs));
            cancellationToken.ThrowIfCancellationRequested();var timer=Stopwatch.StartNew();
            void Tick(){cancellationToken.ThrowIfCancellationRequested();if(timer.ElapsedMilliseconds>deadlineMs)throw new BudgetException();}
            if(document==null||document.Domain!="chemistry"||document.Root.Type!="ChemReaction")return new BalanceResult(BalanceStatus.Invalid);
            try
            {
                var root=document.Root;var left=new List<Species>();var right=new List<Species>();
                Flatten(root.Children[0],left);Flatten(root.Children[1],right);var species=left.Concat(right).ToArray();
                if(species.Length>MaxSpecies)throw new BudgetException();
                var keys=new HashSet<string>(StringComparer.Ordinal);
                foreach(var s in species)
                {
                    Tick();var key=ChemistryProjection.Create(s.Formula).Source.Raw;
                    if(!keys.Add(key))return new BalanceResult(BalanceStatus.NonUnique);
                    Count(s.Formula,s,BigInteger.One,Tick);
                }
                var elements=species.SelectMany(s=>s.Atoms.Keys).Distinct(StringComparer.Ordinal).OrderBy(s=>s,StringComparer.Ordinal).ToArray();
                bool charge=species.Any(s=>!s.Charge.IsZero);int rows=elements.Length+(charge?1:0),columns=species.Length;
                if(rows>MaxRows||rows==0)throw new BudgetException();
                var matrix=new Rational[rows,columns];
                for(int c=0;c<columns;c++)
                {
                    var sign=c<left.Count?BigInteger.One:-BigInteger.One;
                    for(int r=0;r<elements.Length;r++)matrix[r,c]=species[c].Atoms.TryGetValue(elements[r],out var count)?count*sign:BigInteger.Zero;
                    if(charge)matrix[rows-1,c]=species[c].Charge*sign;
                }
                var pivots=new List<int>();int rank=0;
                for(int c=0;c<columns&&rank<rows;c++)
                {
                    Tick();int pivot=rank;while(pivot<rows&&matrix[pivot,c].N.IsZero)pivot++;
                    if(pivot==rows)continue;
                    if(pivot!=rank)for(int k=0;k<columns;k++){var old=matrix[rank,k];matrix[rank,k]=matrix[pivot,k];matrix[pivot,k]=old;}
                    var divisor=matrix[rank,c];for(int k=c;k<columns;k++)matrix[rank,k]=matrix[rank,k]/divisor;
                    for(int r=0;r<rows;r++)
                    {
                        Tick();if(r==rank||matrix[r,c].N.IsZero)continue;var factor=matrix[r,c];
                        for(int k=c;k<columns;k++)matrix[r,k]=matrix[r,k]-factor*matrix[rank,k];
                    }
                    pivots.Add(c);rank++;
                }
                int freedom=columns-rank;
                if(freedom==0)return new BalanceResult(BalanceStatus.NoSolution);
                if(freedom!=1)return new BalanceResult(BalanceStatus.NonUnique);
                int free=Enumerable.Range(0,columns).First(c=>!pivots.Contains(c));var vector=new Rational[columns];vector[free]=BigInteger.One;
                for(int r=0;r<rank;r++)vector[pivots[r]]=-matrix[r,free];
                if(vector.Any(v=>v.N.Sign<=0))return new BalanceResult(BalanceStatus.NoSolution);
                BigInteger lcm=BigInteger.One;
                foreach(var v in vector){Tick();lcm=Bounded(lcm/BigInteger.GreatestCommonDivisor(lcm,v.D)*v.D);}
                var coefficients=vector.Select(v=>Bounded(v.N*(lcm/v.D))).ToArray();
                var divisorAll=coefficients.Aggregate(BigInteger.Zero,(a,b)=>BigInteger.GreatestCommonDivisor(a,b));
                for(int i=0;i<coefficients.Length;i++)coefficients[i]/=divisorAll;
                var outputRoot=new MathNode("ChemReaction",new[]{Side(left,coefficients,0),Side(right,coefficients,left.Count)},@operator:root.Operator);
                var projected=ChemistryProjection.Create(outputRoot);Tick();
                if(!VerifyConservation(projected.Candidates[0].Document.Root,cancellationToken))return new BalanceResult(BalanceStatus.Invalid);
                return coefficients.SequenceEqual(species.Select(s=>s.InputCoefficient))?new BalanceResult(BalanceStatus.AlreadyBalanced,coefficients:coefficients):new BalanceResult(BalanceStatus.Balanced,projected,coefficients);
            }
            catch(BudgetException){return new BalanceResult(BalanceStatus.Limit);}
            catch(ChemistryProjectionLimitException){return new BalanceResult(BalanceStatus.Limit);}
            catch(ArgumentException){return new BalanceResult(BalanceStatus.Invalid);}
        }
        private static void Flatten(MathNode node,List<Species> result)
        {
            if(result.Count>=MaxSpecies)throw new BudgetException();
            if(node.Type=="ChemSum"){Flatten(node.Children[0],result);Flatten(node.Children[1],result);}else result.Add(new Species(node));
        }
        private static BigInteger Integer(string value)=>Bounded(BigInteger.Parse(value,CultureInfo.InvariantCulture));
        private static void Count(MathNode node,Species s,BigInteger multiplier,Action tick)
        {
            tick();switch(node.Type)
            {
                case "ChemElement":s.Atoms[node.Name!]=Bounded((s.Atoms.TryGetValue(node.Name!,out var old)?old:BigInteger.Zero)+multiplier);break;
                case "ChemConcat":foreach(var child in node.Children)Count(child,s,multiplier,tick);break;
                case "ChemGroup":Count(node.Children[0],s,multiplier,tick);break;
                case "ChemSubscript":Count(node.Children[0],s,Bounded(multiplier*Integer(node.Children[1].Value!)),tick);break;
                case "ChemCharge":s.Charge=Charge(node.Value!);Count(node.Children[0],s,multiplier,tick);break;
                default:throw new ArgumentException("Invalid species.");
            }
        }
        private static BigInteger Charge(string value)=>(value.Length==1?BigInteger.One:Integer(value.Substring(0,value.Length-1)))*(value[value.Length-1]=='+'?1:-1);
        private static MathNode Side(List<Species> species,BigInteger[] coefficients,int offset)
        {
            MathNode? side=null;
            for(int i=0;i<species.Count;i++)
            {
                var n=species[i].Formula;var coefficient=coefficients[offset+i];
                if(coefficient!=BigInteger.One)n=new MathNode("ChemCoefficient",new[]{new MathNode("Number",value:coefficient.ToString(CultureInfo.InvariantCulture)),n});
                side=side==null?n:new MathNode("ChemSum",new[]{side,n});
            }
            return side!;
        }

        /// <summary>Separate traversal of the final AST checks conservation, independently of the matrix or its solution vector.</summary>
        public static bool VerifyConservation(MathNode root,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();if(root.Type!="ChemReaction")return false;
            var ledger=new Dictionary<string,BigInteger>(StringComparer.Ordinal);
            try
            {
                var pending=new Stack<Tuple<MathNode,BigInteger>>();pending.Push(Tuple.Create(root.Children[0],BigInteger.One));pending.Push(Tuple.Create(root.Children[1],-BigInteger.One));
                int visited=0;
                while(pending.Count>0)
                {
                    token.ThrowIfCancellationRequested();if(++visited>10000)throw new BudgetException();
                    var current=pending.Pop();var n=current.Item1;var weight=current.Item2;
                    if(n.Type=="ChemElement")Add(n.Name!,weight);
                    else if(n.Type=="ChemCoefficient")pending.Push(Tuple.Create(n.Children[1],Bounded(weight*Integer(n.Children[0].Value!))));
                    else if(n.Type=="ChemSubscript")pending.Push(Tuple.Create(n.Children[0],Bounded(weight*Integer(n.Children[1].Value!))));
                    else if(n.Type=="ChemCharge"){Add("charge",Bounded(weight*Charge(n.Value!)));pending.Push(Tuple.Create(n.Children[0],weight));}
                    else if(n.Type=="ChemConcat"||n.Type=="ChemSum"||n.Type=="ChemGroup")foreach(var child in n.Children)pending.Push(Tuple.Create(child,weight));
                    else return false;
                }
                return ledger.Values.All(n=>n.IsZero);
                void Add(string key,BigInteger amount)=>ledger[key]=Bounded((ledger.TryGetValue(key,out var old)?old:BigInteger.Zero)+amount);
            }
            catch(BudgetException){return false;}
        }
    }
}
