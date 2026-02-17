using System.Numerics;
using System.Text;
using Mobsub.AutomationBridge.Core.Ass;

namespace Mobsub.AutomationBridge.Core.Optimizer;

public sealed class AssTagsLinearizer
{
    public sealed record SubtitleFrame(int TimeMs, TagState Tags);

    public sealed record TagState(
        Vector2 Position,
        double? RotationZ = null,
        double? ScaleX = null,
        double? ScaleY = null);

    public sealed record SubtitleLine(int StartTimeMs, int EndTimeMs, string OverrideTags);

    public int PrecisionDecimals { get; init; } = 2;
    public double PositionLinearTolerance { get; init; } = 0.25;

    public SubtitleLine Linearize(IReadOnlyList<SubtitleFrame> frames)
    {
        if (frames is null)
            throw new ArgumentNullException(nameof(frames));
        if (frames.Count < 2)
            throw new ArgumentException("Need at least 2 frames.", nameof(frames));

        var first = frames[0];
        var last = frames[^1];
        int duration = Math.Max(0, last.TimeMs - first.TimeMs);

        var sb = new StringBuilder(128);
        sb.Append('{');

        // Position -> \move if linear.
        if (TryLinearizePositionToMove(frames, first.TimeMs, out var moveTag))
            sb.Append(moveTag);
        else
            sb.Append(FormatPos(first.Tags.Position));

        // Rotation/scale -> \t (simple endpoint transform).
        AppendScalarTransform(sb, "frz", first.Tags.RotationZ, last.Tags.RotationZ, duration);
        AppendScalarTransform(sb, "fscx", first.Tags.ScaleX, last.Tags.ScaleX, duration);
        AppendScalarTransform(sb, "fscy", first.Tags.ScaleY, last.Tags.ScaleY, duration);

        sb.Append('}');

        return new SubtitleLine(first.TimeMs, last.TimeMs, sb.ToString());
    }

    private bool TryLinearizePositionToMove(IReadOnlyList<SubtitleFrame> frames, int startTimeMs, out string moveTag)
    {
        var p0 = frames[0].Tags.Position;
        var p1 = frames[^1].Tags.Position;

        int t0 = frames[0].TimeMs;
        int t1 = frames[^1].TimeMs;
        int dt = t1 - t0;
        if (dt <= 0)
        {
            moveTag = string.Empty;
            return false;
        }

        // Validate linearity.
        double maxErr2 = 0;
        for (int i = 1; i < frames.Count - 1; i++)
        {
            double t = (frames[i].TimeMs - t0) / (double)dt;
            var expected = Vector2.Lerp(p0, p1, (float)t);
            var delta = frames[i].Tags.Position - expected;
            double err2 = delta.LengthSquared();
            if (err2 > maxErr2)
                maxErr2 = err2;
        }

        double tol2 = PositionLinearTolerance * PositionLinearTolerance;
        if (maxErr2 > tol2)
        {
            moveTag = string.Empty;
            return false;
        }

        int relT1 = 0;
        int relT2 = t1 - startTimeMs;
        if (relT2 < 0)
            relT2 = 0;

        // \move(x1,y1,x2,y2,t1,t2)
        var sb = new StringBuilder(64);
        sb.Append("\\move(");
        AssValueWriter.AppendNumber(sb, p0.X, PrecisionDecimals);
        sb.Append(',');
        AssValueWriter.AppendNumber(sb, p0.Y, PrecisionDecimals);
        sb.Append(',');
        AssValueWriter.AppendNumber(sb, p1.X, PrecisionDecimals);
        sb.Append(',');
        AssValueWriter.AppendNumber(sb, p1.Y, PrecisionDecimals);
        sb.Append(',');
        AssValueWriter.AppendInt(sb, relT1);
        sb.Append(',');
        AssValueWriter.AppendInt(sb, relT2);
        sb.Append(')');
        moveTag = sb.ToString();
        return true;
    }

    private string FormatPos(Vector2 p)
    {
        var sb = new StringBuilder(32);
        sb.Append("\\pos(");
        AssValueWriter.AppendNumber(sb, p.X, PrecisionDecimals);
        sb.Append(',');
        AssValueWriter.AppendNumber(sb, p.Y, PrecisionDecimals);
        sb.Append(')');
        return sb.ToString();
    }

    private void AppendScalarTransform(StringBuilder sb, string tagName, double? start, double? end, int durationMs)
    {
        if (start is null || end is null)
            return;

        double s = start.Value;
        double e = end.Value;
        if (s.Equals(e))
        {
            sb.Append('\\');
            sb.Append(tagName);
            AssValueWriter.AppendNumber(sb, s, PrecisionDecimals);
            return;
        }

        // base value at start
        sb.Append('\\');
        sb.Append(tagName);
        AssValueWriter.AppendNumber(sb, s, PrecisionDecimals);

        // transform to end value
        sb.Append("\\t(");
        AssValueWriter.AppendInt(sb, 0);
        sb.Append(',');
        AssValueWriter.AppendInt(sb, durationMs);
        sb.Append(",\\");
        sb.Append(tagName);
        AssValueWriter.AppendNumber(sb, e, PrecisionDecimals);
        sb.Append(')');
    }
}
