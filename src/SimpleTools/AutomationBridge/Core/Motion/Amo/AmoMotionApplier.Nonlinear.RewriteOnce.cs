using System.Buffers.Text;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private const int MaxTransformDepth = 16;

    private static byte[] RewriteNonlinearFrameOnce(
        byte[] textUtf8,
        AmoApplyContext ctx,
        int frame,
        int relativeTimeMs,
        int timeDeltaMs,
        MotionTsrMath.TsrState mainState,
        MotionTsrMath.TsrState rectState,
        MotionTsrMath.TsrState vectState,
        bool needPosRewrite,
        bool needOrgRewrite,
        bool doRectClip,
        bool doVectClip,
        bool doScale,
        bool doRot,
        List<string> logs,
        int depth = 0)
    {
        if (textUtf8.Length == 0)
            return textUtf8;

        // No override blocks => nothing to rewrite for motion tags.
        if (textUtf8.AsSpan().IndexOf((byte)'{') < 0)
            return textUtf8;

        var mainData = ctx.MainData as AmoTsrData;
        var rectData = ctx.RectClipData as AmoTsrData;
        var srs = ctx.VectClipData as AmoSrsData;
        var vectData = ctx.VectClipData as AmoTsrData;

        bool absPos = ctx.Options.Main.AbsPos;
        var opt = ctx.Options.Main;

        // Karaoke shift in centiseconds.
        int shiftCs = (int)Math.Round(timeDeltaMs * 0.1, MidpointRounding.AwayFromZero);
        int shiftRemainingCs = shiftCs;

        using var edit = AssEventTextEdit.Parse(textUtf8);
        ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;

        Span<byte> clipX1Buf = stackalloc byte[64];
        Span<byte> clipY1Buf = stackalloc byte[64];
        Span<byte> clipX2Buf = stackalloc byte[64];
        Span<byte> clipY2Buf = stackalloc byte[64];
        Span<byte> karaokeNumBuf = stackalloc byte[16];

        double xDelta = 0;
        double yDelta = 0;
        if (absPos && needPosRewrite && (doRectClip || doVectClip) && TryFindLastPosOrMoveAtTime(segments, utf8, relativeTimeMs, out double baseX, out double baseY))
        {
            xDelta = mainState.XCur - baseX;
            yDelta = mainState.YCur - baseY;
        }

        byte[]? RewritePayload(ReadOnlySpan<byte> payloadUtf8)
        {
            if (depth >= MaxTransformDepth)
                return null;
            if (payloadUtf8.IsEmpty || payloadUtf8.IndexOf((byte)'\\') < 0)
                return null;

            byte[] wrapped = AssOverrideTagRewriter.WrapSingleOverrideBlock(payloadUtf8);
            byte[] rewrittenWrapped = RewriteNonlinearFrameOnce(
                wrapped,
                ctx,
                frame,
                relativeTimeMs,
                timeDeltaMs,
                mainState,
                rectState,
                vectState,
                needPosRewrite,
                needOrgRewrite,
                doRectClip,
                doVectClip,
                doScale,
                doRot,
                logs,
                depth + 1);

            if (!AssOverrideTagRewriter.TryUnwrapSingleOverrideBlock(rewrittenWrapped, out var inner))
                return null;
            if (inner.SequenceEqual(payloadUtf8))
                return null;
            return inner.ToArray();
        }

        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];

                if (t.Tag == AssTag.Transform)
                {
                    AssOverrideTagRewriter.TryRewriteTransformTagPayload(edit, utf8, t, RewritePayload);
                    continue;
                }

                int start = t.LineRange.Start.GetOffset(utf8.Length);
                int end = t.LineRange.End.GetOffset(utf8.Length);
                if (end <= start)
                    continue;
                ReadOnlySpan<byte> tagBytes = utf8.Slice(start, end - start);

                if (t.Tag == AssTag.Fade && timeDeltaMs != 0)
                {
                    if (!tagBytes.StartsWith(RawTagStartFade))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Fade)
                    {
                        logs.Add("Malformed \\fade(...) encountered; leaving unchanged.");
                        continue;
                    }

                    int t1 = func.T1 - timeDeltaMs;
                    int t2 = func.T2 - timeDeltaMs;
                    int t3 = func.T3 - timeDeltaMs;
                    int t4 = func.T4 - timeDeltaMs;
                    edit.Replace(t.LineRange, BuildFadeTag(func.A1, func.A2, func.A3, t1, t2, t3, t4));
                    continue;
                }

                if ((t.Tag == AssTag.Movement || t.Tag == AssTag.Position) && (needPosRewrite || needOrgRewrite) && mainData is not null)
                {
                    if (t.Tag == AssTag.Movement)
                    {
                        if (!tagBytes.StartsWith(RawTagStartMove))
                            continue;
                        if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Move)
                            continue;

                        (double ox, double oy) = GetMovePosAtTime(func, relativeTimeMs);
                        if (needPosRewrite)
                        {
                            (double nx, double ny) = absPos
                                ? (mainState.XCur, mainState.YCur)
                                : MotionTsrMath.PositionMath(ox, oy, mainData.XStartPos, mainData.YStartPos, mainState);

                            nx = Round2(nx);
                            ny = Round2(ny);
                            edit.Replace(t.LineRange, BuildPosTag(nx, ny));
                        }
                        else
                        {
                            edit.Replace(t.LineRange, BuildPosTag(ox, oy));
                        }

                        continue;
                    }

                    if (t.Tag == AssTag.Position && needPosRewrite)
                    {
                        if (!tagBytes.StartsWith(RawTagStartPos))
                            continue;
                        if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Pos)
                            continue;

                        (double nx, double ny) = absPos
                            ? (mainState.XCur, mainState.YCur)
                            : MotionTsrMath.PositionMath(func.X1, func.Y1, mainData.XStartPos, mainData.YStartPos, mainState);

                        nx = Round2(nx);
                        ny = Round2(ny);
                        if (Math.Abs(nx - Round2(func.X1)) > Eps || Math.Abs(ny - Round2(func.Y1)) > Eps)
                            edit.Replace(t.LineRange, BuildPosTag(nx, ny));

                        continue;
                    }
                }

                if (t.Tag == AssTag.OriginRotation && needOrgRewrite && mainData is not null)
                {
                    if (!tagBytes.StartsWith(RawTagStartOrg))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Org)
                        continue;

                    var (nx, ny) = MotionTsrMath.PositionMath(func.X1, func.Y1, mainData.XStartPos, mainData.YStartPos, mainState);
                    nx = Round2(nx);
                    ny = Round2(ny);
                    if (Math.Abs(nx - Round2(func.X1)) > Eps || Math.Abs(ny - Round2(func.Y1)) > Eps)
                        edit.Replace(t.LineRange, BuildOrgTag(nx, ny));

                    continue;
                }

                if ((t.Tag == AssTag.Clip || t.Tag == AssTag.InverseClip) && (doRectClip || doVectClip) && (rectData is not null || srs is not null || vectData is not null))
                {
                    ReadOnlySpan<byte> tagStartUtf8 = t.Tag == AssTag.Clip ? RawTagStartClip : RawTagStartIclip;
                    if (!tagBytes.StartsWith(tagStartUtf8))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func))
                        continue;

                    if (func.Kind == AssTagFunctionKind.ClipRect)
                    {
                        if (rectData is null)
                            continue;

                        double x1 = func.X1 + xDelta;
                        double y1 = func.Y1 + yDelta;
                        double x2 = func.X2 + xDelta;
                        double y2 = func.Y2 + yDelta;

                        var (nx1, ny1) = MotionTsrMath.PositionMath(x1, y1, rectData.XStartPos, rectData.YStartPos, rectState);
                        var (nx2, ny2) = MotionTsrMath.PositionMath(x2, y2, rectData.XStartPos, rectData.YStartPos, rectState);

                        if (!AssUtf8Number.TryFormatCompact2(nx1, clipX1Buf, out int x1Len) ||
                            !AssUtf8Number.TryFormatCompact2(ny1, clipY1Buf, out int y1Len) ||
                            !AssUtf8Number.TryFormatCompact2(nx2, clipX2Buf, out int x2Len) ||
                            !AssUtf8Number.TryFormatCompact2(ny2, clipY2Buf, out int y2Len))
                        {
                            continue;
                        }

                        byte[] repl = new byte[tagStartUtf8.Length + x1Len + 1 + y1Len + 1 + x2Len + 1 + y2Len + 1];
                        int p = 0;
                        tagStartUtf8.CopyTo(repl.AsSpan(p));
                        p += tagStartUtf8.Length;

                        clipX1Buf[..x1Len].CopyTo(repl.AsSpan(p));
                        p += x1Len;
                        repl[p++] = (byte)',';

                        clipY1Buf[..y1Len].CopyTo(repl.AsSpan(p));
                        p += y1Len;
                        repl[p++] = (byte)',';

                        clipX2Buf[..x2Len].CopyTo(repl.AsSpan(p));
                        p += x2Len;
                        repl[p++] = (byte)',';

                        clipY2Buf[..y2Len].CopyTo(repl.AsSpan(p));
                        p += y2Len;
                        repl[p] = (byte)')';
                        edit.Replace(t.LineRange, repl);
                        continue;
                    }

                    // Only replace vector clips (no commas), matching a-mo.
                    if (func.Kind != AssTagFunctionKind.ClipDrawing || func.Scale > 1)
                        continue;

                    if (srs is not null)
                    {
                        ReadOnlySpan<byte> d = srs.GetSrsDrawingUtf8(frame);

                        byte[] repl = new byte[tagStartUtf8.Length + d.Length + 1];
                        int pos = 0;
                        tagStartUtf8.CopyTo(repl.AsSpan(pos));
                        pos += tagStartUtf8.Length;
                        d.CopyTo(repl.AsSpan(pos));
                        pos += d.Length;
                        repl[pos] = (byte)')';
                        edit.Replace(t.LineRange, repl);
                        continue;
                    }

                    if (vectData is not null)
                    {
                        byte[] drawingUtf8 = TransformDrawingUtf8(func.Drawing.Span, vectData, vectState, xDelta, yDelta);

                        byte[] repl = new byte[tagStartUtf8.Length + drawingUtf8.Length + 1];
                        int p = 0;
                        tagStartUtf8.CopyTo(repl.AsSpan(p));
                        p += tagStartUtf8.Length;
                        drawingUtf8.CopyTo(repl.AsSpan(p));
                        p += drawingUtf8.Length;
                        repl[p] = (byte)')';
                        edit.Replace(t.LineRange, repl);
                        continue;
                    }
                }

                if (doRot && (t.Tag == AssTag.FontRotationZ || t.Tag == AssTag.FontRotationZSimple) && t.TryGet<double>(out double rv))
                {
                    ReadOnlySpan<byte> tagPrefixUtf8 = t.Tag == AssTag.FontRotationZ ? "\\frz"u8 : "\\fr"u8;
                    double nv = Round2(rv + mainState.RotDiffDeg);
                    if (Math.Abs(nv - rv) > Eps)
                        edit.Replace(t.LineRange, BuildValueTag(tagPrefixUtf8, nv));
                    continue;
                }

                if (doScale)
                {
                    ReadOnlySpan<byte> tagPrefixUtf8;
                    bool enabled;
                    switch (t.Tag)
                    {
                        case AssTag.FontScaleX:
                            tagPrefixUtf8 = "\\fscx"u8;
                            enabled = true;
                            break;
                        case AssTag.FontScaleY:
                            tagPrefixUtf8 = "\\fscy"u8;
                            enabled = true;
                            break;
                        case AssTag.BorderX:
                            tagPrefixUtf8 = "\\xbord"u8;
                            enabled = opt.Border;
                            break;
                        case AssTag.BorderY:
                            tagPrefixUtf8 = "\\ybord"u8;
                            enabled = opt.Border;
                            break;
                        case AssTag.Border:
                            tagPrefixUtf8 = "\\bord"u8;
                            enabled = opt.Border;
                            break;
                        case AssTag.ShadowX:
                            tagPrefixUtf8 = "\\xshad"u8;
                            enabled = opt.Shadow;
                            break;
                        case AssTag.ShadowY:
                            tagPrefixUtf8 = "\\yshad"u8;
                            enabled = opt.Shadow;
                            break;
                        case AssTag.Shadow:
                            tagPrefixUtf8 = "\\shad"u8;
                            enabled = opt.Shadow;
                            break;
                        case AssTag.BlurEdgesGaussian:
                            tagPrefixUtf8 = "\\blur"u8;
                            enabled = opt.Blur;
                            break;
                        default:
                            enabled = false;
                            tagPrefixUtf8 = default;
                            break;
                    }

                    if (enabled && !tagPrefixUtf8.IsEmpty && t.TryGet<double>(out double dv))
                    {
                        double ratio = mainState.RatioX;
                        double nv = t.Tag == AssTag.BlurEdgesGaussian
                            ? dv * (1 - (1 - ratio) * opt.BlurScale)
                            : dv * ratio;

                        nv = Round2(nv);
                        if (Math.Abs(nv - dv) > Eps)
                            edit.Replace(t.LineRange, BuildValueTag(tagPrefixUtf8, nv));
                        continue;
                    }
                }

                if (shiftRemainingCs > 0 && IsKaraokeTag(t.Tag) && t.TryGet<int>(out int tCs))
                {
                    int digitIndex = IndexOfFirstAsciiDigit(tagBytes);
                    if (digitIndex < 0)
                        continue;

                    int expectedDigitIndex = (t.Tag == AssTag.KaraokeF || t.Tag == AssTag.KaraokeO) ? 3 : 2;
                    if (digitIndex != expectedDigitIndex)
                        continue;
                    int digitEnd = digitIndex;
                    while ((uint)digitEnd < (uint)tagBytes.Length && (uint)(tagBytes[digitEnd] - (byte)'0') <= 9)
                        digitEnd++;

                    int newT = tCs - shiftRemainingCs;
                    shiftRemainingCs -= tCs;

                    var head = tagBytes[..digitIndex];
                    var tail = tagBytes[digitEnd..];

                    if (newT > 0)
                    {
                        if (!Utf8Formatter.TryFormat(newT, karaokeNumBuf, out int written))
                            continue;

                        byte[] repl = new byte[head.Length + written + tail.Length];
                        head.CopyTo(repl);
                        karaokeNumBuf[..written].CopyTo(repl.AsSpan(head.Length));
                        tail.CopyTo(repl.AsSpan(head.Length + written));
                        edit.Replace(t.LineRange, repl);
                    }
                    else
                    {
                        edit.Replace(t.LineRange, tail.ToArray());
                    }
                }
            }
        }

        if (!edit.HasEdits)
            return textUtf8;

        byte[] rewritten = edit.ApplyToUtf8Bytes();
        return AssOverrideTagRewriter.RemoveEmptyOverrideBlocks(rewritten);
    }

    private static bool TryFindLastPosOrMoveAtTime(
        ReadOnlySpan<AssEventSegment> segments,
        ReadOnlySpan<byte> utf8,
        int relativeTimeMs,
        out double x,
        out double y)
    {
        x = y = 0;
        bool found = false;

        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != AssTag.Position && t.Tag != AssTag.Movement)
                    continue;

                int start = t.LineRange.Start.GetOffset(utf8.Length);
                int end = t.LineRange.End.GetOffset(utf8.Length);
                if (end <= start)
                    continue;
                ReadOnlySpan<byte> tagBytes = utf8.Slice(start, end - start);

                if (t.Tag == AssTag.Position)
                {
                    if (!tagBytes.StartsWith(RawTagStartPos))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Pos)
                        continue;
                    x = func.X1;
                    y = func.Y1;
                    found = true;
                    continue;
                }

                if (t.Tag == AssTag.Movement)
                {
                    if (!tagBytes.StartsWith(RawTagStartMove))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Move)
                        continue;

                    (x, y) = GetMovePosAtTime(func, relativeTimeMs);
                    found = true;
                }
            }
        }

        return found;
    }

    private static (double x, double y) GetMovePosAtTime(AssTagFunctionValue move, int relativeTimeMs)
    {
        // If t1/t2 omitted, ASS uses full line duration; we don't know it here, so treat it as instantaneous at start.
        if (!move.HasTimes || move.T2 <= move.T1)
            return (move.X1, move.Y1);

        double p = (relativeTimeMs - move.T1) / (double)(move.T2 - move.T1);
        if (p < 0) p = 0;
        if (p > 1) p = 1;

        double x = move.X1 + (move.X2 - move.X1) * p;
        double y = move.Y1 + (move.Y2 - move.Y1) * p;
        return (x, y);
    }

    private static bool IsKaraokeTag(AssTag tag)
        => tag is AssTag.Karaoke or AssTag.KaraokeF or AssTag.KaraokeO or AssTag.KaraokeFSimple;

    private static int IndexOfFirstAsciiDigit(ReadOnlySpan<byte> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            byte b = span[i];
            if ((uint)(b - (byte)'0') <= 9)
                return i;
        }
        return -1;
    }
}
