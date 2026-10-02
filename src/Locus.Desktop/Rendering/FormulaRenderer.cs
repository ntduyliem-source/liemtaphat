using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Locus.Core;
using Locus.Core.Export;

namespace Locus.Desktop.Rendering;

public sealed record RenderOptions(double FontSize = 40, double PixelScale = 2, bool WhiteBackground = false)
{
    public void Validate()
    {
        if (double.IsNaN(FontSize) || double.IsInfinity(FontSize) || FontSize < 16 || FontSize > 96 ||
            double.IsNaN(PixelScale) || double.IsInfinity(PixelScale) || PixelScale < 1 || PixelScale > 4)
            throw new ArgumentOutOfRangeException(nameof(FontSize), "Cỡ chữ hoặc độ phân giải ngoài phạm vi hỗ trợ.");
    }
}

public sealed record VectorPart(string NodeId, string Role, Geometry Geometry);

/// <summary>One immutable scene drives WPF preview, SVG and PNG. No parsing or source rewriting.</summary>
public sealed class FormulaScene
{
    public const string RendererVersion = "locus-vector/0.2";
    public string CandidateId { get; }
    public string Latex { get; }
    public double Width { get; }
    public double Height { get; }
    public RenderOptions Options { get; }
    public IReadOnlyList<VectorPart> Parts { get; }
    public DrawingGroup Drawing { get; }

    internal FormulaScene(Candidate candidate, double width, double height, RenderOptions options, IEnumerable<VectorPart> parts)
    {
        CandidateId = candidate.Id; Latex = CandidateExporter.ToLatex(candidate);
        Width = Math.Ceiling(width); Height = Math.Ceiling(height); Options = options;
        Parts = Array.AsReadOnly(parts.ToArray());
        if (Width > 12000 || Height > 12000 || Width * Height * options.PixelScale * options.PixelScale > 24_000_000)
            throw new InvalidOperationException("Công thức quá lớn để xuất ảnh. Giảm cỡ chữ hoặc tách thành các công thức nhỏ.");
        Drawing = new DrawingGroup();
        using (var context = Drawing.Open())
        {
            context.DrawRectangle(options.WhiteBackground ? Brushes.White : Brushes.Transparent, null, new Rect(0, 0, Width, Height));
            foreach (var part in Parts) context.DrawGeometry(Brushes.Black, null, part.Geometry);
        }
        Drawing.Freeze();
    }

    public DrawingImage Preview { get { var image = new DrawingImage(Drawing); image.Freeze(); return image; } }

    public string ToSvg()
    {
        XNamespace ns = "http://www.w3.org/2000/svg";
        var root = new XElement(ns + "svg", new XAttribute("width", N(Width)), new XAttribute("height", N(Height)),
            new XAttribute("viewBox", $"0 0 {N(Width)} {N(Height)}"), new XAttribute("role", "img"),
            new XAttribute("data-candidate-id", CandidateId), new XAttribute("data-renderer", RendererVersion),
            new XElement(ns + "title", Latex));
        if (Options.WhiteBackground) root.Add(new XElement(ns + "rect", new XAttribute("width", "100%"), new XAttribute("height", "100%"), new XAttribute("fill", "white")));
        foreach (var part in Parts)
            root.Add(new XElement(ns + "path", new XAttribute("d", SvgPath(part.Geometry)), new XAttribute("fill", "black"),
                new XAttribute("fill-rule", part.Geometry.GetFlattenedPathGeometry().FillRule == FillRule.EvenOdd ? "evenodd" : "nonzero"),
                new XAttribute("data-node-id", part.NodeId), new XAttribute("data-role", part.Role)));
        return new XDocument(root).ToString(SaveOptions.DisableFormatting);
    }

    public BitmapSource Bitmap(bool whiteBackground = false)
    {
        var scale = Options.PixelScale;
        var target = new RenderTargetBitmap((int)Math.Ceiling(Width * scale), (int)Math.Ceiling(Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            if (whiteBackground) context.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, Height));
            context.DrawDrawing(Drawing);
        }
        target.Render(visual); target.Freeze(); return target;
    }

    public byte[] ToPng()
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(Bitmap()));
        using var stream = new MemoryStream(); encoder.Save(stream); return stream.ToArray();
    }

    private static string SvgPath(Geometry geometry)
    {
        // Flatten only for interchange; applying the returned transform prevents double/missing offsets.
        var path = geometry.GetFlattenedPathGeometry(0.025, ToleranceType.Absolute);
        var builder = new StringBuilder();
        void Point(char command, Point point)
        {
            point = path.Transform?.Transform(point) ?? point;
            builder.Append(command).Append(N(point.X)).Append(' ').Append(N(point.Y));
        }
        foreach (var figure in path.Figures)
        {
            Point('M', figure.StartPoint);
            foreach (var segment in figure.Segments)
            {
                if (segment is PolyLineSegment poly) foreach (var point in poly.Points) Point('L', point);
                else if (segment is LineSegment line) Point('L', line.Point);
                else throw new InvalidOperationException("Unexpected non-linear segment after flattening.");
            }
            if (figure.IsClosed) builder.Append('Z');
        }
        return builder.ToString();
    }
    private static string N(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}

public static class FormulaRenderer
{
    public static FormulaScene Render(Candidate candidate, RenderOptions? options = null)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        options ??= new RenderOptions(); options.Validate();
        int count = 0;
        var box = Build(candidate.Document.Root, options.FontSize, 0, ref count);
        const double padding = 12;
        return new FormulaScene(candidate, box.Width + padding * 2, box.Height + padding * 2, options,
            Move(box.Parts, padding, padding));
    }

    private sealed record Box(double Width, double Height, double Baseline, IReadOnlyList<VectorPart> Parts);

    private static Box Build(MathNode node, double size, int depth, ref int count)
    {
        if (++count > 2048 || depth > 64) throw new InvalidOperationException("Công thức vượt giới hạn dựng hình của bản alpha.");
        var s = Math.Max(size, 6);
        switch (node.Type)
        {
            case "Number": return Text(node.Value!, false, s, node.Id);
            case "Symbol": return Text(node.Name!, true, s, node.Id);
            case "Greek": return Text(node.Name!, true, s, node.Id);
            case "ChemElement": case "Unit": return Text(node.Name!, false, s, node.Id);
            case "ChemConcat": case "ChemCoefficient": case "Quantity":
                return Row(new[]{Build(node.Children[0],s,depth+1,ref count),Build(node.Children[1],s,depth+1,ref count)},node.Type=="ChemConcat"?0:s*0.16);
            case "ChemGroup":
                return Group(Build(node.Children[0],s,depth+1,ref count),s,node.Id,node.Name=="[]"?"[]":"()");
            case "ChemSubscript": case "Subscript":
                return Script(Build(node.Children[0],s,depth+1,ref count),Build(node.Children[1],s*.65,depth+1,ref count),s,false);
            case "ChemCharge":
                return Script(Build(node.Children[0],s,depth+1,ref count),Text(node.Value!,false,s*.65,node.Id),s,true);
            case "ChemSum": case "ChemReaction":
                return Row(new[]{Build(node.Children[0],s,depth+1,ref count),Text(node.Type=="ChemSum"?"+":node.Operator=="arrow"?"→":"⇌",false,s,node.Id),Build(node.Children[1],s,depth+1,ref count)},s*.18);
            case "Vector":
            {
                var basis=Build(node.Children[0],s,depth+1,ref count);double top=s*.24,width=Math.Max(basis.Width,s*.6),line=Math.Max(.8,s*.035);
                var path=new StreamGeometry();using(var dc=path.Open()){
                    dc.BeginFigure(new Point(0,top*.4),false,false);dc.LineTo(new Point(width,top*.4),true,false);
                    dc.BeginFigure(new Point(width-s*.14,0),false,false);dc.LineTo(new Point(width,top*.4),true,false);dc.LineTo(new Point(width-s*.14,top*.8),true,false);
                }
                var parts=Move(basis.Parts,(width-basis.Width)/2,top).ToList();parts.Add(new(node.Id,"vector-arrow",path.GetWidenedPathGeometry(new Pen(Brushes.Black,line))));
                return new(width,top+basis.Height,top+basis.Baseline,parts);
            }
            case "Unary":
            {
                var child = Build(node.Children[0], s, depth + 1, ref count);
                if (Precedence(node.Children[0]) < 3 || node.Children[0].Type == "Unary") child = Group(child, s, node.Id);
                return Row(new[] { Text(node.Operator == "minus" ? "−" : "+", false, s, node.Id), child }, s * 0.06);
            }
            case "Binary":
            {
                if (node.Operator == "divide")
                {
                    var numerator = Build(node.Children[0], s * 0.9, depth + 1, ref count);
                    var denominator = Build(node.Children[1], s * 0.9, depth + 1, ref count);
                    double gap = s * 0.12, thickness = Math.Max(0.8, s * 0.042), width = Math.Max(numerator.Width, denominator.Width) + s * 0.3;
                    double lineY = numerator.Height + gap, denominatorY = lineY + thickness + gap;
                    var parts = Move(numerator.Parts, (width - numerator.Width) / 2, 0).ToList();
                    parts.Add(new(node.Id, "fraction-rule", new RectangleGeometry(new Rect(0, lineY, width, thickness))));
                    parts.AddRange(Move(denominator.Parts, (width - denominator.Width) / 2, denominatorY));
                    return new(width, denominatorY + denominator.Height, lineY + thickness / 2 + s * 0.24, parts);
                }
                var left = Build(node.Children[0], s, depth + 1, ref count);
                var right = Build(node.Children[1], s, depth + 1, ref count);
                int p = Precedence(node);
                if (Precedence(node.Children[0]) < p) left = Group(left, s, node.Id);
                if (Precedence(node.Children[1]) < p ||
                    (Precedence(node.Children[1]) == p && !IsFraction(node.Children[1])) || node.Children[1].Type == "Unary") right = Group(right, s, node.Id);
                return Row(new[] { left, Text(node.Operator switch { "add" => "+", "subtract" => "−", "multiply" => "·", _ => throw new ArgumentException("Unknown operator") }, false, s, node.Id), right }, s * 0.12);
            }
            case "Power":
            {
                var basis = Build(node.Children[0], s, depth + 1, ref count);
                if (Precedence(node.Children[0]) <= 4) basis = Group(basis, s, node.Id);
                var exponent = Build(node.Children[1], s * 0.65, depth + 1, ref count);
                double raise = Math.Max(s * 0.32, exponent.Height - basis.Baseline * 0.35);
                double baseY = Math.Max(0, raise), expY = Math.Max(0, baseY + basis.Baseline - s * 0.60 - exponent.Baseline);
                var parts = Move(basis.Parts, 0, baseY).ToList();
                parts.AddRange(Move(exponent.Parts, basis.Width + s * 0.045, expY));
                return new(basis.Width + s * 0.045 + exponent.Width, Math.Max(baseY + basis.Height, expY + exponent.Height), baseY + basis.Baseline, parts);
            }
            case "Sqrt":
            {
                var radicand = Build(node.Children[0], s, depth + 1, ref count);
                double top = s * 0.12, width = radicand.Width + s * 0.62, childX = s * 0.56, line = Math.Max(0.8, s * 0.035);
                double bottom = top + radicand.Height, shoulder = top + radicand.Baseline * 0.7;
                var path = new StreamGeometry();
                using (var dc = path.Open())
                {
                    dc.BeginFigure(new Point(s * 0.02, shoulder), false, false);
                    dc.PolyLineTo(new[] { new Point(s * 0.16, shoulder - s * 0.08), new Point(s * 0.29, bottom - s * 0.06), new Point(s * 0.50, line), new Point(width, line) }, true, false);
                }
                var parts = Move(radicand.Parts, childX, top + line * 2).ToList();
                parts.Add(new(node.Id, "radical-rule", path.GetWidenedPathGeometry(new Pen(Brushes.Black, line) { LineJoin = PenLineJoin.Miter })));
                return new(width + line, top + line * 2 + radicand.Height, top + line * 2 + radicand.Baseline, parts);
            }
            case "Relation":
            {
                var left = Build(node.Children[0], s, depth + 1, ref count);
                var right = Build(node.Children[1], s, depth + 1, ref count);
                if (node.Children[0].Type == "Relation") left = Group(left, s, node.Id);
                if (node.Children[1].Type == "Relation") right = Group(right, s, node.Id);
                var op = node.Operator switch { "eq" => "=", "lt" => "<", "gt" => ">", "le" => "≤", "ge" => "≥", _ => throw new ArgumentException("Unknown relation") };
                return Row(new[] { left, Text(op, false, s, node.Id), right }, s * 0.18);
            }
            default: throw new ArgumentException("Unsupported mathematical node: " + node.Type);
        }
    }

    private static Box Text(string value, bool italic, double size, string id)
    {
        var formatted = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Cambria Math"), italic ? FontStyles.Italic : FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Brushes.Black, 1);
        var geometry = formatted.BuildGeometry(new Point());
        var bounds = geometry.Bounds;
        double x = bounds.IsEmpty ? 0 : Math.Max(0, -bounds.Left), y = bounds.IsEmpty ? 0 : Math.Max(0, -bounds.Top);
        var moved = Move(new[] { new VectorPart(id, "glyph:" + value, geometry) }, x, y).ToArray();
        return new(Math.Max(formatted.WidthIncludingTrailingWhitespace + x, bounds.IsEmpty ? 0 : bounds.Right + x),
            Math.Max(formatted.Height + y, bounds.IsEmpty ? 0 : bounds.Bottom + y), formatted.Baseline + y, moved);
    }

    private static Box Script(Box basis,Box script,double size,bool upper)
    {
        double y=upper?basis.Baseline-size*.60-script.Baseline:basis.Baseline+size*.30-script.Baseline;
        double top=Math.Min(0,y);var parts=Move(basis.Parts,0,-top).ToList();parts.AddRange(Move(script.Parts,basis.Width+size*.045,y-top));
        return new(basis.Width+size*.045+script.Width,Math.Max(basis.Height,y+script.Height)-top,basis.Baseline-top,parts);
    }

    private static Box Group(Box box, double size, string id,string delimiters="()")
    {
        // Stretch the parentheses as vector outlines to enclose fractions and other tall groups.
        var left = Text(delimiters[0].ToString(), false, size, id); var right = Text(delimiters[1].ToString(), false, size, id);
        double targetHeight = Math.Max(box.Height, left.Height), scale = targetHeight / left.Height;
        Box Stretch(Box source) => new(source.Width, targetHeight, box.Baseline,
            source.Parts.Select(p => new VectorPart(p.NodeId, "group", Transform(p.Geometry, new ScaleTransform(1, scale)))).ToArray());
        return Row(new[] { Stretch(left), box, Stretch(right) }, size * 0.04);
    }

    private static Box Row(IReadOnlyList<Box> boxes, double gap)
    {
        double baseline = boxes.Max(b => b.Baseline), height = boxes.Max(b => baseline - b.Baseline + b.Height), x = 0;
        var parts = new List<VectorPart>();
        foreach (var box in boxes) { parts.AddRange(Move(box.Parts, x, baseline - box.Baseline)); x += box.Width + gap; }
        return new(Math.Max(0, x - gap), height, baseline, parts);
    }

    private static Geometry Transform(Geometry geometry, Transform transform)
    {
        var result = geometry.Clone();
        var group = new TransformGroup();
        if (geometry.Transform != null) group.Children.Add(geometry.Transform);
        group.Children.Add(transform); result.Transform = group;
        result.Freeze(); return result;
    }
    private static IEnumerable<VectorPart> Move(IEnumerable<VectorPart> parts, double x, double y) =>
        parts.Select(p => new VectorPart(p.NodeId, p.Role, Transform(p.Geometry, new TranslateTransform(x, y))));
    private static bool IsFraction(MathNode node) => node.Type == "Binary" && node.Operator == "divide";
    private static int Precedence(MathNode node) => node.Type switch { "Relation" => 0, "Binary" => node.Operator is "add" or "subtract" ? 1 : 2, "Unary" => 3, "Power" => 4, _ => 5 };
}
