using System.Numerics;
using System.Text;

namespace Mobsub.AutomationBridge.Core.Ass;

public static class AssDrawingWriter
{
    public static string WritePolyline(ReadOnlySpan<Vector2> points, bool closed, int precisionDecimals = 0)
    {
        if (points.Length == 0)
            return string.Empty;

        // Rough estimate: each point ~ 16 chars + spaces.
        var sb = new StringBuilder(points.Length * 18);
        AppendPolyline(sb, points, closed, precisionDecimals);
        return sb.ToString();
    }

    public static void AppendPolyline(StringBuilder sb, ReadOnlySpan<Vector2> points, bool closed, int precisionDecimals = 0)
    {
        if (points.Length == 0)
            return;

        sb.Append('m');
        sb.Append(' ');
        AppendPoint(sb, points[0], precisionDecimals);

        if (points.Length > 1)
        {
            sb.Append(' ');
            sb.Append('l');

            for (int i = 1; i < points.Length; i++)
            {
                sb.Append(' ');
                AppendPoint(sb, points[i], precisionDecimals);
            }
        }

        if (closed && points.Length > 1)
        {
            sb.Append(' ');
            AppendPoint(sb, points[0], precisionDecimals);
        }
    }

    private static void AppendPoint(StringBuilder sb, Vector2 p, int precisionDecimals)
    {
        AssValueWriter.AppendNumber(sb, p.X, precisionDecimals);
        sb.Append(' ');
        AssValueWriter.AppendNumber(sb, p.Y, precisionDecimals);
    }
}
