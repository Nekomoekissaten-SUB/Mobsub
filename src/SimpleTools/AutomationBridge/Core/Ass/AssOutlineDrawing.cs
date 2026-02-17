using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Mobsub.AutomationBridge.Core.Geometry;

namespace Mobsub.AutomationBridge.Core.Ass;

public enum OutlineCommandType
{
    MoveTo = 0,
    LineTo = 1,
    CubicTo = 2,
    Close = 3
}

public readonly record struct OutlineCommand(
    OutlineCommandType Type,
    Vector2 P1,
    Vector2 P2,
    Vector2 P3);

public static class AssOutlineDrawing
{
    public static string ToAssDrawing(
        ReadOnlySpan<OutlineCommand> commands,
        double curveTolerance,
        double simplifyTolerance = 0,
        int precisionDecimals = 0,
        bool closeContours = false)
    {
        if (commands.IsEmpty)
            return string.Empty;

        var sb = new StringBuilder(256);
        var contour = new List<Vector2>(capacity: 256);
        var tmp = new List<Vector2>(capacity: 64);
        var simplified = new List<Vector2>(capacity: 256);

        Vector2 current = default;
        bool hasCurrent = false;

        void FlushContour()
        {
            if (contour.Count == 0)
                return;

            if (simplifyTolerance > 0 && contour.Count > 2)
            {
                RdpSimplifier.Simplify(CollectionsMarshal.AsSpan(contour), simplifyTolerance, simplified);
                AssDrawingWriter.AppendPolyline(sb, CollectionsMarshal.AsSpan(simplified), closeContours, precisionDecimals);
            }
            else
            {
                AssDrawingWriter.AppendPolyline(sb, CollectionsMarshal.AsSpan(contour), closeContours, precisionDecimals);
            }

            sb.Append(' ');
            contour.Clear();
            simplified.Clear();
        }

        for (int i = 0; i < commands.Length; i++)
        {
            var cmd = commands[i];
            switch (cmd.Type)
            {
                case OutlineCommandType.MoveTo:
                    FlushContour();
                    current = cmd.P1;
                    hasCurrent = true;
                    contour.Add(current);
                    break;

                case OutlineCommandType.LineTo:
                    if (!hasCurrent)
                    {
                        current = cmd.P1;
                        hasCurrent = true;
                        contour.Add(current);
                        break;
                    }
                    current = cmd.P1;
                    contour.Add(current);
                    break;

                case OutlineCommandType.CubicTo:
                    if (!hasCurrent)
                    {
                        current = cmd.P3;
                        hasCurrent = true;
                        contour.Add(current);
                        break;
                    }
                    tmp.Clear();
                    new CubicBezier(current, cmd.P1, cmd.P2, cmd.P3).Flatten(curveTolerance, tmp);
                    // tmp includes start point; skip it to avoid duplication.
                    for (int k = 1; k < tmp.Count; k++)
                        contour.Add(tmp[k]);
                    current = cmd.P3;
                    break;

                case OutlineCommandType.Close:
                    FlushContour();
                    hasCurrent = false;
                    current = default;
                    break;
            }
        }

        FlushContour();

        // Trim trailing space.
        if (sb.Length > 0 && sb[^1] == ' ')
            sb.Length--;
        return sb.ToString();
    }
}
