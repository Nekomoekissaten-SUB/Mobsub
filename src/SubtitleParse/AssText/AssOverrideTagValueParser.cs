using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssOverrideTagValueParser
{
    public static AssTagValue ParseValue(in AssOverrideTagToken token, in AssTextOptions options = default)
    {
        if (!token.IsKnown)
            return AssTagValue.Empty;

        return ParseValue(token.Tag, token.Param, token.ParamMemory, options);
    }

    public static AssTagValue ParseValue(AssTag tag, ReadOnlySpan<byte> param, ReadOnlyMemory<byte> paramMemory, in AssTextOptions options = default)
    {
        Utils.TrimSpaces(param, out int start, out int length);
        if (length == 0) return AssTagValue.Empty;

        var trimmedSpan = param.Slice(start, length);
        var trimmedMemory = paramMemory.IsEmpty ? default : paramMemory.Slice(start, length);

        if (AssTagRegistry.TryGetSpecialRule(tag, out var specialRule))
        {
            // VSFilter/libass: \fsc always resets scale (payload ignored); VSFilterMod enables \fsc<scale> overload.
            if (specialRule == AssTagSpecialRule.FontScaleFsc && !options.ModMode)
                return AssTagValue.Empty;

            if (specialRule == AssTagSpecialRule.HexInt32 && Utils.TryParseHexIntLoose(trimmedSpan, out int hex, out var invalidHex))
            {
                if (invalidHex && AssEventTextParser.Logger != null)
                    AssEventTextParser.LogWarning($"Invalid hex integer value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");

                return AssTagValue.FromInt(hex);
            }
        }

        if (AssTagRegistry.IsAlphaTag(tag) && AssColor32.TryParseAlphaByte(trimmedSpan, out var alpha, out var invalidAlpha))
        {
            if (invalidAlpha && AssEventTextParser.Logger != null)
            {
                AssEventTextParser.LogWarning($"Non-canonical alpha value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', parsed as 0x{alpha:X2}.");
            }
            return AssTagValue.FromByte(alpha);
        }

        if (AssTagRegistry.TryGetFunctionKind(tag, out var functionKind) && TryParseFunctionTag(functionKind, trimmedSpan, trimmedMemory, out var funcValue, options))
            return AssTagValue.FromFunction(funcValue);

        if (!AssTagRegistry.TryGetValueKind(tag, out var valueKind))
            return AssTagValue.Empty;

        switch (valueKind)
        {
            case AssTagValueKind.Int:
                if (Utils.TryParseIntLoose(trimmedSpan, out int iv, out var invalidInt))
                {
                    if (invalidInt && AssEventTextParser.Logger != null)
                    {
                        AssEventTextParser.LogWarning($"Invalid integer value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return AssTagValue.FromInt(iv);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Double:
                if (Utils.TryParseDoubleLoose(trimmedSpan, out double dv, out var invalidDouble))
                {
                    if (invalidDouble && AssEventTextParser.Logger != null)
                    {
                        AssEventTextParser.LogWarning($"Invalid number value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return AssTagValue.FromDouble(dv);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Bool:
                // Semantics: only 0/1 are explicit; any other number (including -1) => reset.
                if (Utils.TryParseIntLoose(trimmedSpan, out int bv, out var invalidBool))
                {
                    if (invalidBool && AssEventTextParser.Logger != null)
                    {
                        AssEventTextParser.LogWarning($"Invalid bool value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return bv switch
                    {
                        0 => AssTagValue.FromBool(false),
                        1 => AssTagValue.FromBool(true),
                        _ => AssTagValue.Empty,
                    };
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Byte:
                if (Utils.TryParseIntLoose(trimmedSpan, out int byv, out var invalidByte))
                {
                    if (byv < 0 || byv > 255)
                    {
                        byv = 0;
                        invalidByte = true;
                    }
                    if (invalidByte && AssEventTextParser.Logger != null)
                    {
                        AssEventTextParser.LogWarning($"Invalid byte value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    return AssTagValue.FromByte((byte)byv);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Color:
                if (AssColor32.TryParseTagColor(trimmedSpan, out var color, out var ignoredHighByte, out var invalidColor))
                {
                    if (invalidColor && AssEventTextParser.Logger != null)
                    {
                        AssEventTextParser.LogWarning($"Invalid color value for \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))}: '{Utils.GetString(trimmedSpan)}', treated as 0.");
                    }
                    if (ignoredHighByte && AssEventTextParser.Logger != null)
                    {
                        AssEventTextParser.LogWarning($"ASS color tag \\{Utils.GetString(AssTagRegistry.GetNameBytes(tag))} has more than 6 hex digits; high byte ignored.");
                    }
                    return AssTagValue.FromColor(color);
                }
                return AssTagValue.Empty;
            case AssTagValueKind.Bytes:
                return AssTagValue.FromBytes(trimmedMemory.IsEmpty ? trimmedSpan.ToArray() : trimmedMemory);
            case AssTagValueKind.Function:
            case AssTagValueKind.None:
            default:
                return AssTagValue.Empty;
        }
    }

    private static bool TryParseFunctionTag(AssTagFunctionKind functionKind, ReadOnlySpan<byte> param, ReadOnlyMemory<byte> paramMemory, out AssTagFunctionValue value, in AssTextOptions options)
    {
        value = default;
        switch (functionKind)
        {
            case AssTagFunctionKind.Pos:
                if (AssFunctionTagParsers.TryParsePos(param, out var x, out var y) ||
                    (options.ModMode && AssFunctionTagParsers.TryParsePos3(param, out x, out y, out _)))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Pos, X1 = x, Y1 = y };
                    return true;
                }
                return false;
            case AssTagFunctionKind.Org:
                if (AssFunctionTagParsers.TryParseOrg(param, out var ox, out var oy))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Org, X1 = ox, Y1 = oy };
                    return true;
                }
                return false;
            case AssTagFunctionKind.Move:
                if (AssFunctionTagParsers.TryParseMove(param, out var x1, out var y1, out var x2, out var y2, out var t1, out var t2, out var hasTimes))
                {
                    value = new AssTagFunctionValue
                    {
                        Kind = AssTagFunctionKind.Move,
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        T1 = t1,
                        T2 = t2,
                        HasTimes = hasTimes
                    };
                    return true;
                }
                return false;
            case AssTagFunctionKind.Fade:
                if (AssFunctionTagParsers.TryParseFade(param, out var a1, out var a2, out var a3, out var ft1, out var ft2, out var ft3, out var ft4))
                {
                    value = new AssTagFunctionValue
                    {
                        Kind = AssTagFunctionKind.Fade,
                        A1 = a1,
                        A2 = a2,
                        A3 = a3,
                        T1 = ft1,
                        T2 = ft2,
                        T3 = ft3,
                        T4 = ft4
                    };
                    return true;
                }
                return false;
            case AssTagFunctionKind.Fad:
                if (AssFunctionTagParsers.TryParseFad(param, out var fadT1, out var fadT2))
                {
                    value = new AssTagFunctionValue { Kind = AssTagFunctionKind.Fad, T1 = fadT1, T2 = fadT2 };
                    return true;
                }
                return false;
            case AssTagFunctionKind.ClipRect:
            case AssTagFunctionKind.ClipDrawing:
                if (AssFunctionTagParsers.TryParseClip(param, out var clipKind, out var cx1, out var cy1, out var cx2, out var cy2, out var scale, out var drawing))
                {
                    if (clipKind == AssFunctionTagParsers.AssClipKind.Rect)
                    {
                        value = new AssTagFunctionValue
                        {
                            Kind = AssTagFunctionKind.ClipRect,
                            X1 = cx1,
                            Y1 = cy1,
                            X2 = cx2,
                            Y2 = cy2
                        };
                    }
                    else
                    {
                        value = new AssTagFunctionValue
                        {
                            Kind = AssTagFunctionKind.ClipDrawing,
                            Scale = scale,
                            Drawing = GetSliceMemory(param, paramMemory, drawing)
                        };
                    }
                    return true;
                }
                return false;
            case AssTagFunctionKind.Transform:
                if (AssFunctionTagParsers.TryParseTransform(param, out var tt1, out var tt2, out var hasTimesT, out var accel, out var hasAccel, out var tagPayload))
                {
                    value = new AssTagFunctionValue
                    {
                        Kind = AssTagFunctionKind.Transform,
                        T1 = tt1,
                        T2 = tt2,
                        HasTimes = hasTimesT,
                        Accel = accel,
                        HasAccel = hasAccel,
                        TagPayload = GetSliceMemory(param, paramMemory, tagPayload)
                    };
                    return true;
                }
                return false;
        }

        return false;
    }

    private static ReadOnlyMemory<byte> GetSliceMemory(ReadOnlySpan<byte> fullSpan, ReadOnlyMemory<byte> fullMemory, ReadOnlySpan<byte> sliceSpan)
    {
        if (sliceSpan.IsEmpty)
            return ReadOnlyMemory<byte>.Empty;

        if (fullMemory.IsEmpty)
            return sliceSpan.ToArray();

        ref byte fullRef = ref MemoryMarshal.GetReference(fullSpan);
        ref byte sliceRef = ref MemoryMarshal.GetReference(sliceSpan);
        int offset = (int)Unsafe.ByteOffset(ref fullRef, ref sliceRef);
        if ((uint)offset > (uint)fullSpan.Length || offset + sliceSpan.Length > fullSpan.Length)
            return sliceSpan.ToArray();

        return fullMemory.Slice(offset, sliceSpan.Length);
    }
}
