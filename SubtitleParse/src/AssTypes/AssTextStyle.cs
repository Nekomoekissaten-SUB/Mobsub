using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public partial class AssTextStyle(AssStyle baseStyle, ILogger? logger = null)
{
    internal readonly AssStyle BaseStyle = baseStyle;

    public void Reset(AssStyle style)
    {
        FontWeight = style.Bold ? 1 : 0;
        FontEncoding = style.Encoding;
        FontName = style.Fontname;
        FontItalic = style.Italic;
        TextUnderline = style.Underline;
        TextStrikeOut = style.StrikeOut;
    }
}

public class AssTagTransform(ILogger? logger)
{
    public int StartTime = 0;
    public int EndTime = 0;
    public double Accel = 1.0;
    public AssTextStyleTransform TransTextStyle;

    internal void ParseTime1(ReadOnlySpan<char> span) => StartTime = ParseInt(span, "t1");
    internal void ParseTime2(ReadOnlySpan<char> span) => EndTime = ParseInt(span, "t2");
    internal void ParseAccel(ReadOnlySpan<char> span)
    {
        if (!double.TryParse(span, out var v))
        {
            logger?.ZLogWarning($"Useless transformation accel: {span.ToString()}");
            Accel = 1.0;
        }
        else
        {
            Accel = v;
        }
    }
    
    private int ParseInt(ReadOnlySpan<char> span, string name)
    {
        if (!int.TryParse(span, out var v))
        {
            logger?.ZLogWarning($"Useless transformation {name}: {span.ToString()}");
        }
        return v;
    }
}

public class AssTextColor
{
    public AssRGB8 Primary;
    public AssRGB8 Secondary;
    public AssRGB8 Outline;
    public AssRGB8 Back;
}

public struct AssTextBorder
{
    public double X;
    public double Y;
}
public struct AssTextShadow
{
    public double X;
    public double Y;
}
public struct AssTextScale
{
    public double X;
    public double Y;
}