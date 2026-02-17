using System.Numerics;

namespace Mobsub.AutomationBridge.Core.Motion;

public readonly record struct PerspectiveTags(
    int Align,
    Vector2 Position,
    Vector2 Origin,
    double AngleZ,
    double AngleX,
    double AngleY,
    double ScaleX,
    double ScaleY,
    double ShearX,
    double ShearY);

public static class PerspectiveTagsSolver
{
    public const double DefaultScreenZ = 312.5;

    private const double MinSize = 0.01;
    private const double Eps = 1e-12;

    // Index 0 unused; indices map to ASS \an 1..9.
    private static readonly double[] AnXShift = { 0, 0, 0.5, 1, 0, 0.5, 1, 0, 0.5, 1 };
    private static readonly double[] AnYShift = { 0, 1, 1, 1, 0.5, 0.5, 0.5, 0, 0, 0 };

    // orgMode:
    // 1 (or 0): keep origin as-is
    // 2: set \org to quad diagonal intersection
    // 3: choose \org trying to make \fax close to 0 (ported from arch.Perspective)
    public static bool TrySolveFromQuad(
        ReadOnlySpan<Vector2> quad,
        double width,
        double height,
        int align,
        int orgMode,
        Vector2 origin,
        double layoutScale,
        out PerspectiveTags tags,
        double screenZ = DefaultScreenZ)
    {
        tags = default;

        if (quad.Length < 4)
            return false;

        width = Math.Max(width, MinSize);
        height = Math.Max(height, MinSize);

        if ((uint)(align - 1) > 8u)
            align = 7;

        double scaledScreenZ = layoutScale * screenZ;

        var q1 = Vec2.From(quad[0]);
        var q2 = Vec2.From(quad[1]);
        var q3 = Vec2.From(quad[2]);
        var q4 = Vec2.From(quad[3]);

        // Find a parallelogram projecting to the quad.
        // Equivalent to: solve (q1 - q3) = a*(q2 - q3) + b*(q4 - q3)
        // where a=z24[1], b=z24[2] in the original script.
        if (!TrySolve2x2Columns(q2 - q3, q4 - q3, q1 - q3, out var z24))
            return false;

        var org = Vec2.From(origin);
        if (orgMode == 2)
        {
            org = DiagonalIntersection(q1, q2, q3, q4);
        }
        else if (orgMode == 3)
        {
            org = ChooseOriginForZeroFax(q1, q2, q3, q4, z24, scaledScreenZ);
        }

        // Normalize to origin.
        q1 -= org;
        q2 -= org;
        q3 -= org;
        q4 -= org;

        // Unproject the quad.
        double z1 = 1.0;
        double z2 = z24.X;
        double z4 = z24.Y;
        double z3 = (z24.X + z24.Y) - 1.0;

        var p1 = new Vec3(q1.X, q1.Y, scaledScreenZ) * z1;
        var p2 = new Vec3(q2.X, q2.Y, scaledScreenZ) * z2;
        var p3 = new Vec3(q3.X, q3.Y, scaledScreenZ) * z3;
        var p4 = new Vec3(q4.X, q4.Y, scaledScreenZ) * z4;

        // Normalize so the origin has z=scaledScreenZ.
        var c2 = p1 - p2;
        var c3 = p1 - p4;

        // Solve p1.xy = beta*c2.xy + gamma*c3.xy, then solve alpha from z.
        if (!TrySolve2x2Columns(new Vec2(c2.X, c2.Y), new Vec2(c3.X, c3.Y), new Vec2(p1.X, p1.Y), out var bg))
            return false;

        double beta = bg.X;
        double gamma = bg.Y;
        if (Math.Abs(scaledScreenZ) < Eps)
            return false;

        double alpha = (p1.Z - (beta * c2.Z) - (gamma * c3.Z)) / scaledScreenZ;
        if (Math.Abs(alpha) < Eps)
            return false;

        p1 /= alpha;
        p2 /= alpha;
        p3 /= alpha;
        p4 /= alpha;

        // Move plane so origin is at z=0.
        var shift = new Vec3(0, 0, scaledScreenZ);
        p1 -= shift;
        p2 -= shift;
        p3 -= shift;
        p4 -= shift;

        // Find rotations.
        var n = Vec3.Cross(p2 - p1, p4 - p1);

        double roty = Math.Atan2(n.X, n.Z);
        var n2 = RotateY(n, roty);
        double rotx = Math.Atan2(n2.Y, n2.Z);

        p1 = RotateY(p1, roty);
        p2 = RotateY(p2, roty);
        p3 = RotateY(p3, roty);
        p4 = RotateY(p4, roty);

        p1 = RotateX(p1, rotx);
        p2 = RotateX(p2, rotx);
        p3 = RotateX(p3, rotx);
        p4 = RotateX(p4, rotx);

        var ab = p2 - p1;
        double rotz = Math.Atan2(ab.Y, ab.X);

        p1 = RotateZ(p1, -rotz);
        p2 = RotateZ(p2, -rotz);
        p3 = RotateZ(p3, -rotz);
        p4 = RotateZ(p4, -rotz);

        // We now have a horizontal parallelogram in the 2D plane.
        ab = p2 - p1;
        var ad = p4 - p1;

        if (Math.Abs(ad.Y) < Eps)
            return false;

        double rawFax = ad.X / ad.Y;
        double quadWidth = ab.Length();
        double quadHeight = Math.Abs(ad.Y);

        if (quadWidth < Eps || quadHeight < Eps)
            return false;

        double scaleX = quadWidth / width;
        double scaleY = quadHeight / height;

        double posX = org.X + p1.X + (quadWidth * AnXShift[align]);
        double posY = org.Y + p1.Y + (quadHeight * AnYShift[align]);

        tags = new PerspectiveTags(
            Align: align,
            Position: new Vector2((float)posX, (float)posY),
            Origin: org.ToVector2(),
            AngleZ: RadToDeg(-rotz),
            AngleX: RadToDeg(rotx),
            AngleY: RadToDeg(-roty),
            ScaleX: 100 * scaleX,
            ScaleY: 100 * scaleY,
            ShearX: rawFax * scaleY / scaleX,
            ShearY: 0);

        return true;
    }

    public static void TransformRect(
        in PerspectiveTags tags,
        double width,
        double height,
        Span<Vector2> destination,
        double layoutScale,
        double screenZ = DefaultScreenZ)
    {
        if (destination.Length < 4)
            throw new ArgumentException("destination must have length >= 4.", nameof(destination));

        width = Math.Max(width, MinSize);
        height = Math.Max(height, MinSize);

        int align = tags.Align;
        if ((uint)(align - 1) > 8u)
            align = 7;

        double scaledScreenZ = layoutScale * screenZ;

        var org = Vec2.From(tags.Origin);
        var pos = Vec2.From(tags.Position);

        double sx = tags.ScaleX / 100.0;
        double sy = tags.ScaleY / 100.0;

        double z = DegToRad(-tags.AngleZ);
        double x = DegToRad(-tags.AngleX);
        double y = DegToRad(tags.AngleY);

        var shift = new Vec2(width * AnXShift[align], height * AnYShift[align]);
        var translate = pos - org;

        Span<Vec2> pts = stackalloc Vec2[4]
        {
            new(0, 0),
            new(width, 0),
            new(width, height),
            new(0, height)
        };

        for (int i = 0; i < pts.Length; i++)
        {
            // Shear
            double px = pts[i].X;
            double py = pts[i].Y;
            px = px + tags.ShearX * py;
            py = py + tags.ShearY * pts[i].X;

            // Alignment shift
            px -= shift.X;
            py -= shift.Y;

            // Scale
            px *= sx;
            py *= sy;

            // Translate relative to origin
            px += translate.X;
            py += translate.Y;

            // Rotate ZXY
            var p3 = new Vec3(px, py, 0);
            p3 = RotateZ(p3, z);
            p3 = RotateX(p3, x);
            p3 = RotateY(p3, y);

            // Project
            double w = p3.Z + scaledScreenZ;
            double proj = w == 0 ? 1 : (scaledScreenZ / w);
            double outX = proj * p3.X + org.X;
            double outY = proj * p3.Y + org.Y;

            destination[i] = new Vector2((float)outX, (float)outY);
        }
    }

    private static Vec2 ChooseOriginForZeroFax(Vec2 q1, Vec2 q2, Vec2 q3, Vec2 q4, Vec2 z24, double scaledScreenZ)
    {
        var v2 = q2 - q1;
        var v4 = q4 - q1;

        double a = (1 - z24.X) * (1 - z24.Y);
        var b = (z24.X * v2) + (z24.Y * v4) - (z24.X * z24.Y * (v2 + v4));
        double c = (z24.X * z24.Y * v2.Dot(v4)) + ((z24.X - 1) * (z24.Y - 1) * scaledScreenZ * scaledScreenZ);

        // Default o: would put \org at the diagonal intersection.
        var o = q1 - DiagonalIntersection(q1, q2, q3, q4);

        if (Math.Abs(a) < Eps)
        {
            double bb = b.Dot(b);
            if (bb > Eps)
            {
                // Equation cuts out a line. Find point closest to o.
                o = o + b * ((c - o.Dot(b)) / bb);
            }
        }
        else
        {
            // Equation cuts out a circle.
            var circleCenter = b / (2 * a);
            double sqradius = ((b.Dot(b) / (4 * a)) - c) / a;

            if (sqradius <= 0)
            {
                // Rare; best-effort fallback.
                o = circleCenter;
            }
            else
            {
                double radius = Math.Sqrt(sqradius);
                var center2t = o - circleCenter;
                double len = center2t.Length();
                if (len < Eps)
                {
                    o = circleCenter + new Vec2(radius, 0);
                }
                else
                {
                    o = circleCenter + (center2t / len) * radius;
                }
            }
        }

        return q1 - o;
    }

    private static Vec2 DiagonalIntersection(Vec2 q1, Vec2 q2, Vec2 q3, Vec2 q4)
    {
        // Intersect lines q1->q3 and q2->q4.
        // Solve: q1 + t*(q3-q1) = q4 + u*(q2-q4)
        // => t*(q3-q1) + u*(q4-q2) = (q4-q1)
        var d13 = q3 - q1;
        var d42 = q4 - q2;
        if (!TrySolve2x2Columns(d13, d42, q4 - q1, out var tu))
        {
            return (q1 + q2 + q3 + q4) / 4.0;
        }
        return q1 + (tu.X * d13);
    }

    private static bool TrySolve2x2Columns(Vec2 c1, Vec2 c2, Vec2 b, out Vec2 x)
    {
        double det = (c1.X * c2.Y) - (c2.X * c1.Y);
        if (Math.Abs(det) < Eps)
        {
            x = default;
            return false;
        }

        double inv = 1.0 / det;
        double a = ((b.X * c2.Y) - (c2.X * b.Y)) * inv;
        double d = ((c1.X * b.Y) - (b.X * c1.Y)) * inv;
        x = new Vec2(a, d);
        return true;
    }

    private static Vec3 RotateZ(Vec3 p, double phi)
    {
        double c = Math.Cos(phi);
        double s = Math.Sin(phi);
        return new Vec3((c * p.X) - (s * p.Y), (s * p.X) + (c * p.Y), p.Z);
    }

    private static Vec3 RotateX(Vec3 p, double phi)
    {
        double c = Math.Cos(phi);
        double s = Math.Sin(phi);
        return new Vec3(p.X, (c * p.Y) - (s * p.Z), (s * p.Y) + (c * p.Z));
    }

    // Matches arch.Math Matrix.rot2d(phi).onSubspace(2) applied to column vectors.
    private static Vec3 RotateY(Vec3 p, double phi)
    {
        double c = Math.Cos(phi);
        double s = Math.Sin(phi);
        return new Vec3((c * p.X) - (s * p.Z), p.Y, (s * p.X) + (c * p.Z));
    }

    private static double DegToRad(double deg) => deg * (Math.PI / 180.0);
    private static double RadToDeg(double rad) => rad * (180.0 / Math.PI);

    private readonly struct Vec2
    {
        public readonly double X;
        public readonly double Y;

        public Vec2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 From(Vector2 v) => new(v.X, v.Y);
        public Vector2 ToVector2() => new((float)X, (float)Y);

        public double Dot(Vec2 other) => (X * other.X) + (Y * other.Y);
        public double Length() => Math.Sqrt(Dot(this));

        public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(double s, Vec2 v) => new(s * v.X, s * v.Y);
        public static Vec2 operator *(Vec2 v, double s) => new(v.X * s, v.Y * s);
        public static Vec2 operator /(Vec2 v, double s) => new(v.X / s, v.Y / s);
    }

    private readonly struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Length() => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

        public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 v, double s) => new(v.X * s, v.Y * s, v.Z * s);
        public static Vec3 operator *(double s, Vec3 v) => new(v.X * s, v.Y * s, v.Z * s);
        public static Vec3 operator /(Vec3 v, double s) => new(v.X / s, v.Y / s, v.Z / s);

        public static Vec3 Cross(Vec3 a, Vec3 b)
            => new(
                (a.Y * b.Z) - (a.Z * b.Y),
                (a.Z * b.X) - (a.X * b.Z),
                (a.X * b.Y) - (a.Y * b.X));
    }
}
