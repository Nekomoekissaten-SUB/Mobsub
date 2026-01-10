using Mobsub.SubtitleParseNT2;

namespace Mobsub.SubtitleParseNT2.AssUtils;

public static class AssFunctionTagParsers
{
    public enum AssClipKind : byte
    {
        Rect = 0,
        Drawing = 1,
    }

    public static bool TryParsePos(ReadOnlySpan<byte> payload, out double x, out double y)
    {
        x = 0;
        y = 0;
        if (!Utils.TryGetParenContent(payload, out var inner))
            return false;
        if (!TryReadDoubleToken(ref inner, "pos", out x))
            return false;
        if (!TryReadDoubleToken(ref inner, "pos", out y))
            return false;

        Utils.SkipSpaces(ref inner);
        return inner.IsEmpty;
    }

    public static bool TryParseFad(ReadOnlySpan<byte> payload, out int t1, out int t2)
    {
        t1 = 0;
        t2 = 0;
        if (!Utils.TryGetParenContent(payload, out var inner))
            return false;
        if (!TryReadDoubleToken(ref inner, "fad", out var dt1))
            return false;
        if (!TryReadDoubleToken(ref inner, "fad", out var dt2))
            return false;

        // Some producers output fractional times; ASS expects ms integers.
        t1 = (int)dt1;
        t2 = (int)dt2;

        Utils.SkipSpaces(ref inner);
        return inner.IsEmpty;
    }

    public static bool TryParseFade(ReadOnlySpan<byte> payload, out int a1, out int a2, out int a3, out int t1, out int t2, out int t3, out int t4)
    {
        a1 = a2 = a3 = t1 = t2 = t3 = t4 = 0;
        if (!Utils.TryGetParenContent(payload, out var inner))
            return false;

        // Accept floats for compatibility, truncate toward zero.
        if (!TryReadDoubleToken(ref inner, "fade", out var da1)) return false;
        if (!TryReadDoubleToken(ref inner, "fade", out var da2)) return false;
        if (!TryReadDoubleToken(ref inner, "fade", out var da3)) return false;
        if (!TryReadDoubleToken(ref inner, "fade", out var dt1)) return false;
        if (!TryReadDoubleToken(ref inner, "fade", out var dt2)) return false;
        if (!TryReadDoubleToken(ref inner, "fade", out var dt3)) return false;
        if (!TryReadDoubleToken(ref inner, "fade", out var dt4)) return false;

        a1 = (int)da1;
        a2 = (int)da2;
        a3 = (int)da3;
        t1 = (int)dt1;
        t2 = (int)dt2;
        t3 = (int)dt3;
        t4 = (int)dt4;

        Utils.SkipSpaces(ref inner);
        return inner.IsEmpty;
    }

    public static bool TryParseOrg(ReadOnlySpan<byte> payload, out double x, out double y)
        => TryParsePos(payload, out x, out y);

    public static bool TryParseMove(ReadOnlySpan<byte> payload,
        out double x1, out double y1, out double x2, out double y2,
        out int t1, out int t2, out bool hasTimes)
    {
        x1 = y1 = x2 = y2 = 0;
        t1 = t2 = 0;
        hasTimes = false;

        if (!Utils.TryGetParenContent(payload, out var inner))
            return false;

        if (!TryReadDoubleToken(ref inner, "move", out x1)) return false;
        if (!TryReadDoubleToken(ref inner, "move", out y1)) return false;
        if (!TryReadDoubleToken(ref inner, "move", out x2)) return false;
        if (!TryReadDoubleToken(ref inner, "move", out y2)) return false;

        Utils.SkipSpaces(ref inner);
        if (inner.IsEmpty)
            return true;

        if (!TryReadDoubleToken(ref inner, "move", out var dt1))
            return false;
        if (!TryReadDoubleToken(ref inner, "move", out var dt2))
            return false;

        // Some producers output fractional times; ASS expects ms integers.
        t1 = (int)dt1;
        t2 = (int)dt2;

        Utils.SkipSpaces(ref inner);
        hasTimes = inner.IsEmpty;
        return hasTimes;
    }

    public static bool TryParseClipRect(ReadOnlySpan<byte> payload, out int x1, out int y1, out int x2, out int y2)
    {
        x1 = y1 = x2 = y2 = 0;
        if (!Utils.TryGetParenContent(payload, out var inner))
            return false;

        // ASS spec says these are integers, but some producers output floats.
        // For compatibility we accept doubles and truncate toward zero (like C atoi-style parsing would effectively do).
        if (!TryReadDoubleToken(ref inner, "clip", out var dx1)) return false;
        if (!TryReadDoubleToken(ref inner, "clip", out var dy1)) return false;
        if (!TryReadDoubleToken(ref inner, "clip", out var dx2)) return false;
        if (!TryReadDoubleToken(ref inner, "clip", out var dy2)) return false;

        x1 = (int)dx1;
        y1 = (int)dy1;
        x2 = (int)dx2;
        y2 = (int)dy2;

        Utils.SkipSpaces(ref inner);
        return inner.IsEmpty;
    }

    /// <summary>
    /// Parses \clip(...) / \iclip(...).
    /// Recognizes two shapes:
    /// - Rect: clip(x1,y1,x2,y2)
    /// - Drawing: clip(m ...)
    /// - Drawing with scale: clip(scale, m ...)
    /// </summary>
    public static bool TryParseClip(ReadOnlySpan<byte> payload,
        out AssClipKind kind,
        out int x1, out int y1, out int x2, out int y2,
        out int scale, out ReadOnlySpan<byte> drawing)
    {
        kind = default;
        x1 = y1 = x2 = y2 = 0;
        scale = 1;
        drawing = default;

        if (TryParseClipRect(payload, out x1, out y1, out x2, out y2))
        {
            kind = AssClipKind.Rect;
            return true;
        }

        if (TryParseClipDrawing(payload, out scale, out drawing))
        {
            kind = AssClipKind.Drawing;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses \clip(...) / \iclip(...) drawing form.
    /// Accepted forms:
    /// - clip(m ...)
    /// - clip(scale, m ...)
    /// Returns the drawing payload span (no validation of drawing commands).
    /// </summary>
    public static bool TryParseClipDrawing(ReadOnlySpan<byte> payload, out int scale, out ReadOnlySpan<byte> drawing)
    {
        scale = 1;
        drawing = default;
        if (!Utils.TryGetParenContent(payload, out var inner))
            return false;

        inner = Utils.TrimSpaces(inner);
        if (inner.IsEmpty)
            return false;

        int commaIndex = inner.IndexOf((byte)',');
        if (commaIndex < 0)
        {
            drawing = inner;
            return !drawing.IsEmpty;
        }

        if (inner[(commaIndex + 1)..].IndexOf((byte)',') >= 0)
            return false;

        var scaleSpan = Utils.TrimSpaces(inner[..commaIndex]);
        if (scaleSpan.IsEmpty)
            return false;

        if (!Utils.TryParseDoubleLoose(scaleSpan, out double sDouble, out var invalidScale))
            return false;
        if (invalidScale)
            LogInvalidNumber("clip", scaleSpan);

        scale = Math.Max(1, (int)sDouble);
        drawing = Utils.TrimSpaces(inner[(commaIndex + 1)..]);
        return !drawing.IsEmpty;
    }

    /// <summary>
    /// Parses \t(...) header numbers and returns the raw tag payload (starting at the first '\\').
    /// Only parses the leading numeric header; the returned tag payload is left for the caller to interpret.
    /// </summary>
    public static bool TryParseTransform(ReadOnlySpan<byte> payload,
        out int t1, out int t2, out bool hasTimes,
        out double accel, out bool hasAccel,
        out ReadOnlySpan<byte> tagPayload)
    {
        t1 = t2 = 0;
        hasTimes = false;
        accel = 0;
        hasAccel = false;
        tagPayload = default;

        if (!Utils.TryGetParenContent(payload, out var inner))
            return false;

        Utils.SkipSpaces(ref inner);
        int slashIndex = inner.IndexOf((byte)'\\');
        if (slashIndex < 0)
            return false;

        var header = Utils.TrimSpaces(inner[..slashIndex]);
        tagPayload = Utils.TrimSpaces(inner[slashIndex..]);
        if (tagPayload.IsEmpty)
            return false;

        if (header.IsEmpty)
            return true;

        // Parse 1-3 comma-separated tokens in header: accel OR (t1,t2[,accel]).
        // Note: because we split at the first '\\', the header often ends with a trailing comma.
        // Accepted forms:
        // - \t(\\tags)
        // - \t(accel,\\tags)
        // - \t(t1,t2,\\tags)
        // - \t(t1,t2,accel,\\tags)
        ReadOnlySpan<byte> h = header;
        if (!TryReadDoubleToken(ref h, "t", out var first))
            return false;

        Utils.SkipSpaces(ref h);
        if (h.IsEmpty)
        {
            // Single token => accel
            accel = first;
            hasAccel = true;
            return true;
        }

        if (!TryReadDoubleToken(ref h, "t", out var second))
            return false;

        Utils.SkipSpaces(ref h);
        if (h.IsEmpty)
        {
            t1 = (int)first;
            t2 = (int)second;
            hasTimes = true;
            return true;
        }

        if (!TryReadDoubleToken(ref h, "t", out accel))
            return false;

        Utils.SkipSpaces(ref h);
        if (!h.IsEmpty)
            return false;

        t1 = (int)first;
        t2 = (int)second;
        hasTimes = true;
        hasAccel = true;
        return true;
    }

    private static bool TryReadToken(ref ReadOnlySpan<byte> span, out ReadOnlySpan<byte> token)
    {
        Utils.SkipSpaces(ref span);
        if (span.IsEmpty)
        {
            token = default;
            return false;
        }

        int commaIndex = span.IndexOf((byte)',');
        if (commaIndex < 0)
        {
            token = span;
            span = ReadOnlySpan<byte>.Empty;
            return true;
        }

        token = span[..commaIndex];
        span = span[(commaIndex + 1)..];
        return true;
    }

    private static bool TryReadDoubleToken(ref ReadOnlySpan<byte> span, string tag, out double value)
    {
        value = 0;
        if (!TryReadToken(ref span, out var token))
            return false;

        token = Utils.TrimSpaces(token);
        if (token.IsEmpty)
            return false;

        if (!Utils.TryParseDoubleLoose(token, out value, out var invalid))
            return false;

        if (invalid)
            LogInvalidNumber(tag, token);

        return true;
    }

    private static void LogInvalidNumber(string tag, ReadOnlySpan<byte> token)
    {
        if (AssEventParser.Logger == null)
            return;

        AssEventParser.LogWarning($"Invalid numeric value in \\{tag}: '{Utils.GetString(token)}', treated as 0.");
    }
}
