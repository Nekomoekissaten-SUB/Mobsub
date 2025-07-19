﻿using System.Numerics;
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
        Colors = new AssTextColor(style);
        FontWeight = style.Bold ? 1 : 0;
        FontItalic = style.Italic;
        TextUnderline = style.Underline;
        TextStrikeOut = style.StrikeOut;
        TextScale = new AssTextScale(style);
        TextSpacing = style.Spacing;
        // Angle
        // BorderStyle
        Borders = new AssTextBorder(style);
        Shadows = new AssTextShadow(style);
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
        lastValue = new AssTextColor(baseStyle);
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

    public bool TryGetPosition(out Vector2 lastValue, (int w, int h)? resolution)
    {
        var value = GetPosition();
        if (value is not null)
        {
            lastValue = (Vector2)value;
            return true;
        }

        if (resolution is null)
        {
            lastValue = Vector2.Zero;
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
                1 => new Vector2(marginLeft, height - marginVertical),
                2 => new Vector2((int)(width * 0.5) + marginLeft - marginRight, height - marginVertical),
                3 => new Vector2(marginRight, height - marginVertical),
                4 => new Vector2(marginLeft, (int)(height * 0.5)),
                5 => new Vector2((int)(width * 0.5) + marginLeft - marginRight, (int)(height * 0.5)),
                6 => new Vector2(marginRight, (int)(height * 0.5)),
                7 => new Vector2(marginLeft, marginVertical),
                8 => new Vector2((int)(width * 0.5) + marginLeft - marginRight, marginVertical),
                9 => new Vector2(marginRight, marginVertical),
                _ => Vector2.Zero
            };
        }
        
        return false;
    }

    public bool TryGetTextWrapStyle(int scriptInfoWrapStyle, out int lastValue)
    {
        var value = GetTextWrapStyle();
        if (value is not null)
        {
            lastValue = (int)value;
            return true;
        }
        lastValue = scriptInfoWrapStyle;
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

public struct AssTextColor(AssStyle style)
{
    public AssRGB8 Primary = style.PrimaryColour;
    public AssRGB8 Secondary = style.SecondaryColour;
    public AssRGB8 Outline = style.OutlineColour;
    public AssRGB8 Back = style.BackColour;
}

public struct AssTextBorder(AssStyle style)
{
    public double X = style.Outline;
    public double Y = style.Outline;
}
public struct AssTextShadow(AssStyle style)
{
    public double X = style.Shadow;
    public double Y = style.Shadow;
}
public struct AssTextScale(AssStyle style)
{
    public double X = style.ScaleX;
    public double Y = style.ScaleY;
}
