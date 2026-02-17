using System.Buffers.Text;
using System.Globalization;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private static byte[] BuildValueTag(ReadOnlySpan<byte> tagPrefixUtf8, double value)
    {
        Span<byte> valueBuf = stackalloc byte[64];
        if (!AssUtf8Number.TryFormatCompact2(value, valueBuf, out int valueLen))
        {
            // Fallback: return raw prefix (should never happen for typical ASS ranges).
            return tagPrefixUtf8.ToArray();
        }

        byte[] replacement = new byte[tagPrefixUtf8.Length + valueLen];
        int p = 0;
        tagPrefixUtf8.CopyTo(replacement.AsSpan(p));
        p += tagPrefixUtf8.Length;
        valueBuf[..valueLen].CopyTo(replacement.AsSpan(p));
        return replacement;
    }

    private static byte[] BuildPosTag(double x, double y)
    {
        Span<byte> xBuf = stackalloc byte[64];
        Span<byte> yBuf = stackalloc byte[64];
        if (!AssUtf8Number.TryFormatCompact2(x, xBuf, out int xLen) ||
            !AssUtf8Number.TryFormatCompact2(y, yBuf, out int yLen))
        {
            return Utf8.GetBytes($"\\pos({Format2(x)},{Format2(y)})");
        }

        byte[] replacement = new byte[RawTagStartPos.Length + xLen + 1 + yLen + 1];
        int p = 0;
        RawTagStartPos.CopyTo(replacement);
        p += RawTagStartPos.Length;
        xBuf[..xLen].CopyTo(replacement.AsSpan(p));
        p += xLen;
        replacement[p++] = (byte)',';
        yBuf[..yLen].CopyTo(replacement.AsSpan(p));
        p += yLen;
        replacement[p] = (byte)')';
        return replacement;
    }

    private static byte[] BuildOrgTag(double x, double y)
    {
        Span<byte> xBuf = stackalloc byte[64];
        Span<byte> yBuf = stackalloc byte[64];
        if (!AssUtf8Number.TryFormatCompact2(x, xBuf, out int xLen) ||
            !AssUtf8Number.TryFormatCompact2(y, yBuf, out int yLen))
        {
            return Utf8.GetBytes($"\\org({Format2(x)},{Format2(y)})");
        }

        byte[] replacement = new byte[RawTagStartOrg.Length + xLen + 1 + yLen + 1];
        int p = 0;
        RawTagStartOrg.CopyTo(replacement);
        p += RawTagStartOrg.Length;
        xBuf[..xLen].CopyTo(replacement.AsSpan(p));
        p += xLen;
        replacement[p++] = (byte)',';
        yBuf[..yLen].CopyTo(replacement.AsSpan(p));
        p += yLen;
        replacement[p] = (byte)')';
        return replacement;
    }

    private static byte[] BuildFadeTag(int a1, int a2, int a3, int t1, int t2, int t3, int t4)
    {
        Span<byte> a1Buf = stackalloc byte[16];
        Span<byte> a2Buf = stackalloc byte[16];
        Span<byte> a3Buf = stackalloc byte[16];
        Span<byte> t1Buf = stackalloc byte[16];
        Span<byte> t2Buf = stackalloc byte[16];
        Span<byte> t3Buf = stackalloc byte[16];
        Span<byte> t4Buf = stackalloc byte[16];

        if (!Utf8Formatter.TryFormat(a1, a1Buf, out int a1Len) ||
            !Utf8Formatter.TryFormat(a2, a2Buf, out int a2Len) ||
            !Utf8Formatter.TryFormat(a3, a3Buf, out int a3Len) ||
            !Utf8Formatter.TryFormat(t1, t1Buf, out int t1Len) ||
            !Utf8Formatter.TryFormat(t2, t2Buf, out int t2Len) ||
            !Utf8Formatter.TryFormat(t3, t3Buf, out int t3Len) ||
            !Utf8Formatter.TryFormat(t4, t4Buf, out int t4Len))
        {
            return Utf8.GetBytes($"\\fade({a1},{a2},{a3},{t1},{t2},{t3},{t4})");
        }

        byte[] replacement = new byte[
            RawTagStartFade.Length +
            a1Len + 1 + a2Len + 1 + a3Len + 1 +
            t1Len + 1 + t2Len + 1 + t3Len + 1 + t4Len +
            1];

        int p = 0;
        RawTagStartFade.CopyTo(replacement);
        p += RawTagStartFade.Length;

        a1Buf[..a1Len].CopyTo(replacement.AsSpan(p));
        p += a1Len;
        replacement[p++] = (byte)',';

        a2Buf[..a2Len].CopyTo(replacement.AsSpan(p));
        p += a2Len;
        replacement[p++] = (byte)',';

        a3Buf[..a3Len].CopyTo(replacement.AsSpan(p));
        p += a3Len;
        replacement[p++] = (byte)',';

        t1Buf[..t1Len].CopyTo(replacement.AsSpan(p));
        p += t1Len;
        replacement[p++] = (byte)',';

        t2Buf[..t2Len].CopyTo(replacement.AsSpan(p));
        p += t2Len;
        replacement[p++] = (byte)',';

        t3Buf[..t3Len].CopyTo(replacement.AsSpan(p));
        p += t3Len;
        replacement[p++] = (byte)',';

        t4Buf[..t4Len].CopyTo(replacement.AsSpan(p));
        p += t4Len;

        replacement[p] = (byte)')';
        return replacement;
    }

    private static byte[] BuildMoveTag(double x1, double y1, double x2, double y2, int t1, int t2)
    {
        Span<byte> x1Buf = stackalloc byte[64];
        Span<byte> y1Buf = stackalloc byte[64];
        Span<byte> x2Buf = stackalloc byte[64];
        Span<byte> y2Buf = stackalloc byte[64];
        Span<byte> t1Buf = stackalloc byte[16];
        Span<byte> t2Buf = stackalloc byte[16];

        if (!AssUtf8Number.TryFormatCompact2(x1, x1Buf, out int x1Len) ||
            !AssUtf8Number.TryFormatCompact2(y1, y1Buf, out int y1Len) ||
            !AssUtf8Number.TryFormatCompact2(x2, x2Buf, out int x2Len) ||
            !AssUtf8Number.TryFormatCompact2(y2, y2Buf, out int y2Len) ||
            !Utf8Formatter.TryFormat(t1, t1Buf, out int t1Len) ||
            !Utf8Formatter.TryFormat(t2, t2Buf, out int t2Len))
        {
            return Utf8.GetBytes($"\\move({Format2(x1)},{Format2(y1)},{Format2(x2)},{Format2(y2)},{t1},{t2})");
        }

        byte[] replacement = new byte[
            RawTagStartMove.Length +
            x1Len + 1 + y1Len + 1 + x2Len + 1 + y2Len + 1 +
            t1Len + 1 + t2Len +
            1];

        int p = 0;
        RawTagStartMove.CopyTo(replacement);
        p += RawTagStartMove.Length;

        x1Buf[..x1Len].CopyTo(replacement.AsSpan(p));
        p += x1Len;
        replacement[p++] = (byte)',';

        y1Buf[..y1Len].CopyTo(replacement.AsSpan(p));
        p += y1Len;
        replacement[p++] = (byte)',';

        x2Buf[..x2Len].CopyTo(replacement.AsSpan(p));
        p += x2Len;
        replacement[p++] = (byte)',';

        y2Buf[..y2Len].CopyTo(replacement.AsSpan(p));
        p += y2Len;
        replacement[p++] = (byte)',';

        t1Buf[..t1Len].CopyTo(replacement.AsSpan(p));
        p += t1Len;
        replacement[p++] = (byte)',';

        t2Buf[..t2Len].CopyTo(replacement.AsSpan(p));
        p += t2Len;

        replacement[p] = (byte)')';
        return replacement;
    }

    private static byte[] BuildTagWithTransform(byte[] startTag, int begin, int end, byte[] finishTag)
    {
        Span<byte> beginBuf = stackalloc byte[16];
        Span<byte> endBuf = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(begin, beginBuf, out int bLen))
            return startTag;
        if (!Utf8Formatter.TryFormat(end, endBuf, out int eLen))
            return startTag;

        const int prefixLen = 3; // "\\t("
        byte[] replacement = new byte[startTag.Length + prefixLen + bLen + 1 + eLen + 1 + finishTag.Length + 1];
        int p = 0;
        startTag.CopyTo(replacement);
        p += startTag.Length;
        "\\t("u8.CopyTo(replacement.AsSpan(p));
        p += prefixLen;
        beginBuf[..bLen].CopyTo(replacement.AsSpan(p));
        p += bLen;
        replacement[p++] = (byte)',';
        endBuf[..eLen].CopyTo(replacement.AsSpan(p));
        p += eLen;
        replacement[p++] = (byte)',';
        finishTag.CopyTo(replacement.AsSpan(p));
        p += finishTag.Length;
        replacement[p] = (byte)')';
        return replacement;
    }

    private static string Format2(double v)
    {
        v = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        return v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
