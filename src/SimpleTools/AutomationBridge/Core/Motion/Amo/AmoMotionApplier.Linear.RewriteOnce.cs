using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static partial class AmoMotionApplier
{
    private static byte[] RewriteLinearOnce(
        byte[] textUtf8,
        AmoTsrData data,
        MotionTsrMath.TsrState s,
        MotionTsrMath.TsrState e,
        AmoMainOptions opt,
        bool absPos,
        int begin,
        int end,
        bool needPosRewrite,
        bool needOrgRewrite,
        bool doScale,
        bool doRot)
    {
        if (textUtf8.Length == 0)
            return textUtf8;

        if (!needPosRewrite && !needOrgRewrite && !doScale && !doRot)
            return textUtf8;

        // No override blocks => nothing to rewrite for motion tags.
        if (textUtf8.AsSpan().IndexOf((byte)'{') < 0)
            return textUtf8;

        using var edit = AssEventTextEdit.Parse(textUtf8);
        ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;

        for (int si = 0; si < segments.Length; si++)
        {
            ref readonly var seg = ref segments[si];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];

                int start = t.LineRange.Start.GetOffset(utf8.Length);
                int endExclusive = t.LineRange.End.GetOffset(utf8.Length);
                if (endExclusive <= start)
                    continue;
                ReadOnlySpan<byte> tagBytes = utf8.Slice(start, endExclusive - start);

                if ((t.Tag == AssTag.Movement || t.Tag == AssTag.Position) && (needPosRewrite || needOrgRewrite))
                {
                    if (t.Tag == AssTag.Movement)
                    {
                        if (!tagBytes.StartsWith(RawTagStartMove))
                            continue;
                        if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Move)
                            continue;

                        // Convert \move -> \pos at line start (a-mo does this in linear).
                        // If t1/t2 omitted, ASS uses full line duration; we don't know it here, so treat it as instantaneous at start.
                        (double ox, double oy) = GetMovePosAtTime(func, relativeTimeMs: 0);

                        if (needPosRewrite)
                        {
                            (double xs, double ys) = absPos
                                ? (s.XCur, s.YCur)
                                : MotionTsrMath.PositionMath(ox, oy, data.XStartPos, data.YStartPos, s);
                            (double xe, double ye) = absPos
                                ? (e.XCur, e.YCur)
                                : MotionTsrMath.PositionMath(ox, oy, data.XStartPos, data.YStartPos, e);

                            xs = Round2(xs);
                            ys = Round2(ys);
                            xe = Round2(xe);
                            ye = Round2(ye);

                            if ((Math.Abs(xs - xe) <= Eps && Math.Abs(ys - ye) <= Eps) || begin >= end)
                                edit.Replace(t.LineRange, BuildPosTag(xs, ys));
                            else
                                edit.Replace(t.LineRange, BuildMoveTag(xs, ys, xe, ye, begin, end));
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

                        (double xs, double ys) = absPos
                            ? (s.XCur, s.YCur)
                            : MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, s);
                        (double xe, double ye) = absPos
                            ? (e.XCur, e.YCur)
                            : MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, e);

                        xs = Round2(xs);
                        ys = Round2(ys);
                        xe = Round2(xe);
                        ye = Round2(ye);

                        if ((Math.Abs(xs - xe) <= Eps && Math.Abs(ys - ye) <= Eps) || begin >= end)
                        {
                            if (Math.Abs(xs - func.X1) > Eps || Math.Abs(ys - func.Y1) > Eps)
                                edit.Replace(t.LineRange, BuildPosTag(xs, ys));
                        }
                        else
                        {
                            edit.Replace(t.LineRange, BuildMoveTag(xs, ys, xe, ye, begin, end));
                        }

                        continue;
                    }
                }

                if (t.Tag == AssTag.OriginRotation && needOrgRewrite)
                {
                    if (!tagBytes.StartsWith(RawTagStartOrg))
                        continue;
                    if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Org)
                        continue;

                    var (xs, ys) = MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, s);
                    var (xe, ye) = MotionTsrMath.PositionMath(func.X1, func.Y1, data.XStartPos, data.YStartPos, e);
                    xs = Round2(xs);
                    ys = Round2(ys);
                    xe = Round2(xe);
                    ye = Round2(ye);

                    if ((Math.Abs(xs - xe) <= Eps && Math.Abs(ys - ye) <= Eps) || begin >= end)
                    {
                        if (Math.Abs(xs - func.X1) > Eps || Math.Abs(ys - func.Y1) > Eps)
                            edit.Replace(t.LineRange, BuildOrgTag(xs, ys));
                    }
                    else
                    {
                        byte[] startTag = BuildOrgTag(xs, ys);
                        byte[] finishTag = BuildOrgTag(xe, ye);
                        edit.Replace(t.LineRange, BuildTagWithTransform(startTag, begin, end, finishTag));
                    }

                    continue;
                }

                if (doRot && (t.Tag == AssTag.FontRotationZ || t.Tag == AssTag.FontRotationZSimple) && t.TryGet<double>(out double r))
                {
                    ReadOnlySpan<byte> tagPrefixUtf8 = t.Tag == AssTag.FontRotationZ ? "\\frz"u8 : "\\fr"u8;

                    double sv = Round2(r + s.RotDiffDeg);
                    double ev = Round2(r + e.RotDiffDeg);

                    if (Math.Abs(sv - ev) <= Eps || begin >= end)
                    {
                        if (Math.Abs(sv - r) > Eps)
                            edit.Replace(t.LineRange, BuildValueTag(tagPrefixUtf8, sv));
                    }
                    else
                    {
                        byte[] startTag = BuildValueTag(tagPrefixUtf8, sv);
                        byte[] finishTag = BuildValueTag(tagPrefixUtf8, ev);
                        edit.Replace(t.LineRange, BuildTagWithTransform(startTag, begin, end, finishTag));
                    }

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
                            tagPrefixUtf8 = default;
                            enabled = false;
                            break;
                    }

                    if (enabled && !tagPrefixUtf8.IsEmpty && t.TryGet<double>(out double v))
                    {
                        double sRatio = s.RatioX;
                        double eRatio = e.RatioX;

                        double sv = v;
                        double ev = v;

                        if (t.Tag == AssTag.BlurEdgesGaussian)
                        {
                            double rs = 1 - (1 - sRatio) * opt.BlurScale;
                            double re = 1 - (1 - eRatio) * opt.BlurScale;
                            sv = sv * rs;
                            ev = ev * re;
                        }
                        else
                        {
                            sv = sv * sRatio;
                            ev = ev * eRatio;
                        }

                        sv = Round2(sv);
                        ev = Round2(ev);

                        if (Math.Abs(sv - ev) <= Eps || begin >= end)
                        {
                            if (Math.Abs(sv - v) > Eps)
                                edit.Replace(t.LineRange, BuildValueTag(tagPrefixUtf8, sv));
                        }
                        else
                        {
                            byte[] startTag = BuildValueTag(tagPrefixUtf8, sv);
                            byte[] finishTag = BuildValueTag(tagPrefixUtf8, ev);
                            edit.Replace(t.LineRange, BuildTagWithTransform(startTag, begin, end, finishTag));
                        }

                        continue;
                    }
                }
            }
        }

        return edit.HasEdits ? edit.ApplyToUtf8Bytes() : textUtf8;
    }
}
