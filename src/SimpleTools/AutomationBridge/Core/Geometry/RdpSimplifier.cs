using System.Numerics;

namespace Mobsub.AutomationBridge.Core.Geometry;

public static class RdpSimplifier
{
    public static void Simplify(ReadOnlySpan<Vector2> points, double tolerance, List<Vector2> output)
    {
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        output.Clear();

        if (points.Length == 0)
            return;
        if (points.Length <= 2 || tolerance <= 0)
        {
            for (int i = 0; i < points.Length; i++)
                output.Add(points[i]);
            return;
        }

        double tol2 = tolerance * tolerance;

        int n = points.Length;
        var keep = new bool[n];
        keep[0] = true;
        keep[n - 1] = true;

        Span<(int A, int B)> stack = stackalloc (int, int)[64];
        int sp = 0;
        stack[sp++] = (0, n - 1);

        while (sp > 0)
        {
            var (a, b) = stack[--sp];
            if (b <= a + 1)
                continue;

            int maxIdx = -1;
            double maxDist2 = 0;

            var pA = points[a];
            var pB = points[b];
            var v = pB - pA;
            float vv = Vector2.Dot(v, v);

            for (int i = a + 1; i < b; i++)
            {
                double dist2 = vv == 0
                    ? Vector2.DistanceSquared(points[i], pA)
                    : DistancePointToSegmentSquared(points[i], pA, pB, v, 1f / vv);

                if (dist2 > maxDist2)
                {
                    maxDist2 = dist2;
                    maxIdx = i;
                }
            }

            if (maxIdx >= 0 && maxDist2 > tol2)
            {
                keep[maxIdx] = true;

                if (sp + 2 > stack.Length)
                {
                    // Fallback: process recursively in worst-case spikes.
                    SimplifySegment(points, a, maxIdx, tol2, keep);
                    SimplifySegment(points, maxIdx, b, tol2, keep);
                    continue;
                }

                stack[sp++] = (a, maxIdx);
                stack[sp++] = (maxIdx, b);
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (keep[i])
                output.Add(points[i]);
        }
    }

    private static void SimplifySegment(ReadOnlySpan<Vector2> points, int a, int b, double tol2, bool[] keep)
    {
        if (b <= a + 1)
            return;

        int maxIdx = -1;
        double maxDist2 = 0;

        var pA = points[a];
        var pB = points[b];
        var v = pB - pA;
        float vv = Vector2.Dot(v, v);

        for (int i = a + 1; i < b; i++)
        {
            double dist2 = vv == 0
                ? Vector2.DistanceSquared(points[i], pA)
                : DistancePointToSegmentSquared(points[i], pA, pB, v, 1f / vv);

            if (dist2 > maxDist2)
            {
                maxDist2 = dist2;
                maxIdx = i;
            }
        }

        if (maxIdx >= 0 && maxDist2 > tol2)
        {
            keep[maxIdx] = true;
            SimplifySegment(points, a, maxIdx, tol2, keep);
            SimplifySegment(points, maxIdx, b, tol2, keep);
        }
    }

    private static double DistancePointToSegmentSquared(Vector2 p, Vector2 a, Vector2 b, Vector2 v, float invVv)
    {
        var ap = p - a;
        float t = Vector2.Dot(ap, v) * invVv;
        if (t <= 0)
            return Vector2.DistanceSquared(p, a);
        if (t >= 1)
            return Vector2.DistanceSquared(p, b);
        var proj = a + t * v;
        return Vector2.DistanceSquared(p, proj);
    }
}
