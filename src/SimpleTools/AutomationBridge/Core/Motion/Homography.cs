using System.Numerics;

namespace Mobsub.AutomationBridge.Core.Motion;

public static class Homography
{
    public static bool TryCreateSquareToQuad(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        out Matrix4x4 m)
    {
        // Maps:
        // (0,0)->p0, (1,0)->p1, (1,1)->p2, (0,1)->p3
        // Based on the well-known closed-form solution (Graphics Gems).
        double x0 = p0.X, y0 = p0.Y;
        double x1 = p1.X, y1 = p1.Y;
        double x2 = p2.X, y2 = p2.Y;
        double x3 = p3.X, y3 = p3.Y;

        double dx1 = x1 - x2;
        double dx2 = x3 - x2;
        double dx3 = x0 - x1 + x2 - x3;
        double dy1 = y1 - y2;
        double dy2 = y3 - y2;
        double dy3 = y0 - y1 + y2 - y3;

        double a, b, c, d, e, f, g, h;
        c = x0;
        f = y0;

        if (dx3 == 0 && dy3 == 0)
        {
            // Affine
            a = x1 - x0;
            b = x3 - x0;
            d = y1 - y0;
            e = y3 - y0;
            g = 0;
            h = 0;
        }
        else
        {
            double det = dx1 * dy2 - dx2 * dy1;
            if (det == 0)
            {
                m = default;
                return false;
            }

            g = (dx3 * dy2 - dx2 * dy3) / det;
            h = (dx1 * dy3 - dx3 * dy1) / det;
            a = x1 - x0 + g * x1;
            b = x3 - x0 + h * x3;
            d = y1 - y0 + g * y1;
            e = y3 - y0 + h * y3;
        }

        // Row-vector convention with Vector4.Transform(v, M) in System.Numerics:
        // For input (u,v,0,1):
        // X = u*M11 + v*M21 + M41 = a*u + b*v + c
        // Y = u*M12 + v*M22 + M42 = d*u + e*v + f
        // W = u*M14 + v*M24 + M44 = g*u + h*v + 1
        m = new Matrix4x4(
            (float)a, (float)d, 0, (float)g,
            (float)b, (float)e, 0, (float)h,
            0, 0, 1, 0,
            (float)c, (float)f, 0, 1);
        return true;
    }

    public static bool TryCreateQuadToSquare(
        Vector2 p0,
        Vector2 p1,
        Vector2 p2,
        Vector2 p3,
        out Matrix4x4 m)
    {
        if (!TryCreateSquareToQuad(p0, p1, p2, p3, out var sq2q))
        {
            m = default;
            return false;
        }

        if (!Matrix4x4.Invert(sq2q, out m))
            return false;
        return true;
    }

    public static Vector2 TransformPoint(in Matrix4x4 m, Vector2 p)
    {
        float x = p.X;
        float y = p.Y;

        float tx = x * m.M11 + y * m.M21 + m.M41;
        float ty = x * m.M12 + y * m.M22 + m.M42;
        float tw = x * m.M14 + y * m.M24 + m.M44;

        if (tw == 0)
            return new Vector2(float.NaN, float.NaN);

        float invW = 1f / tw;
        return new Vector2(tx * invW, ty * invW);
    }
}
