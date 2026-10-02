using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Locus.Core.Plotting
{
    public sealed class PlotSample
    {
        public IReadOnlyList<double[][]> Paths { get; }
        public int Evaluations { get; }
        public bool Limited { get; }
        internal PlotSample(List<double[][]> paths, int evaluations, bool limited) { Paths = paths; Evaluations = evaluations; Limited = limited; }
    }
    /// <summary>Adaptive screen-space sampling; conservative interval checks split uncertain domains.</summary>
    public static class PlotSampler
    {
        public static PlotSample Sample(PlotNode bound, double left, double right, double bottom, double top, CancellationToken token = default)
        {
            if (!(left < right) || !(bottom < top)) throw new ArgumentException("Invalid sampling viewport.");
            var paths = new List<double[][]>(); var active = new List<double[]>();
            int evaluations = 0; bool limited = false;
            double tolerance = (top - bottom) / 800;
            Action cut = () => { if (active.Count > 1) paths.Add(active.ToArray()); active.Clear(); };
            Func<double, double> evaluate = x => { evaluations++; return bound.Evaluate(x); };
            Action<double, double, double, double, int> visit = null!;
            visit = (a, b, ya, yb, depth) =>
            {
                token.ThrowIfCancellationRequested();
                if (evaluations >= 12000) { limited = true; cut(); return; }
                double m = a + (b - a) / 2, ym = evaluate(m);
                var range = Range(bound, a, b);
                if (range.Empty) { cut(); return; }
                bool valid = Finite(ya) && Finite(yb) && Finite(ym);
                bool outside = range.Safe && (range.Low > top + (top - bottom) || range.High < bottom - (top - bottom));
                if (outside) { cut(); return; }
                // Interval extrema catch oscillations whose endpoints and midpoint alias to the same value.
                bool unseenTurn = bound.Oscillatory && range.Safe && (range.High > Math.Max(ya, Math.Max(ym, yb)) + tolerance || range.Low < Math.Min(ya, Math.Min(ym, yb)) - tolerance);
                bool split = !valid || !range.Safe || unseenTurn || Math.Abs(ym - (ya / 2 + yb / 2)) > tolerance;
                if (split)
                {
                    if (depth >= 14 || b - a < (right - left) / 1e7) { if (valid && range.Safe) limited = true; cut(); return; }
                    visit(a, m, ya, ym, depth + 1); visit(m, b, ym, yb, depth + 1); return;
                }
                // Clip huge off-screen coordinates before SVG serialization; never bridge a skipped interval.
                if (Math.Abs(ya) > 1e12 || Math.Abs(yb) > 1e12) { cut(); return; }
                if (active.Count == 0) active.Add(new[] { a, ya });
                active.Add(new[] { b, yb });
            };
            for (int i = 0; i < 96 && evaluations < 12000; i++)
            {
                double a = left + (right - left) * i / 96, b = left + (right - left) * (i + 1) / 96;
                visit(a, b, evaluate(a), evaluate(b), 0);
            }
            cut(); return new PlotSample(paths, evaluations, limited || evaluations >= 12000);
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        private readonly struct Interval
        {
            internal readonly double Low, High; internal readonly bool Safe, Empty;
            internal Interval(double low, double high, bool safe = true, bool empty = false) { Low = low; High = high; Safe = safe && Finite(low) && Finite(high); Empty = empty; }
        }
        private static Interval Range(PlotNode n, double x0, double x1)
        {
            if (n.Kind == "number") { double v = n.Evaluate(0); return new Interval(v, v, empty: !Finite(v)); }
            if (n.Kind == "symbol") return new Interval(x0, x1);
            var a = Range(n.Children[0], x0, x1);
            if (!a.Safe) return new Interval(0, 0, false, a.Empty);
            if (n.Kind == "unary") return n.Value == "-" ? new Interval(-a.High, -a.Low) : a;
            if (n.Kind == "function")
            {
                if (n.Value == "sqrt") return new Interval(Math.Sqrt(a.Low), Math.Sqrt(a.High), a.Low >= 0, a.High < 0);
                if (n.Value == "ln") return new Interval(Math.Log(a.Low), Math.Log(a.High), a.Low > 0, a.High <= 0);
                if (n.Value == "exp") return new Interval(Math.Exp(a.Low), Math.Exp(a.High));
                if (n.Value == "abs") return new Interval(a.Low <= 0 && a.High >= 0 ? 0 : Math.Min(Math.Abs(a.Low), Math.Abs(a.High)), Math.Max(Math.Abs(a.Low), Math.Abs(a.High)));
                if (n.Value == "tan")
                {
                    bool pole = Math.Ceiling((a.Low - Math.PI / 2) / Math.PI) <= Math.Floor((a.High - Math.PI / 2) / Math.PI);
                    return new Interval(Math.Tan(a.Low), Math.Tan(a.High), !pole);
                }
                double lo = a.Low + (n.Value == "cos" ? Math.PI / 2 : 0), hi = a.High + (n.Value == "cos" ? Math.PI / 2 : 0);
                if (hi - lo >= Math.PI * 2) return new Interval(-1, 1);
                double low = Math.Min(Math.Sin(lo), Math.Sin(hi)), high = Math.Max(Math.Sin(lo), Math.Sin(hi));
                if (Math.Ceiling((lo - Math.PI / 2) / (2 * Math.PI)) <= Math.Floor((hi - Math.PI / 2) / (2 * Math.PI))) high = 1;
                if (Math.Ceiling((lo + Math.PI / 2) / (2 * Math.PI)) <= Math.Floor((hi + Math.PI / 2) / (2 * Math.PI))) low = -1;
                return new Interval(low, high);
            }
            var b = Range(n.Children[1], x0, x1); if (!b.Safe) return new Interval(0, 0, false, b.Empty);
            if (n.Value == "+") return new Interval(a.Low + b.Low, a.High + b.High);
            if (n.Value == "-") return new Interval(a.Low - b.High, a.High - b.Low);
            if (n.Value == "/")
            {
                if (b.Low <= 0 && b.High >= 0) return new Interval(0, 0, false, b.Low == 0 && b.High == 0);
                b = new Interval(1 / b.High, 1 / b.Low);
            }
            if (n.Value == "*" || n.Value == "/")
            {
                var products = new[] { a.Low * b.Low, a.Low * b.High, a.High * b.Low, a.High * b.High };
                return new Interval(products.Min(), products.Max());
            }
            if (b.Low == b.High)
            {
                double power = b.Low;
                if (a.Low <= 0 && a.High >= 0 && power == 0) return new Interval(1, 1, false, a.Low == 0 && a.High == 0);
                if (a.Low <= 0 && a.High >= 0 && power < 0) return new Interval(0, 0, false);
                if (a.Low < 0 && power != Math.Truncate(power)) return new Interval(0, 0, false, a.High < 0);
                double p = Math.Pow(a.Low, power), q = Math.Pow(a.High, power);
                return new Interval(a.Low <= 0 && a.High >= 0 && power > 0 && power % 2 == 0 ? 0 : Math.Min(p, q), Math.Max(p, q));
            }
            if (a.Low <= 0) return new Interval(0, 0, false);
            var values = new[] { Math.Pow(a.Low, b.Low), Math.Pow(a.Low, b.High), Math.Pow(a.High, b.Low), Math.Pow(a.High, b.High) };
            return new Interval(values.Min(), values.Max());
        }
    }
}
