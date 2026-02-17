using System.Numerics;

namespace Mobsub.AutomationBridge.Core.Geometry;

public readonly record struct CubicBezier(Vector2 P0, Vector2 P1, Vector2 P2, Vector2 P3)
{
    public Vector2 Evaluate(float t)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        return uuu * P0 +
               3f * uu * t * P1 +
               3f * u * tt * P2 +
               ttt * P3;
    }

    public void Flatten(double tolerance, List<Vector2> points)
    {
        if (points is null)
            throw new ArgumentNullException(nameof(points));

        double tol2 = tolerance * tolerance;

        // Iterative subdivision (stack) to avoid recursion.
        Span<CubicBezier> stack = stackalloc CubicBezier[64];
        int sp = 0;
        stack[sp++] = this;

        // Ensure start point present.
        if (points.Count == 0)
            points.Add(P0);

        while (sp > 0)
        {
            var bez = stack[--sp];
            if (IsFlatEnough(bez, tol2))
            {
                points.Add(bez.P3);
                continue;
            }

            Subdivide(bez, out var left, out var right);

            // Depth-first: push right then left.
            if (sp + 2 > stack.Length)
            {
                // Rare: fall back to recursion by adding more stack capacity via List.
                FlattenSlow(left, tol2, points);
                FlattenSlow(right, tol2, points);
                continue;
            }

            stack[sp++] = right;
            stack[sp++] = left;
        }
    }

    private static bool IsFlatEnough(in CubicBezier b, double tol2)
    {
        // Distance from control points to the baseline P0-P3.
        // Using squared distances avoids sqrt.
        var a = b.P0;
        var d = b.P3;
        var v = d - a;
        float vv = Vector2.Dot(v, v);
        if (vv == 0)
        {
            // Degenerate: treat as flat if controls are close to P0.
            return Vector2.DistanceSquared(b.P1, a) <= (float)tol2 &&
                   Vector2.DistanceSquared(b.P2, a) <= (float)tol2;
        }

        float invVv = 1f / vv;
        float d1 = DistancePointToLineSquared(b.P1, a, v, invVv);
        float d2 = DistancePointToLineSquared(b.P2, a, v, invVv);
        return d1 <= (float)tol2 && d2 <= (float)tol2;
    }

    private static float DistancePointToLineSquared(Vector2 p, Vector2 a, Vector2 v, float invVv)
    {
        // Project (p-a) onto v to get the closest point on infinite line.
        var ap = p - a;
        float t = Vector2.Dot(ap, v) * invVv;
        var proj = a + t * v;
        return Vector2.DistanceSquared(p, proj);
    }

    private static void Subdivide(in CubicBezier b, out CubicBezier left, out CubicBezier right)
    {
        // De Casteljau at t=0.5.
        var p01 = (b.P0 + b.P1) * 0.5f;
        var p12 = (b.P1 + b.P2) * 0.5f;
        var p23 = (b.P2 + b.P3) * 0.5f;

        var p012 = (p01 + p12) * 0.5f;
        var p123 = (p12 + p23) * 0.5f;

        var p0123 = (p012 + p123) * 0.5f;

        left = new CubicBezier(b.P0, p01, p012, p0123);
        right = new CubicBezier(p0123, p123, p23, b.P3);
    }

    private static void FlattenSlow(in CubicBezier b, double tol2, List<Vector2> points)
    {
        if (IsFlatEnough(b, tol2))
        {
            points.Add(b.P3);
            return;
        }

        Subdivide(b, out var left, out var right);
        FlattenSlow(left, tol2, points);
        FlattenSlow(right, tol2, points);
    }
}
