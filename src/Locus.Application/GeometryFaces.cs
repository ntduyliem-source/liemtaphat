namespace Locus.Application;

/// <summary>Triangulates a simple planar face in its own plane, independently of the camera.</summary>
internal static class GeometryFaces
{
    internal sealed record Mesh(int[][] Triangles, string? Problem = null);
    private readonly record struct Point(double X, double Y);

    internal static Mesh Triangulate(IReadOnlyList<Position> vertices)
    {
        Mesh Invalid(string message) => new([], message);
        if (vertices.Count < 3) return Invalid("Mặt / đa giác cần ít nhất ba đỉnh.");
        var origin = vertices[0];
        double extent = vertices.Max(p => (p - origin).Length);
        double epsilon = Math.Max(1e-12, extent * 1e-9), areaEpsilon = epsilon * extent;
        var normal = new Position();
        for (int i = 1; i + 1 < vertices.Count; i++)
        {
            var candidate = (vertices[i] - origin).Cross(vertices[i + 1] - origin);
            if (candidate.Length > normal.Length) normal = candidate;
        }
        if (normal.Length <= areaEpsilon) return Invalid("Mặt / đa giác suy biến: các đỉnh thẳng hàng.");
        normal *= 1 / normal.Length;
        if (vertices.Any(p => Math.Abs((p - origin).Dot(normal)) > epsilon))
            return Invalid("Mặt không còn đồng phẳng; chỉnh lại đỉnh trước khi xuất.");

        // Drop the dominant normal coordinate to keep a well-conditioned 2D projection.
        int drop = Math.Abs(normal.X) >= Math.Abs(normal.Y) && Math.Abs(normal.X) >= Math.Abs(normal.Z) ? 0 : Math.Abs(normal.Y) >= Math.Abs(normal.Z) ? 1 : 2;
        var points = vertices.Select(p => { var v = p - origin; return drop == 0 ? new Point(v.Y, v.Z) : drop == 1 ? new Point(v.X, v.Z) : new Point(v.X, v.Y); }).ToArray();
        double Cross(int a, int b, int c) => (points[b].X - points[a].X) * (points[c].Y - points[a].Y) - (points[b].Y - points[a].Y) * (points[c].X - points[a].X);
        bool OnSegment(int a, int b, int p) => Math.Abs(Cross(a, b, p)) <= areaEpsilon &&
            points[p].X >= Math.Min(points[a].X, points[b].X) - epsilon && points[p].X <= Math.Max(points[a].X, points[b].X) + epsilon &&
            points[p].Y >= Math.Min(points[a].Y, points[b].Y) - epsilon && points[p].Y <= Math.Max(points[a].Y, points[b].Y) + epsilon;
        for (int i = 0; i < points.Length; i++)
        {
            for (int j = i + 1; j < points.Length; j++)
                if ((vertices[i] - vertices[j]).Length <= epsilon) return Invalid("Mặt / đa giác có đỉnh trùng; chỉnh lại các đỉnh trước khi xuất.");
            int next = (i + 1) % points.Length;
            for (int j = i + 1; j < points.Length; j++)
            {
                int otherNext = (j + 1) % points.Length;
                if (next == j || otherNext == i) continue;
                double a = Cross(i, next, j), b = Cross(i, next, otherNext), c = Cross(j, otherNext, i), d = Cross(j, otherNext, next);
                bool opposite(double x, double y) => x > areaEpsilon && y < -areaEpsilon || x < -areaEpsilon && y > areaEpsilon;
                if (opposite(a, b) && opposite(c, d) || OnSegment(i, next, j) || OnSegment(i, next, otherNext) || OnSegment(j, otherNext, i) || OnSegment(j, otherNext, next))
                    return Invalid("Mặt / đa giác tự cắt; chỉnh lại thứ tự các đỉnh trước khi xuất.");
            }
        }

        var remaining = Enumerable.Range(0, points.Length).ToList();
        // Collinear boundary points are retained in the document but need no triangle of their own.
        for (int i = 0; remaining.Count > 3 && i < remaining.Count;)
        {
            int before = remaining[(i + remaining.Count - 1) % remaining.Count], current = remaining[i], after = remaining[(i + 1) % remaining.Count];
            if (OnSegment(before, after, current)) { remaining.RemoveAt(i); i = 0; }
            else i++;
        }
        double area = 0;
        for (int i = 0; i < remaining.Count; i++)
        {
            var a = points[remaining[i]]; var b = points[remaining[(i + 1) % remaining.Count]];
            area += a.X * b.Y - a.Y * b.X;
        }
        if (Math.Abs(area) <= areaEpsilon) return Invalid("Mặt / đa giác suy biến: diện tích bằng không.");
        int orientation = Math.Sign(area);
        var triangles = new List<int[]>();
        while (remaining.Count > 3)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                if (Cross(a, b, c) * orientation <= areaEpsilon) continue;
                if (remaining.Any(p => p != a && p != b && p != c && Cross(a, b, p) * orientation >= -areaEpsilon && Cross(b, c, p) * orientation >= -areaEpsilon && Cross(c, a, p) * orientation >= -areaEpsilon)) continue;
                triangles.Add([a, b, c]); remaining.RemoveAt(i); clipped = true; break;
            }
            if (!clipped) return Invalid("Chưa dựng được mặt từ các đỉnh này; chỉnh lại hình trước khi xuất.");
        }
        triangles.Add(remaining.ToArray());
        return new(triangles.ToArray());
    }
}
