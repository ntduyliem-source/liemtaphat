using System.Globalization;
using System.Security;
using System.Text;
using System.Text.Json;
using Locus.Core.Plotting;

namespace Locus.Application;

public sealed record PlotRequest(PlotDocument Document, long Version);
public sealed record PlotReading(string Id, string Kind, string Raw, string MathMl, string Latex);
public sealed record PlotCurveResult(string Id, string MathMl, string Latex, string[] Parameters, string? Problem, int Start, int End, double[][][] Paths, bool Limited, PlotReading[] Readings, string? SelectedReadingId);
public sealed record PlotResult(long Version, PlotCurveResult[] Curves, string Svg);
public interface IPlotScheduler { Task<PlotResult> PlotAsync(PlotRequest request, CancellationToken token); }

public static class PlotWire
{
    public const string Operation = "plot";
    private sealed record Message(string Operation, PlotRequest Request);
    public static string Request(PlotRequest request) => JsonSerializer.Serialize(new Message(Operation, request));
    public static string Run(string json) => JsonSerializer.Serialize(Analyze((JsonSerializer.Deserialize<Message>(json) ?? throw new FormatException()).Request));
    public static PlotResult Deserialize(string json) => JsonSerializer.Deserialize<PlotResult>(json) ?? throw new FormatException();
    public static PlotResult Analyze(PlotRequest request, CancellationToken token = default)
    {
        var document = request.Document; DocumentCodec.ValidatePlot(document);
        var values = (document.Parameters ?? []).Where(p => p.Value.HasValue).ToDictionary(p => p.Name, p => p.Value!.Value, StringComparer.Ordinal);
        var results = new List<PlotCurveResult>();
        foreach (var curve in document.Curves)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(curve.Raw)) { results.Add(new(curve.Id, "", "", [], "Nhập hàm để vẽ.", 0, 0, [], false, [], null)); continue; }
            try
            {
                var interpretations = PlotInterpretationSet.Analyze(curve.Raw, token);
                var readings = interpretations.Select(i => new PlotReading(i.Id, i.Kind, i.Raw, i.Expression.MathMl, i.Expression.Latex)).ToArray();
                var chosen = curve.InterpretedRaw == null
                    ? interpretations.FirstOrDefault(i => i.Kind == "direct")
                    : interpretations.FirstOrDefault(i => i.Kind == curve.InterpretationKind && i.Raw == curve.InterpretedRaw);
                if (chosen == null)
                {
                    if (interpretations.Count == 0) _ = PlotExpression.Parse(curve.Raw, token);
                    var choiceProblem = interpretations.Any(i => i.Kind == "repair")
                        ? "Có đề nghị sửa nhưng Locus chưa áp dụng. Chọn đề nghị nếu đúng ý bạn."
                        : "Biểu thức còn mơ hồ. Chọn một cách hiểu trước khi vẽ.";
                    results.Add(new(curve.Id, "", "", [], choiceProblem, 0, curve.Raw.Length, [], false, readings, null));
                    continue;
                }
                var expression = chosen.Expression;
                var missing = expression.Parameters.Where(p => !values.ContainsKey(p)).ToArray();
                string? problem = missing.Length > 0 ? $"Cần giá trị cho {missing.Length} tham số: {string.Join(", ", missing.Take(5))}{(missing.Length > 5 ? "…" : "")}" : null;
                double left = Math.Max(document.Viewport.Left, curve.DomainMin ?? document.Viewport.Left), right = Math.Min(document.Viewport.Right, curve.DomainMax ?? document.Viewport.Right);
                var sample = problem == null && curve.Visible && left < right ? PlotSampler.Sample(expression.Bind(values), left, right, document.Viewport.Bottom, document.Viewport.Top, token) : null;
                results.Add(new(curve.Id, expression.MathMl, expression.Latex, expression.Parameters.ToArray(), problem, 0, curve.Raw.Length, sample?.Paths.ToArray() ?? [], sample?.Limited ?? false, readings, chosen.Id));
            }
            catch (PlotParseException ex) { results.Add(new(curve.Id, "", "", [], ex.Message, ex.Start, ex.End, [], false, [], null)); }
        }
        return new(request.Version, results.ToArray(), PlotSvg.Render(document, results));
    }
}

public static class PlotSvg
{
    public const int Width = 1000, Height = 640;
    public static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static string E(string value) => SecurityElement.Escape(value) ?? "";
    public static string Render(PlotDocument document, IEnumerable<PlotCurveResult> curves)
    {
        var v = document.Viewport; var axes = document.Axes ?? new();
        double X(double x) => (x - v.Left) / (v.Right - v.Left) * Width;
        double Y(double y) => (v.Top - y) / (v.Top - v.Bottom) * Height;
        string Line(double x1, double y1, double x2, double y2, string color, double width = 1) => $"<path d=\"M{Number(x1)} {Number(y1)}L{Number(x2)} {Number(y2)}\" stroke=\"{color}\" stroke-width=\"{Number(width)}\"/>";
        var svg = new StringBuilder($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\" viewBox=\"0 0 {Width} {Height}\" data-background=\"white\" role=\"img\" aria-label=\"Đồ thị hàm số\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/><defs><clipPath id=\"plot-clip\"><rect width=\"{Width}\" height=\"{Height}\"/></clipPath></defs>");
        double Step(double size) { double power = Math.Pow(10, Math.Floor(Math.Log10(size / 10))); double n = size / 10 / power; return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * power; }
        double sx = Step(v.Right - v.Left), sy = Step(v.Top - v.Bottom);
        svg.Append("<g font-family=\"Arial,sans-serif\" font-size=\"13\" fill=\"#64706c\">");
        for (double x = Math.Ceiling(v.Left / sx) * sx, count = 0; x <= v.Right && count < 32; x += sx, count++)
        {
            if (axes.Grid) svg.Append(Line(X(x), 0, X(x), Height, "#e8eeeb"));
            if (axes.Axes) svg.Append($"<text x=\"{Number(X(x) + 4)}\" y=\"{Number(Math.Clamp(Y(0) + 17, 18, Height - 8))}\">{E(x.ToString("G5", CultureInfo.InvariantCulture))}</text>");
        }
        for (double y = Math.Ceiling(v.Bottom / sy) * sy, count = 0; y <= v.Top && count < 32; y += sy, count++)
        {
            if (axes.Grid) svg.Append(Line(0, Y(y), Width, Y(y), "#e8eeeb"));
            if (axes.Axes && Math.Abs(y) > sy / 100) svg.Append($"<text x=\"{Number(Math.Clamp(X(0) + 6, 6, Width - 58))}\" y=\"{Number(Y(y) - 5)}\">{E(y.ToString("G5", CultureInfo.InvariantCulture))}</text>");
        }
        if (axes.Axes)
        {
            if (v.Left <= 0 && v.Right >= 0) svg.Append(Line(X(0), 0, X(0), Height, "#82928b", 1.3));
            if (v.Bottom <= 0 && v.Top >= 0) svg.Append(Line(0, Y(0), Width, Y(0), "#82928b", 1.3));
            svg.Append($"<text x=\"970\" y=\"{Number(Math.Clamp(Y(0) - 10, 20, 620))}\">{E(axes.XLabel)}</text><text x=\"{Number(Math.Clamp(X(0) + 10, 10, 970))}\" y=\"20\">{E(axes.YLabel)}</text>");
        }
        svg.Append("</g><g clip-path=\"url(#plot-clip)\" fill=\"none\" stroke-linejoin=\"round\" stroke-linecap=\"round\">");
        foreach (var result in curves)
        {
            var curve = document.Curves.First(c => c.Id == result.Id);
            foreach (var path in result.Paths)
            {
                if (path.Length < 2) continue;
                svg.Append($"<path data-curve=\"{E(curve.Id)}\" stroke=\"{curve.Color}\" stroke-width=\"{Number(curve.Width)}\" stroke-dasharray=\"{(curve.LineStyle == "dashed" ? "10 6" : curve.LineStyle == "dotted" ? "2 6" : "none")}\" d=\"");
                for (int i = 0; i < path.Length; i++) svg.Append($"{(i == 0 ? "M" : "L")}{Number(X(path[i][0]))} {Number(Y(path[i][1]))}");
                svg.Append("\"/>");
            }
        }
        return svg.Append("</g></svg>").ToString();
    }
}
