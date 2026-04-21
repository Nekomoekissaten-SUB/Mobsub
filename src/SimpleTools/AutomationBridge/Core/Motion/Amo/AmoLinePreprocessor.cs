using System.Globalization;
using System.Text;
using Mobsub.AutomationBridge.Core.Models;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleParse.AssUtils;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal sealed class AmoPreparedLine
{
    public AutomationLine Source { get; }
    public AssTokenizedText Tokenized { get; }
    public bool HasOrg { get; }
    public bool HasClip { get; }
    public AmoStyleInfo Style { get; }

    public AmoPreparedLine(AutomationLine source, AssTokenizedText tokenized, bool hasOrg, bool hasClip, AmoStyleInfo style)
    {
        Source = source;
        Tokenized = tokenized;
        HasOrg = hasOrg;
        HasClip = hasClip;
        Style = style;
    }
}

internal static class AmoLinePreprocessor
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    public static AmoPreparedLine Prepare(
        AutomationLine line,
        AmoApplyOptions options,
        Dictionary<string, AmoStyleInfo> styles,
        int scriptResX,
        int scriptResY,
        AmoPrepareHints hints,
        out string? error)
    {
        error = null;
        if (line.TextUtf8 is null)
        {
            error = "Line.text_utf8 is missing.";
            return null!;
        }

        ReadOnlyMemory<byte> textUtf8 = line.TextUtf8.Value;

        int durationMs = 0;
        if (line.StartTime is not null && line.EndTime is not null)
            durationMs = Math.Max(0, line.EndTime.Value - line.StartTime.Value);

        AmoStyleInfo style = default;
        if (line.Style is not null && styles.TryGetValue(line.Style, out var st))
            style = st;
        else
            style = new AmoStyleInfo(Align: 7, MarginL: 0, MarginR: 0, MarginT: 0, ScaleX: 100, ScaleY: 100, Outline: 0, Shadow: 0, Angle: 0);

        if (!textUtf8.IsEmpty &&
            (hints.EnsurePos ||
             hints.EnsureMissingScaleTags ||
             hints.EnsureMissingBorderTag ||
             hints.EnsureMissingShadowTag ||
             hints.EnsureMissingRotationTag))
        {
            using var read = AssEventTextRead.Parse(textUtf8);

            string insertPos = string.Empty;
            if (hints.EnsurePos && !AssTagValueParser.TryParsePosOrMoveBase(read, out _, out _))
            {
                int align = AssTagValueParser.TryParseAlign(read) ?? (line.Align ?? style.Align);

                int marginL = line.MarginL is not null && line.MarginL.Value != 0 ? line.MarginL.Value : style.MarginL;
                int marginR = line.MarginR is not null && line.MarginR.Value != 0 ? line.MarginR.Value : style.MarginR;
                int marginT = line.MarginT is not null && line.MarginT.Value != 0 ? line.MarginT.Value : style.MarginT;

                var (x, y) = MotionTsrMath.GetDefaultPosition(scriptResX, scriptResY, align, marginL, marginR, marginT);
                insertPos = $"\\pos({MotionTsrMath.FormatCompact(MotionTsrMath.Round2(x))},{MotionTsrMath.FormatCompact(MotionTsrMath.Round2(y))})";
            }

            string insertMissing = string.Empty;
            var insertOpt = options.Main with
            {
                XScale = options.Main.XScale && hints.EnsureMissingScaleTags,
                Border = options.Main.Border && hints.EnsureMissingBorderTag,
                Shadow = options.Main.Shadow && hints.EnsureMissingShadowTag,
                ZRotation = options.Main.ZRotation && hints.EnsureMissingRotationTag,
            };

            if (insertOpt.XScale || insertOpt.Border || insertOpt.Shadow || insertOpt.ZRotation)
            {
                GetFirstOverrideBlockTagPresence(read, out bool hasFscx, out bool hasFscy, out bool hasBord, out bool hasShad, out bool hasFrzOrFr);
                insertMissing = BuildMissingTags(insertOpt, style, hasFscx, hasFscy, hasBord, hasShad, hasFrzOrFr);
            }

            if (!string.IsNullOrEmpty(insertPos) || !string.IsNullOrEmpty(insertMissing))
            {
                string insert = insertMissing + insertPos;

                // Fast path: keep the legacy behavior for lines without a leading override block.
                if (textUtf8.Span[0] != (byte)'{')
                {
                    byte[] insertUtf8 = Utf8.GetBytes(insert);
                    textUtf8 = PrefixNewOverrideBlock(textUtf8.Span, insertUtf8);
                }
                else
                {
                    byte[] insertUtf8 = Utf8.GetBytes(insert);
                    textUtf8 = AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlockUtf8(
                        read,
                        insertTagsUtf8: insertUtf8,
                        shouldRemove: static _ => false);
                }
            }
        }

        // Tokenize transforms to avoid mangling inside \t.
        var tok1 = AssTransformTokenizer.Tokenize(textUtf8.Span, durationMs);

        // Brutalize \fad -> alpha transforms (only outside tokenized transforms).
        byte[] fadProcessed = BrutalizeFad(tok1.Utf8, durationMs);

        // Restore original transforms, then retokenize so newly created \t are also tokenized.
        byte[] withRawTransforms = new AssTokenizedText(fadProcessed, tok1.Transforms, durationMs).DontTouchTransforms();
        var tok2 = AssTransformTokenizer.Tokenize(withRawTransforms, durationMs);

        // Insert missing tags after \r inside override blocks (only affects tags outside tokenized transforms).
        var insertOpt2 = options.Main with
        {
            XScale = options.Main.XScale && hints.EnsureMissingScaleTags,
            Border = options.Main.Border && hints.EnsureMissingBorderTag,
            Shadow = options.Main.Shadow && hints.EnsureMissingShadowTag,
             ZRotation = options.Main.ZRotation && hints.EnsureMissingRotationTag,
         };
        byte[] rProcessed = InsertMissingTagsAfterResets(tok2.Utf8, insertOpt2, style, styles);

        // Convert/normalize \clip tags if clip processing is enabled.
        bool anyClipOpt = options.Main.RectClip || options.Main.VectClip || options.Clip.RectClip || options.Clip.VectClip || options.Main.RcToVc || options.Clip.RcToVc;
        bool rcToVc = options.Main.RcToVc || options.Clip.RcToVc;
        bool hasClip = false;
        byte[] clipProcessed = anyClipOpt ? ConvertClips(rProcessed, rcToVc, ref hasClip) : rProcessed;

        bool hasOrg = clipProcessed.AsSpan().IndexOf("\\org("u8) >= 0;

        // Keep the same transforms list but update tokenized text.
        var tokenized = new AssTokenizedText(clipProcessed, tok2.Transforms, durationMs);
        return new AmoPreparedLine(line, tokenized, hasOrg, hasClip, style);
    }

    private static byte[] PrefixNewOverrideBlock(ReadOnlySpan<byte> lineUtf8, ReadOnlySpan<byte> insertTagsUtf8)
    {
        byte[] output = new byte[lineUtf8.Length + insertTagsUtf8.Length + 2];
        int pos = 0;
        output[pos++] = (byte)'{';
        insertTagsUtf8.CopyTo(output.AsSpan(pos));
        pos += insertTagsUtf8.Length;
        output[pos++] = (byte)'}';
        lineUtf8.CopyTo(output.AsSpan(pos));
        return output;
    }

    private static void GetFirstOverrideBlockTagPresence(
        AssEventTextRead read,
        out bool hasFscx,
        out bool hasFscy,
        out bool hasBord,
        out bool hasShad,
        out bool hasFrzOrFr)
    {
        hasFscx = false;
        hasFscy = false;
        hasBord = false;
        hasShad = false;
        hasFrzOrFr = false;

        if (!read.TryGetFirstOverrideBlock(out _, out var tags))
            return;

        for (int i = 0; i < tags.Length; i++)
        {
            switch (tags[i].Tag)
            {
                case AssTag.FontScaleX:
                    hasFscx = true;
                    break;
                case AssTag.FontScaleY:
                    hasFscy = true;
                    break;
                case AssTag.Border:
                    hasBord = true;
                    break;
                case AssTag.Shadow:
                    hasShad = true;
                    break;
                case AssTag.FontRotationZ:
                case AssTag.FontRotationZSimple:
                    hasFrzOrFr = true;
                    break;
            }
        }
    }

    private static string BuildMissingTags(
        AmoMainOptions options,
        AmoStyleInfo style,
        bool hasFscx,
        bool hasFscy,
        bool hasBord,
        bool hasShad,
        bool hasFrzOrFr)
    {
        var sb = new StringBuilder(capacity: 64);

        // \fscx/\fscy
        if (options.XScale)
        {
            if (!hasFscx)
            {
                double v = style.ScaleX;
                if (Math.Abs(v) > 1e-9)
                    sb.Append("\\fscx").Append(MotionTsrMath.FormatCompact(v));
            }
            if (!hasFscy)
            {
                double v = style.ScaleY;
                if (Math.Abs(v) > 1e-9)
                    sb.Append("\\fscy").Append(MotionTsrMath.FormatCompact(v));
            }
        }

        if (options.Border)
        {
            if (!hasBord)
            {
                double v = style.Outline;
                if (Math.Abs(v) > 1e-9)
                    sb.Append("\\bord").Append(MotionTsrMath.FormatCompact(v));
            }
        }

        if (options.Shadow)
        {
            if (!hasShad)
            {
                double v = style.Shadow;
                if (Math.Abs(v) > 1e-9)
                    sb.Append("\\shad").Append(MotionTsrMath.FormatCompact(v));
            }
        }

        if (options.ZRotation)
        {
            if (!hasFrzOrFr)
                sb.Append("\\frz").Append(MotionTsrMath.FormatCompact(style.Angle));
        }

        return sb.ToString();
    }

    private static byte[] InsertMissingTagsAfterResets(byte[] textUtf8, AmoMainOptions options, AmoStyleInfo lineStyle, Dictionary<string, AmoStyleInfo> styles)
    {
        if (textUtf8.Length == 0 || (!options.XScale && !options.Border && !options.Shadow && !options.ZRotation))
            return textUtf8;

        if (textUtf8.AsSpan().IndexOf("\\r"u8) < 0)
            return textUtf8;

        using var edit = AssEventTextEdit.Parse(textUtf8);
        ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;

        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;

            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != AssTag.Reset)
                    continue;

                int resetStart = t.LineRange.Start.GetOffset(utf8.Length);
                int resetEnd = t.LineRange.End.GetOffset(utf8.Length);
                if (resetEnd < resetStart)
                    resetEnd = resetStart;

                // Keep legacy style name semantics: everything after "\\r" until the next '\\' (raw, no trimming).
                int nameStart = Math.Min(resetStart + 2, resetEnd);
                ReadOnlySpan<byte> styleNameUtf8 = utf8.Slice(nameStart, resetEnd - nameStart);
                string styleName = styleNameUtf8.IsEmpty ? string.Empty : Utf8.GetString(styleNameUtf8);

                AmoStyleInfo style = lineStyle;
                if (!string.IsNullOrEmpty(styleName) && styles.TryGetValue(styleName, out var st))
                    style = st;

                bool hasFscx = false;
                bool hasFscy = false;
                bool hasBord = false;
                bool hasShad = false;
                bool hasFrzOrFr = false;

                // Only check tags in the remainder after this reset tag.
                for (int j = 0; j < tags.Length; j++)
                {
                    ref readonly var after = ref tags[j];
                    int tagStart = after.LineRange.Start.GetOffset(utf8.Length);
                    if (tagStart < resetEnd)
                        continue;

                    switch (after.Tag)
                    {
                        case AssTag.FontScaleX:
                            hasFscx = true;
                            break;
                        case AssTag.FontScaleY:
                            hasFscy = true;
                            break;
                        case AssTag.Border:
                            hasBord = true;
                            break;
                        case AssTag.Shadow:
                            hasShad = true;
                            break;
                        case AssTag.FontRotationZ:
                        case AssTag.FontRotationZSimple:
                            hasFrzOrFr = true;
                            break;
                    }
                }

                string missing = BuildMissingTags(options, style, hasFscx, hasFscy, hasBord, hasShad, hasFrzOrFr);
                if (!string.IsNullOrEmpty(missing))
                {
                    byte[] missingUtf8 = Utf8.GetBytes(missing);
                    edit.Insert(resetEnd, missingUtf8);
                }

                break; // match legacy behavior: only process the first \r in a block.
            }
        }

        if (!edit.HasEdits)
            return textUtf8;

        return edit.ApplyToUtf8Bytes();
    }

    private static byte[] BrutalizeFad(byte[] tokenizedUtf8, int durationMs)
    {
        if (tokenizedUtf8.Length == 0)
            return tokenizedUtf8;

        // Fast path: no \fad => no work.
        if (tokenizedUtf8.AsSpan().IndexOf("\\fad"u8) < 0)
            return tokenizedUtf8;

        static bool IsAlphaTag(AssTag tag)
            => tag is AssTag.Alpha or AssTag.AlphaPrimary or AssTag.AlphaSecondary or AssTag.AlphaBorder or AssTag.AlphaShadow;

        static string GetAlphaTagName(AssTag tag)
            => tag switch
            {
                AssTag.Alpha => "\\alpha",
                AssTag.AlphaPrimary => "\\1a",
                AssTag.AlphaSecondary => "\\2a",
                AssTag.AlphaBorder => "\\3a",
                AssTag.AlphaShadow => "\\4a",
                _ => string.Empty
            };

        string FadToTransform(int tIn, int tOut, string alphaTag, string value)
        {
            var sb = new StringBuilder(64);
            if (tIn > 0)
            {
                sb.Append(alphaTag).Append("&HFF&")
                  .Append("\\t(0,").Append(tIn).Append(',').Append(alphaTag).Append(value).Append(')');
            }
            else
            {
                sb.Append(alphaTag).Append(value);
            }

            if (tOut > 0)
            {
                int t1 = durationMs - tOut;
                if (t1 < 0) t1 = 0;
                sb.Append("\\t(").Append(t1).Append(',').Append(durationMs).Append(',').Append(alphaTag).Append("&HFF&)");
            }

            return sb.ToString();
        }

        using var edit = AssEventTextEdit.Parse(tokenizedUtf8);
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;
        ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;

        int fadIn = 0;
        int fadOut = 0;
        bool found = false;

        // Remove all \fad tags, remember the first successfully parsed one.
        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != AssTag.Fad)
                    continue;

                if (!found && t.TryGet<AssTagFunctionValue>(out var func) && func.Kind == AssTagFunctionKind.Fad)
                {
                    found = true;
                    fadIn = func.T1;
                    fadOut = func.T2;
                }

                edit.Delete(t.LineRange);
            }
        }

        // If we couldn't parse the fad params, keep the legacy behavior: just remove \fad tags.
        if (!found)
            return edit.HasEdits ? edit.ApplyToUtf8Bytes() : tokenizedUtf8;

        // Replace alpha tags in all override blocks.
        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (!IsAlphaTag(t.Tag))
                    continue;
                if (!t.TryGet<byte>(out var alpha))
                    continue;

                string alphaTag = GetAlphaTagName(t.Tag);
                if (string.IsNullOrEmpty(alphaTag))
                    continue;

                string value = "&H" + alpha.ToString("X2", CultureInfo.InvariantCulture) + "&";
                string repl = FadToTransform(fadIn, fadOut, alphaTag, value);
                edit.Replace(t.LineRange, Utf8.GetBytes(repl));
            }
        }

        // Ensure first override block has a base \alpha.
        if (segments.Length > 0 && segments[0].SegmentKind == AssEventSegmentKind.TagBlock)
        {
            int segStart = segments[0].LineRange.Start.GetOffset(utf8.Length);
            if (segStart == 0 && utf8.Length > 0 && utf8[0] == (byte)'{')
            {
                string inject = FadToTransform(fadIn, fadOut, "\\alpha", "&H00&");
                edit.Insert(1, Utf8.GetBytes(inject));
            }
        }

        return edit.ApplyToUtf8Bytes();
    }

    private static byte[] ConvertClips(byte[] tokenizedUtf8, bool rcToVc, ref bool hasClip)
    {
        if (tokenizedUtf8.Length == 0)
            return tokenizedUtf8;

        // Fast path: avoid parsing if there is no potential clip tag.
        if (tokenizedUtf8.AsSpan().IndexOf("\\clip"u8) < 0 &&
            tokenizedUtf8.AsSpan().IndexOf("\\iclip"u8) < 0)
        {
            return tokenizedUtf8;
        }

        using var edit = AssEventTextEdit.Parse(tokenizedUtf8);
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;

        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != AssTag.Clip && t.Tag != AssTag.InverseClip)
                    continue;

                hasClip = true;

                string? newInner = null;
                if (t.TryGet<AssTagFunctionValue>(out var func))
                {
                    if (func.Kind == AssTagFunctionKind.ClipRect)
                    {
                        if (rcToVc)
                        {
                            newInner = RectToVectDrawing(func.X1, func.Y1, func.X2, func.Y2);
                        }
                        else
                        {
                            // Keep rect, but normalize formatting (floats rounded like a-mo convertClipToFP does for vector only).
                            newInner = $"{MotionTsrMath.FormatCompact(MotionTsrMath.Round2(func.X1))},{MotionTsrMath.FormatCompact(MotionTsrMath.Round2(func.Y1))},{MotionTsrMath.FormatCompact(MotionTsrMath.Round2(func.X2))},{MotionTsrMath.FormatCompact(MotionTsrMath.Round2(func.Y2))}";
                        }
                    }
                    else if (func.Kind == AssTagFunctionKind.ClipDrawing)
                    {
                        int scale = Math.Max(1, func.Scale);
                        string drawing = func.Drawing.IsEmpty ? string.Empty : Utf8.GetString(func.Drawing.Span);
                        if (scale > 1)
                        {
                            double div = Math.Pow(2.0, scale - 1);
                            drawing = ConvertDrawingToFP(drawing, div);
                        }
                        newInner = drawing;
                    }
                }

                if (string.IsNullOrEmpty(newInner))
                    continue;

                string replacement = (t.Tag == AssTag.InverseClip ? "\\iclip(" : "\\clip(") + newInner + ")";
                edit.Replace(t.LineRange, Utf8.GetBytes(replacement));
            }
        }

        return edit.HasEdits ? edit.ApplyToUtf8Bytes() : tokenizedUtf8;
    }

    private static string RectToVectDrawing(double l, double t, double r, double b)
    {
        // Match a-mo: m l l l (no closing command).
        return string.Format(CultureInfo.InvariantCulture,
            "m {0} {1} l {2} {1} {2} {3} {0} {3}",
            MotionTsrMath.FormatCompact(MotionTsrMath.Round2(l)),
            MotionTsrMath.FormatCompact(MotionTsrMath.Round2(t)),
            MotionTsrMath.FormatCompact(MotionTsrMath.Round2(r)),
            MotionTsrMath.FormatCompact(MotionTsrMath.Round2(b)));
    }

    private static string ConvertDrawingToFP(string drawing, double div)
    {
        // Scale all coordinate pairs by 1/div and round to 2 decimals.
        var sb = new StringBuilder(drawing.Length + 16);
        int i = 0;
        while (i < drawing.Length)
        {
            // Copy non-number
            if (!(drawing[i] == '-' || drawing[i] == '.' || (drawing[i] >= '0' && drawing[i] <= '9')))
            {
                sb.Append(drawing[i]);
                i++;
                continue;
            }

            int start = i;
            while (i < drawing.Length && (drawing[i] == '-' || drawing[i] == '+' || drawing[i] == '.' || drawing[i] == 'e' || drawing[i] == 'E' || (drawing[i] >= '0' && drawing[i] <= '9')))
                i++;

            if (double.TryParse(drawing.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                double v = MotionTsrMath.Round2(d / div);
                sb.Append(MotionTsrMath.FormatCompact(v));
            }
            else
            {
                sb.Append(drawing.AsSpan(start, i - start));
            }
        }

        return sb.ToString();
    }
}
