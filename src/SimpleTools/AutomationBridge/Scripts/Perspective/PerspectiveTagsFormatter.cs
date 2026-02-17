using System.Text;
using Mobsub.AutomationBridge.Core.Ass;
using Mobsub.AutomationBridge.Core.Motion;

namespace Mobsub.AutomationBridge.Scripts.Perspective;

internal static class PerspectiveTagsFormatter
{
    public static string FormatPerspectiveTags(in PerspectiveTags tags, int precisionDecimals, bool includeAlign)
    {
        static double D(float v) => v;

        var sb = new StringBuilder(160);

        if (includeAlign)
        {
            sb.Append("\\an");
            AssValueWriter.AppendInt(sb, tags.Align);
        }

        sb.Append("\\org(");
        AssValueWriter.AppendNumber(sb, D(tags.Origin.X), precisionDecimals);
        sb.Append(',');
        AssValueWriter.AppendNumber(sb, D(tags.Origin.Y), precisionDecimals);
        sb.Append(')');

        sb.Append("\\pos(");
        AssValueWriter.AppendNumber(sb, D(tags.Position.X), precisionDecimals);
        sb.Append(',');
        AssValueWriter.AppendNumber(sb, D(tags.Position.Y), precisionDecimals);
        sb.Append(')');

        sb.Append("\\frz");
        AssValueWriter.AppendNumber(sb, tags.AngleZ, precisionDecimals);

        sb.Append("\\frx");
        AssValueWriter.AppendNumber(sb, tags.AngleX, precisionDecimals);

        sb.Append("\\fry");
        AssValueWriter.AppendNumber(sb, tags.AngleY, precisionDecimals);

        sb.Append("\\fscx");
        AssValueWriter.AppendNumber(sb, tags.ScaleX, precisionDecimals);

        sb.Append("\\fscy");
        AssValueWriter.AppendNumber(sb, tags.ScaleY, precisionDecimals);

        sb.Append("\\fax");
        AssValueWriter.AppendNumber(sb, tags.ShearX, precisionDecimals);

        sb.Append("\\fay");
        AssValueWriter.AppendNumber(sb, tags.ShearY, precisionDecimals);

        return sb.ToString();
    }
}
