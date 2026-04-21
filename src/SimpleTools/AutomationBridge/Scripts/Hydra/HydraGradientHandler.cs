using System.Buffers;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Hydra;

internal static class HydraGradientHandler
{
    private const string ClassDialogue = "dialogue";
    private static readonly AssTextOptions TagOptions = new(Dialect: AssTextDialect.VsFilterMod);

    private const byte Backslash = (byte)'\\';
    private const byte OpenParen = (byte)'(';
    private const byte CloseParen = (byte)')';
    private const byte Comma = (byte)',';

    private enum GradientValueKind : byte
    {
        Number = 0,
        Color = 1,
        Alpha = 2,
    }

    private readonly struct GradientTag
    {
        public readonly AssTag Tag;
        public readonly GradientValueKind Kind;
        public readonly double EndNumber;
        public readonly AssColor32 EndColor;
        public readonly byte EndAlpha;

        public GradientTag(AssTag tag, double endNumber)
        {
            Tag = tag;
            Kind = GradientValueKind.Number;
            EndNumber = endNumber;
            EndColor = default;
            EndAlpha = 0;
        }

        public GradientTag(AssTag tag, AssColor32 endColor)
        {
            Tag = tag;
            Kind = GradientValueKind.Color;
            EndNumber = 0;
            EndColor = endColor;
            EndAlpha = 0;
        }

        public GradientTag(AssTag tag, byte endAlpha)
        {
            Tag = tag;
            Kind = GradientValueKind.Alpha;
            EndNumber = 0;
            EndColor = default;
            EndAlpha = endAlpha;
        }
    }

    public static BridgeHandlerResult Handle(HydraGradientCall call, List<string> logs)
    {
        var lines = call.Lines;
        if (lines is null || lines.Length == 0)
            return BadArgs("lines is required and must be non-empty.", logs);

        ReadOnlyMemory<byte> tagsUtf8 = call.Args.TagsUtf8 ?? ReadOnlyMemory<byte>.Empty;
        ReadOnlySpan<byte> normalizedTags = NormalizeTagsPayload(tagsUtf8.Span);
        if (normalizedTags.IsEmpty || normalizedTags.IndexOf(Backslash) < 0)
            return BadArgs("args.tags is required and must contain at least one \\\\tag.", logs);

        if (!TryParseGradientTags(normalizedTags, out var gradientTags, out string? tagsError))
            return BadArgs(tagsError ?? "args.tags parse failed.", logs);

        using var tagSet = HydraTagSet.FromTagsPayload(normalizedTags);
        if (!tagSet.Any)
            return BadArgs("args.tags did not contain any known ASS tags.", logs);

        HydraGradientKind kind = NormalizeKind(call.Args.Kind);

        double stripe = call.Args.Stripe;
        if (double.IsNaN(stripe) || double.IsInfinity(stripe) || stripe <= 0)
            return BadArgs("args.stripe must be a finite number > 0.", logs);

        double accel = call.Args.Accel;
        if (double.IsNaN(accel) || double.IsInfinity(accel) || accel <= 0)
            return BadArgs("args.accel must be a finite number > 0.", logs);

        bool centered = call.Args.Centered;
        bool useHsl = call.Args.UseHsl;
        bool shortRotation = call.Args.ShortRotation;

        int charGroup = call.Args.CharGroup;
        if (charGroup < 1)
            return BadArgs("args.char_group must be >= 1.", logs);

        bool byLineUseLast = call.Args.ByLineUseLast;

        var ops = new List<IBridgePatchOp>(capacity: Math.Min(256, lines.Length));

        return kind switch
        {
            HydraGradientKind.Vertical => ApplyStripedGradient(lines, gradientTags, tagSet, stripe, accel, centered, useHsl, shortRotation, vertical: true, ops, logs),
            HydraGradientKind.Horizontal => ApplyStripedGradient(lines, gradientTags, tagSet, stripe, accel, centered, useHsl, shortRotation, vertical: false, ops, logs),
            HydraGradientKind.ByChar => ApplyByCharGradient(lines, gradientTags, tagSet, accel, centered, useHsl, shortRotation, charGroup, ops, logs),
            HydraGradientKind.ByLine => ApplyByLineGradient(lines, gradientTags, tagSet, accel, centered, useHsl, shortRotation, byLineUseLast, ops, logs),
            _ => BadArgs("args.kind is invalid.", logs),
        };
    }

    private static BridgeHandlerResult ApplyStripedGradient(
        BridgeLine[] lines,
        GradientTag[] gradientTags,
        HydraTagSet tagSet,
        double stripe,
        double accel,
        bool centered,
        bool useHsl,
        bool shortRotation,
        bool vertical,
        List<IBridgePatchOp> ops,
        List<string> logs)
    {
        var indexed = new List<BridgeLine>(lines.Length);
        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;
            indexed.Add(line);
        }

        if (indexed.Count == 0)
            return OkNoOp(logs);

        indexed.Sort(static (a, b) => b.Index.CompareTo(a.Index));

        Func<AssTag, bool> shouldRemove = tag => tag is AssTag.Clip or AssTag.InverseClip || tagSet.Contains(tag);

        foreach (var line in indexed)
        {
            ReadOnlyMemory<byte> textUtf8 = line.TextUtf8 ?? ReadOnlyMemory<byte>.Empty;
            using var read = AssEventTextRead.Parse(textUtf8, TagOptions);

            if (!read.TryGetFirstOverrideBlock(out _, out var firstTags))
                return BadArgs($"Line #{line.Index}: missing first override block.", logs);

            if (!TryGetRectClip(firstTags, out var clipTag, out var x1, out var y1, out var x2, out var y2))
                return BadArgs($"Line #{line.Index}: missing rectangular \\\\clip(x1,y1,x2,y2) in the first override block.", logs);

            x1 = Math.Floor(x1);
            y1 = Math.Floor(y1);
            x2 = Math.Ceiling(x2);
            y2 = Math.Ceiling(y2);

            double size = vertical ? (y2 - y1) : (x2 - x1);
            int total = (int)Math.Ceiling(size / stripe);
            if (total < 2)
                return BadArgs($"Line #{line.Index}: gradient would create no stripes (stripe={stripe}, clip_size={size}).", logs);

            int half = (total + 1) / 2;

            int startTime = line.StartTime ?? 0;
            int endTime = line.EndTime ?? startTime;

            var inserts = new BridgeLineInsert[total];

            for (int l = 1; l <= total; l++)
            {
                int ln = l;
                int count = total;
                if (centered)
                {
                    count = half;
                    if (ln > half)
                        ln = total - ln + 1;
                }

                double sx1 = x1;
                double sy1 = y1;
                double sx2 = x2;
                double sy2 = y2;
                if (vertical)
                {
                    sy1 = y1 + (l - 1) * stripe;
                    sy2 = sy1 + stripe;
                }
                else
                {
                    sx1 = x1 + (l - 1) * stripe;
                    sx2 = sx1 + stripe;
                }

                byte[] insertTags = BuildStripeInsertTags(
                    clipTag: clipTag,
                    x1: sx1,
                    y1: sy1,
                    x2: sx2,
                    y2: sy2,
                    firstTags,
                    gradientTags,
                    count,
                    ln,
                    accel,
                    useHsl,
                    shortRotation);

                byte[] newTextUtf8 = AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlockUtf8(read, insertTags, shouldRemove);

                inserts[l - 1] = new BridgeLineInsert(
                    TemplateId: 0,
                    StartTime: startTime,
                    EndTime: endTime,
                    TextUtf8: newTextUtf8);
            }

            ops.Add(new BridgeSpliceTemplatePatchOp(
                Index: line.Index,
                DeleteCount: 1,
                Templates: null,
                Inserts: inserts));
        }

        return OkPatch(ops, logs);
    }

    private static BridgeHandlerResult ApplyByLineGradient(
        BridgeLine[] lines,
        GradientTag[] gradientTags,
        HydraTagSet tagSet,
        double accel,
        bool centered,
        bool useHsl,
        bool shortRotation,
        bool byLineUseLast,
        List<IBridgePatchOp> ops,
        List<string> logs)
    {
        var indexed = new List<BridgeLine>(lines.Length);
        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;
            indexed.Add(line);
        }

        indexed.Sort(static (a, b) => a.Index.CompareTo(b.Index));

        if (indexed.Count == 0)
            return OkNoOp(logs);

        int total = indexed.Count;
        if (total < 2)
            return BadArgs("Gradient by line requires at least 2 dialogue lines.", logs);

        int half = (total + 1) / 2;

        var firstLine = indexed[0];
        using var firstRead = AssEventTextRead.Parse(firstLine.TextUtf8 ?? ReadOnlyMemory<byte>.Empty, TagOptions);
        _ = firstRead.TryGetFirstOverrideBlock(out _, out var firstTags);

        ReadOnlySpan<AssTagSpan> lastTags = default;
        AssEventTextRead? lastRead = null;
        if (byLineUseLast)
        {
            var lastLine = indexed[^1];
            lastRead = AssEventTextRead.Parse(lastLine.TextUtf8 ?? ReadOnlyMemory<byte>.Empty, TagOptions);
            _ = lastRead.TryGetFirstOverrideBlock(out _, out lastTags);
        }

        try
        {
            for (int i = 0; i < indexed.Count; i++)
            {
                var line = indexed[i];
                ReadOnlyMemory<byte> textUtf8 = line.TextUtf8 ?? ReadOnlyMemory<byte>.Empty;
                using var read = AssEventTextRead.Parse(textUtf8, TagOptions);

                int ln = i + 1;
                int count = total;
                if (centered)
                {
                    count = half;
                    if (ln > half)
                        ln = total - ln + 1;
                }

                byte[] insertTags = BuildByLineInsertTags(
                    firstTags,
                    lastTags,
                    gradientTags,
                    count,
                    ln,
                    accel,
                    useHsl,
                    shortRotation,
                    byLineUseLast);

                byte[] newTextUtf8 = AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlockUtf8(read, insertTags, tagSet.Contains);

                if (!newTextUtf8.AsSpan().SequenceEqual(textUtf8.Span))
                {
                    ops.Add(new BridgeSetTextPatchOp(
                        Index: line.Index,
                        TextUtf8: newTextUtf8));
                }
            }
        }
        finally
        {
            lastRead?.Dispose();
        }

        return OkPatch(ops, logs);
    }

    private static BridgeHandlerResult ApplyByCharGradient(
        BridgeLine[] lines,
        GradientTag[] gradientTags,
        HydraTagSet tagSet,
        double accel,
        bool centered,
        bool useHsl,
        bool shortRotation,
        int charGroup,
        List<IBridgePatchOp> ops,
        List<string> logs)
    {
        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;

            ReadOnlyMemory<byte> textUtf8 = line.TextUtf8 ?? ReadOnlyMemory<byte>.Empty;
            if (textUtf8.Length == 0)
                continue;

            // Ensure first override block exists and contains explicit start values for all tags.
            using var read0 = AssEventTextRead.Parse(textUtf8, TagOptions);
            _ = read0.TryGetFirstOverrideBlock(out _, out var firstTags0);

            byte[] startTags = BuildStartTags(firstTags0, gradientTags, useHsl, shortRotation);
            byte[] baseTextUtf8 = AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlockUtf8(read0, startTags, tagSet.Contains);

            // Now compute insertion points on the updated text.
            using var read = AssEventTextRead.Parse(baseTextUtf8, TagOptions);
            ReadOnlySpan<byte> utf8 = read.Utf8.Span;

            int totalChars = CountVisibleChars(read.Segments, utf8);
            if (totalChars < 2)
                continue;

            int totalSegments = (totalChars + charGroup - 1) / charGroup;
            if (totalSegments < 2)
                continue;

            int half = (totalSegments + 1) / 2;

            int insertCount = totalSegments - 1;
            int[] insertionOffsets = ArrayPool<int>.Shared.Rent(insertCount);
            try
            {
                int writtenOffsets = CollectInsertionOffsets(read.Segments, utf8, charGroup, insertionOffsets);
                if (writtenOffsets != insertCount)
                    continue;

                // Build all tag blocks into a single buffer (to avoid N tiny allocations).
                var blocks = ArrayPool<(int Start, int Length)>.Shared.Rent(insertCount);
                var blocksWriter = new ArrayBufferWriter<byte>(insertCount * 32);
                try
                {
                    for (int seg = 2; seg <= totalSegments; seg++)
                    {
                        int ln = seg;
                        int count = totalSegments;
                        if (centered)
                        {
                            count = half;
                            if (ln > half)
                                ln = totalSegments - ln + 1;
                        }

                        int start = blocksWriter.WrittenCount;
                        WriteByte(blocksWriter, (byte)'{');
                        WriteByCharSegmentTags(
                            blocksWriter,
                            firstTags0,
                            gradientTags,
                            count,
                            ln,
                            accel,
                            useHsl,
                            shortRotation);
                        WriteByte(blocksWriter, (byte)'}');
                        blocks[seg - 2] = (start, blocksWriter.WrittenCount - start);
                    }

                    var finalWriter = new ArrayBufferWriter<byte>(utf8.Length + blocksWriter.WrittenCount + 16);

                    int pos = 0;
                    ReadOnlySpan<byte> allBlocksBytes = blocksWriter.WrittenSpan;
                    for (int i = 0; i < insertCount; i++)
                    {
                        int off = insertionOffsets[i];
                        if ((uint)off > (uint)utf8.Length || off < pos)
                            continue;

                        if (off > pos)
                            finalWriter.Write(utf8.Slice(pos, off - pos));

                        var (bs, bl) = blocks[i];
                        if (bl > 0)
                            finalWriter.Write(allBlocksBytes.Slice(bs, bl));

                        pos = off;
                    }

                    if (pos < utf8.Length)
                        finalWriter.Write(utf8[pos..]);

                    byte[] newTextUtf8 = finalWriter.WrittenSpan.ToArray();
                    if (!newTextUtf8.AsSpan().SequenceEqual(textUtf8.Span))
                    {
                        ops.Add(new BridgeSetTextPatchOp(
                            Index: line.Index,
                            TextUtf8: newTextUtf8));
                    }
                }
                finally
                {
                    ArrayPool<(int Start, int Length)>.Shared.Return(blocks, clearArray: false);
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(insertionOffsets, clearArray: false);
            }
        }

        return OkPatch(ops, logs);
    }

    private static int CountVisibleChars(ReadOnlySpan<AssEventSegment> segments, ReadOnlySpan<byte> utf8)
    {
        int count = 0;
        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.Text)
                continue;

            var (start, end) = GetRangeOffsets(seg.LineRange, utf8.Length);
            if (end <= start)
                continue;

            ReadOnlySpan<byte> sp = utf8.Slice(start, end - start);
            int i = 0;
            while (i < sp.Length)
            {
                int consumed = ConsumeUtf8Scalar(sp[i..]);
                i += Math.Max(1, consumed);
                count++;
            }
        }

        return count;
    }

    private static int CollectInsertionOffsets(
        ReadOnlySpan<AssEventSegment> segments,
        ReadOnlySpan<byte> utf8,
        int charGroup,
        Span<int> offsets)
    {
        int nextBoundary = charGroup + 1;
        int visibleIndex = 1;
        int w = 0;

        for (int s = 0; s < segments.Length && w < offsets.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.Text)
                continue;

            var (start, end) = GetRangeOffsets(seg.LineRange, utf8.Length);
            if (end <= start)
                continue;

            int abs = start;
            ReadOnlySpan<byte> sp = utf8.Slice(start, end - start);
            int i = 0;
            while (i < sp.Length && w < offsets.Length)
            {
                if (visibleIndex == nextBoundary)
                {
                    offsets[w++] = abs + i;
                    nextBoundary += charGroup;
                }

                int consumed = ConsumeUtf8Scalar(sp[i..]);
                i += Math.Max(1, consumed);
                visibleIndex++;
            }
        }

        return w;
    }

    private static int ConsumeUtf8Scalar(ReadOnlySpan<byte> sp)
    {
        // Minimal UTF-8 scalar length detector: good enough for counting/insertion boundaries.
        // Invalid sequences are treated as 1 byte.
        if (sp.IsEmpty)
            return 0;

        byte b0 = sp[0];
        if (b0 < 0x80)
            return 1;

        if (b0 < 0xC2)
            return 1;

        if (b0 < 0xE0)
            return sp.Length >= 2 ? 2 : 1;

        if (b0 < 0xF0)
            return sp.Length >= 3 ? 3 : 1;

        if (b0 < 0xF5)
            return sp.Length >= 4 ? 4 : 1;

        return 1;
    }

    private static bool TryGetRectClip(
        ReadOnlySpan<AssTagSpan> tags,
        out AssTag clipTag,
        out double x1,
        out double y1,
        out double x2,
        out double y2)
    {
        clipTag = default;
        x1 = y1 = x2 = y2 = 0;

        for (int i = 0; i < tags.Length; i++)
        {
            ref readonly var t = ref tags[i];
            if (t.Tag is not (AssTag.Clip or AssTag.InverseClip))
                continue;

            if (t.TryGet<AssTagFunctionValue>(out var func) && func.Kind == AssTagFunctionKind.ClipRect)
            {
                clipTag = t.Tag;
                x1 = func.X1;
                y1 = func.Y1;
                x2 = func.X2;
                y2 = func.Y2;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetStartNumber(ReadOnlySpan<AssTagSpan> tags, AssTag tag, out double value)
    {
        for (int i = 0; i < tags.Length; i++)
        {
            ref readonly var t = ref tags[i];
            if (!IsSameTagForGradient(tag, t.Tag))
                continue;

            if (t.TryGet<double>(out var dv))
            {
                value = dv;
                return true;
            }

            if (t.TryGet<int>(out var iv))
            {
                value = iv;
                return true;
            }

            if (t.TryGet<byte>(out var by))
            {
                value = by;
                return true;
            }

            if (t.TryGet<bool>(out var bv))
            {
                value = bv ? 1.0 : 0.0;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetStartAlpha(ReadOnlySpan<AssTagSpan> tags, AssTag tag, out byte value)
    {
        for (int i = 0; i < tags.Length; i++)
        {
            ref readonly var t = ref tags[i];
            if (!IsSameTagForGradient(tag, t.Tag))
                continue;

            if (t.TryGet<byte>(out var by))
            {
                value = by;
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetStartColor(ReadOnlySpan<AssTagSpan> tags, AssTag tag, out AssColor32 value)
    {
        for (int i = 0; i < tags.Length; i++)
        {
            ref readonly var t = ref tags[i];
            if (!IsSameTagForGradient(tag, t.Tag))
                continue;

            if (t.TryGet<AssColor32>(out var c))
            {
                value = c;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool IsSameTagForGradient(AssTag a, AssTag b)
    {
        if (a == b)
            return true;

        // Match Hydra behavior: treat \c and \1c as equivalent for gradients.
        if (a is AssTag.ColorPrimaryAbbreviation && b is AssTag.ColorPrimary)
            return true;
        if (a is AssTag.ColorPrimary && b is AssTag.ColorPrimaryAbbreviation)
            return true;

        return false;
    }

    private static double GetProgress(int count, int ln, double accel)
    {
        if (count <= 1)
            return 0;
        if (ln <= 1)
            return 0;
        if (ln >= count)
            return 1;

        double t = (ln - 1) / (double)(count - 1);
        if (accel == 1.0)
            return t;
        return Math.Pow(t, accel);
    }

    private static double LerpNumber(double a, double b, double t)
        => a + (b - a) * t;

    private static byte LerpByte(byte a, byte b, double t)
        => (byte)Math.Clamp((int)Math.Round(a + (b - a) * t, MidpointRounding.AwayFromZero), 0, 255);

    private static AssColor32 LerpColorRgb(AssColor32 a, AssColor32 b, double t)
    {
        byte r = LerpByte(a.R, b.R, t);
        byte g = LerpByte(a.G, b.G, t);
        byte bb = LerpByte(a.B, b.B, t);
        return new AssColor32(r, g, bb, alpha: 0);
    }

    private readonly struct Hsl(double h, double s, double l)
    {
        public double H { get; } = h;
        public double S { get; } = s;
        public double L { get; } = l;
    }

    private static AssColor32 LerpColorHsl(AssColor32 a, AssColor32 b, double t)
    {
        Hsl ha = RgbToHsl(a);
        Hsl hb = RgbToHsl(b);

        double dh = hb.H - ha.H;
        if (dh > 0.5) dh -= 1.0;
        if (dh < -0.5) dh += 1.0;

        double h = ha.H + dh * t;
        h -= Math.Floor(h);

        double s = ha.S + (hb.S - ha.S) * t;
        double l = ha.L + (hb.L - ha.L) * t;

        return HslToRgb(new Hsl(h, s, l));
    }

    private static Hsl RgbToHsl(AssColor32 c)
    {
        double r = c.R / 255.0;
        double g = c.G / 255.0;
        double b = c.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double h = 0;
        double s = 0;
        double l = (max + min) * 0.5;

        if (max != min)
        {
            double d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

            if (max == r)
                h = (g - b) / d + (g < b ? 6.0 : 0.0);
            else if (max == g)
                h = (b - r) / d + 2.0;
            else
                h = (r - g) / d + 4.0;

            h /= 6.0;
        }

        return new Hsl(h, s, l);
    }

    private static AssColor32 HslToRgb(Hsl hsl)
    {
        double h = hsl.H;
        double s = Math.Clamp(hsl.S, 0, 1);
        double l = Math.Clamp(hsl.L, 0, 1);

        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1.0 + s) : (l + s - l * s);
            double p = 2.0 * l - q;
            r = Hue2Rgb(p, q, h + 1.0 / 3.0);
            g = Hue2Rgb(p, q, h);
            b = Hue2Rgb(p, q, h - 1.0 / 3.0);
        }

        return new AssColor32(
            (byte)Math.Clamp((int)Math.Round(r * 255.0, MidpointRounding.AwayFromZero), 0, 255),
            (byte)Math.Clamp((int)Math.Round(g * 255.0, MidpointRounding.AwayFromZero), 0, 255),
            (byte)Math.Clamp((int)Math.Round(b * 255.0, MidpointRounding.AwayFromZero), 0, 255),
            alpha: 0);
    }

    private static double Hue2Rgb(double p, double q, double t)
    {
        t -= Math.Floor(t);
        if (t < 1.0 / 6.0) return p + (q - p) * 6.0 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6.0;
        return p;
    }

    private static bool IsRotationTag(AssTag tag)
        => tag is AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple;

    private static double AdjustShortAngle(double start, double end)
    {
        double diff = end - start;
        while (diff > 180.0) diff -= 360.0;
        while (diff < -180.0) diff += 360.0;
        return start + diff;
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        Span<byte> span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }

    private static void WriteHexByteUpper(byte value, Span<byte> dest2)
    {
        if (dest2.Length != 2)
            return;

        const string digits = "0123456789ABCDEF";
        dest2[0] = (byte)digits[(value >> 4) & 0xF];
        dest2[1] = (byte)digits[value & 0xF];
    }

    private static void WriteRectClipTag(IBufferWriter<byte> writer, AssTag clipTag, double x1, double y1, double x2, double y2)
    {
        WriteByte(writer, Backslash);
        writer.Write(AssTagRegistry.GetNameBytes(clipTag));
        WriteByte(writer, OpenParen);
        AssUtf8Number.WriteCompact3(writer, x1);
        WriteByte(writer, Comma);
        AssUtf8Number.WriteCompact3(writer, y1);
        WriteByte(writer, Comma);
        AssUtf8Number.WriteCompact3(writer, x2);
        WriteByte(writer, Comma);
        AssUtf8Number.WriteCompact3(writer, y2);
        WriteByte(writer, CloseParen);
    }

    private static void WriteNumberTag(IBufferWriter<byte> writer, AssTag tag, double value)
    {
        WriteByte(writer, Backslash);
        writer.Write(AssTagRegistry.GetNameBytes(tag));
        AssUtf8Number.WriteCompact3(writer, value);
    }

    private static void WriteAlphaTag(IBufferWriter<byte> writer, AssTag tag, byte value)
    {
        WriteByte(writer, Backslash);
        writer.Write(AssTagRegistry.GetNameBytes(tag));
        writer.Write("&H"u8);
        Span<byte> hex = stackalloc byte[2];
        WriteHexByteUpper(value, hex);
        writer.Write(hex);
        WriteByte(writer, (byte)'&');
    }

    private static void WriteColorTag(IBufferWriter<byte> writer, AssTag tag, AssColor32 value)
    {
        WriteByte(writer, Backslash);
        writer.Write(AssTagRegistry.GetNameBytes(tag));
        writer.Write("&H"u8);
        Span<byte> hex = stackalloc byte[6];
        WriteHexByteUpper(value.B, hex.Slice(0, 2));
        WriteHexByteUpper(value.G, hex.Slice(2, 2));
        WriteHexByteUpper(value.R, hex.Slice(4, 2));
        writer.Write(hex);
        WriteByte(writer, (byte)'&');
    }

    private static byte[] BuildStartTags(ReadOnlySpan<AssTagSpan> firstTags, GradientTag[] gradientTags, bool useHsl, bool shortRotation)
    {
        _ = useHsl;

        var writer = new ArrayBufferWriter<byte>(gradientTags.Length * 24);
        for (int i = 0; i < gradientTags.Length; i++)
        {
            ref readonly var gt = ref gradientTags[i];
            switch (gt.Kind)
            {
                case GradientValueKind.Color:
                    {
                        AssColor32 start = TryGetStartColor(firstTags, gt.Tag, out var c) ? c : gt.EndColor;
                        WriteColorTag(writer, gt.Tag, start);
                        break;
                    }
                case GradientValueKind.Alpha:
                    {
                        byte start = TryGetStartAlpha(firstTags, gt.Tag, out var a) ? a : gt.EndAlpha;
                        WriteAlphaTag(writer, gt.Tag, start);
                        break;
                    }
                default:
                    {
                        double start = TryGetStartNumber(firstTags, gt.Tag, out var n) ? n : gt.EndNumber;
                        _ = shortRotation; // start value does not need short-path normalization
                        WriteNumberTag(writer, gt.Tag, start);
                        break;
                    }
            }
        }
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] BuildByLineInsertTags(
        ReadOnlySpan<AssTagSpan> firstTags,
        ReadOnlySpan<AssTagSpan> lastTags,
        GradientTag[] gradientTags,
        int count,
        int ln,
        double accel,
        bool useHsl,
        bool shortRotation,
        bool byLineUseLast)
    {
        var writer = new ArrayBufferWriter<byte>(gradientTags.Length * 24);
        double t = GetProgress(count, ln, accel);

        for (int i = 0; i < gradientTags.Length; i++)
        {
            ref readonly var gt = ref gradientTags[i];

            switch (gt.Kind)
            {
                case GradientValueKind.Color:
                    {
                        AssColor32 s = TryGetStartColor(firstTags, gt.Tag, out var c1) ? c1 : gt.EndColor;
                        AssColor32 e = gt.EndColor;
                        if (byLineUseLast && TryGetStartColor(lastTags, gt.Tag, out var c2))
                            e = c2;

                        WriteColorTag(writer, gt.Tag, useHsl ? LerpColorHsl(s, e, t) : LerpColorRgb(s, e, t));
                        break;
                    }
                case GradientValueKind.Alpha:
                    {
                        byte s = TryGetStartAlpha(firstTags, gt.Tag, out var a1) ? a1 : gt.EndAlpha;
                        byte e = gt.EndAlpha;
                        if (byLineUseLast && TryGetStartAlpha(lastTags, gt.Tag, out var a2))
                            e = a2;

                        WriteAlphaTag(writer, gt.Tag, LerpByte(s, e, t));
                        break;
                    }
                default:
                    {
                        double s = TryGetStartNumber(firstTags, gt.Tag, out var n1) ? n1 : gt.EndNumber;
                        double e = gt.EndNumber;
                        if (byLineUseLast && TryGetStartNumber(lastTags, gt.Tag, out var n2))
                            e = n2;

                        if (shortRotation && IsRotationTag(gt.Tag))
                            e = AdjustShortAngle(s, e);

                        WriteNumberTag(writer, gt.Tag, LerpNumber(s, e, t));
                        break;
                    }
            }
        }

        return writer.WrittenSpan.ToArray();
    }

    private static void WriteByCharSegmentTags(
        IBufferWriter<byte> writer,
        ReadOnlySpan<AssTagSpan> firstTags,
        GradientTag[] gradientTags,
        int count,
        int ln,
        double accel,
        bool useHsl,
        bool shortRotation)
    {
        double t = GetProgress(count, ln, accel);

        for (int i = 0; i < gradientTags.Length; i++)
        {
            ref readonly var gt = ref gradientTags[i];
            switch (gt.Kind)
            {
                case GradientValueKind.Color:
                    {
                        AssColor32 s = TryGetStartColor(firstTags, gt.Tag, out var c1) ? c1 : gt.EndColor;
                        AssColor32 e = gt.EndColor;
                        WriteColorTag(writer, gt.Tag, useHsl ? LerpColorHsl(s, e, t) : LerpColorRgb(s, e, t));
                        break;
                    }
                case GradientValueKind.Alpha:
                    {
                        byte s = TryGetStartAlpha(firstTags, gt.Tag, out var a1) ? a1 : gt.EndAlpha;
                        byte e = gt.EndAlpha;
                        WriteAlphaTag(writer, gt.Tag, LerpByte(s, e, t));
                        break;
                    }
                default:
                    {
                        double s = TryGetStartNumber(firstTags, gt.Tag, out var n1) ? n1 : gt.EndNumber;
                        double e = gt.EndNumber;
                        if (shortRotation && IsRotationTag(gt.Tag))
                            e = AdjustShortAngle(s, e);
                        WriteNumberTag(writer, gt.Tag, LerpNumber(s, e, t));
                        break;
                    }
            }
        }
    }

    private static byte[] BuildStripeInsertTags(
        AssTag clipTag,
        double x1,
        double y1,
        double x2,
        double y2,
        ReadOnlySpan<AssTagSpan> firstTags,
        GradientTag[] gradientTags,
        int count,
        int ln,
        double accel,
        bool useHsl,
        bool shortRotation)
    {
        var writer = new ArrayBufferWriter<byte>(gradientTags.Length * 24 + 64);

        WriteRectClipTag(writer, clipTag, x1, y1, x2, y2);

        double t = GetProgress(count, ln, accel);

        for (int i = 0; i < gradientTags.Length; i++)
        {
            ref readonly var gt = ref gradientTags[i];
            switch (gt.Kind)
            {
                case GradientValueKind.Color:
                    {
                        AssColor32 s = TryGetStartColor(firstTags, gt.Tag, out var c1) ? c1 : gt.EndColor;
                        AssColor32 e = gt.EndColor;
                        WriteColorTag(writer, gt.Tag, useHsl ? LerpColorHsl(s, e, t) : LerpColorRgb(s, e, t));
                        break;
                    }
                case GradientValueKind.Alpha:
                    {
                        byte s = TryGetStartAlpha(firstTags, gt.Tag, out var a1) ? a1 : gt.EndAlpha;
                        byte e = gt.EndAlpha;
                        WriteAlphaTag(writer, gt.Tag, LerpByte(s, e, t));
                        break;
                    }
                default:
                    {
                        double s = TryGetStartNumber(firstTags, gt.Tag, out var n1) ? n1 : gt.EndNumber;
                        double e = gt.EndNumber;
                        if (shortRotation && IsRotationTag(gt.Tag))
                            e = AdjustShortAngle(s, e);
                        WriteNumberTag(writer, gt.Tag, LerpNumber(s, e, t));
                        break;
                    }
            }
        }

        return writer.WrittenSpan.ToArray();
    }

    private static HydraGradientKind NormalizeKind(HydraGradientKind kind)
        => kind is HydraGradientKind.Vertical or HydraGradientKind.Horizontal or HydraGradientKind.ByChar or HydraGradientKind.ByLine
            ? kind
            : HydraGradientKind.Vertical;

    private static ReadOnlySpan<byte> NormalizeTagsPayload(ReadOnlySpan<byte> tagsUtf8)
    {
        int start = 0;
        int end = tagsUtf8.Length;

        while (start < end && IsAsciiWhitespace(tagsUtf8[start])) start++;
        while (end > start && IsAsciiWhitespace(tagsUtf8[end - 1])) end--;

        if (end - start >= 2 && tagsUtf8[start] == (byte)'{' && tagsUtf8[end - 1] == (byte)'}')
        {
            start++;
            end--;
            while (start < end && IsAsciiWhitespace(tagsUtf8[start])) start++;
            while (end > start && IsAsciiWhitespace(tagsUtf8[end - 1])) end--;
        }

        return tagsUtf8.Slice(start, end - start);
    }

    private static bool IsAsciiWhitespace(byte b)
        => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));

    private static bool TryParseGradientTags(ReadOnlySpan<byte> tagsPayload, out GradientTag[] tags, out string? error)
    {
        error = null;
        tags = Array.Empty<GradientTag>();

        var tmp = new List<GradientTag>(capacity: 8);

        var scanner = new AssOverrideTagScanner(tagsPayload, payloadAbsoluteStartByte: 0, lineBytes: default, TagOptions);
        while (scanner.MoveNext(out var token))
        {
            if (!token.IsKnown)
                continue;

            var value = AssOverrideTagValueParser.ParseValue(token.Tag, token.Param, token.ParamMemory, TagOptions);
            if (value.Kind == AssTagValueKind.None)
                continue;

            AssTag tag = NormalizeGradientTag(token.Tag);

            GradientTag gt;
            switch (value.Kind)
            {
                case AssTagValueKind.Color:
                    if (!value.TryGet<AssColor32>(out var c))
                        continue;
                    gt = new GradientTag(tag, c);
                    break;

                case AssTagValueKind.Byte:
                    if (IsAlphaTag(tag) && value.TryGet<byte>(out var a))
                    {
                        gt = new GradientTag(tag, a);
                        break;
                    }
                    if (!value.TryGet<byte>(out var by))
                        continue;
                    gt = new GradientTag(tag, (double)by);
                    break;

                case AssTagValueKind.Int:
                    if (!value.TryGet<int>(out var iv))
                        continue;
                    gt = new GradientTag(tag, iv);
                    break;

                case AssTagValueKind.Double:
                    if (!value.TryGet<double>(out var dv))
                        continue;
                    gt = new GradientTag(tag, dv);
                    break;

                case AssTagValueKind.Bool:
                    if (!value.TryGet<bool>(out var bv))
                        continue;
                    gt = new GradientTag(tag, bv ? 1.0 : 0.0);
                    break;

                default:
                    continue;
            }

            bool replaced = false;
            for (int i = 0; i < tmp.Count; i++)
            {
                if (tmp[i].Tag == gt.Tag && tmp[i].Kind == gt.Kind)
                {
                    tmp[i] = gt;
                    replaced = true;
                    break;
                }
            }
            if (!replaced)
                tmp.Add(gt);
        }

        if (tmp.Count == 0)
        {
            error = "args.tags did not contain any supported gradient tags.";
            return false;
        }

        tags = tmp.ToArray();
        return true;
    }

    private static AssTag NormalizeGradientTag(AssTag tag)
        => tag == AssTag.ColorPrimary ? AssTag.ColorPrimaryAbbreviation : tag;

    private static bool IsAlphaTag(AssTag tag)
        => tag is AssTag.Alpha or AssTag.AlphaPrimary or AssTag.AlphaSecondary or AssTag.AlphaBorder or AssTag.AlphaShadow;

    private static BridgeHandlerResult OkNoOp(List<string> logs)
        => new(BridgeErrorCodes.Ok, new BridgeResponse(true, null, logs.ToArray(), Patch: null, Result: null, Methods: null));

    private static BridgeHandlerResult OkPatch(List<IBridgePatchOp> ops, List<string> logs)
    {
        BridgePatch? patch = ops.Count > 0 ? new BridgePatch(ops.ToArray()) : null;
        var resp = new BridgeResponse(true, null, logs.ToArray(), patch, Result: null, Methods: null);
        return new BridgeHandlerResult(BridgeErrorCodes.Ok, resp);
    }

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));
}
