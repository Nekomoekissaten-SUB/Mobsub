using Microsoft.Extensions.Logging;
using ZLogger;

namespace Mobsub.SubtitleParse.AssTypes;

public partial class AssTextStyle(AssStyle baseStyle)
{
    public AssStyle BaseStyle => baseStyle;

    public void Reset(AssStyle style)
    {
        FontName = style.Fontname;
        FontSize = style.Fontsize;
        Colors = new AssTextColor()
        {
            Primary = style.PrimaryColour,
            Secondary = style.SecondaryColour,
            Outline = style.OutlineColour,
            Back = style.BackColour,
        };
        FontWeight = style.Bold ? 1 : 0;
        FontItalic = style.Italic;
        TextUnderline = style.Underline;
        TextStrikeOut = style.StrikeOut;
        TextScale = new AssTextScale()
        {
            X = style.ScaleX,
            Y = style.ScaleY,
        };
        TextSpacing = style.Spacing;
        // Angle
        // BorderStyle
        Borders = new AssTextBorder()
        {
            X = style.Outline,
            Y = style.Outline,
        };
        Shadows = new AssTextShadow()
        {
            X = style.Shadow,
            Y = style.Shadow,
        };
        // Alignment ??= style.Alignment;
        // MarginL
        // MarginR
        // MarginV
        FontEncoding = style.Encoding;
    }

    public AssTextStyle DeepCopy()
    {
        var textStyle = (AssTextStyle)MemberwiseClone();
        if (Transform is not null)
        {
            textStyle.Transform = new List<AssTagTransform>();
            // not need deep copy?
            textStyle.Transform.AddRange(Transform);
        }

        return textStyle;
    }
    
    public bool TryGetColors(out AssTextColor lastValue)
    {
        var value = GetColors();
        if (value is not null)
        {
            lastValue = (AssTextColor)value;
            return true;
        }
        lastValue = new AssTextColor()
        {
            Primary = baseStyle.PrimaryColour,
            Secondary = baseStyle.SecondaryColour,
            Outline = baseStyle.OutlineColour,
            Back = baseStyle.BackColour,
        };
        return false;
    }

    public bool TryGetFontWeight(out int lastValue)
    {
        var value = GetFontWeight();
        if (value is not null)
        {
            lastValue = (int)value;
            return true;
        }
        lastValue = baseStyle.Bold ? 1 : 0;
        return false;
    }

    public bool TryGetPosition(out AssTextPosition lastValue, (int w, int h)? resolution)
    {
        var value = GetPosition();
        if (value is not null)
        {
            lastValue = (AssTextPosition)value;
            return true;
        }

        if (resolution is null)
        {
            lastValue = new AssTextPosition();
        }
        else
        {
            // resolution mod2?
            TryGetAlignment(out var alignment);
            var marginLeft = baseStyle.MarginL;
            var marginRight = baseStyle.MarginR;
            var marginVertical = baseStyle.MarginV;
            var width = resolution.Value.w;
            var height = resolution.Value.h;

            lastValue = alignment switch
            {
                1 => new AssTextPosition(marginLeft, height - marginVertical),
                2 => new AssTextPosition((int)(width * 0.5) + marginLeft - marginRight, height - marginVertical),
                3 => new AssTextPosition(marginRight, height - marginVertical),
                4 => new AssTextPosition(marginLeft, (int)(height * 0.5)),
                5 => new AssTextPosition((int)(width * 0.5) + marginLeft - marginRight, (int)(height * 0.5)),
                6 => new AssTextPosition(marginRight, (int)(height * 0.5)),
                7 => new AssTextPosition(marginLeft, marginVertical),
                8 => new AssTextPosition((int)(width * 0.5) + marginLeft - marginRight, marginVertical),
                9 => new AssTextPosition(marginRight, marginVertical),
                _ => new AssTextPosition()
            };
        }
        
        return false;
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

public struct AssTextPosition(double x, double y)
{
    public double X = x;
    public double Y = y;
}