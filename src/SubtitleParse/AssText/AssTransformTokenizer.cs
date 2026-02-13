using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public sealed class AssTokenizedText
{
    private const byte TokenDelimiterUtf8 = 0x03;

    public byte[] Utf8 { get; }
    public IReadOnlyList<AssTransform> Transforms { get; }
    public int LineDurationMs { get; }

    public AssTokenizedText(byte[] utf8, IReadOnlyList<AssTransform> transforms, int lineDurationMs)
    {
        Utf8 = utf8 ?? Array.Empty<byte>();
        Transforms = transforms;
        LineDurationMs = lineDurationMs;
    }

    public byte[] DontTouchTransforms()
    {
        if (Transforms.Count == 0)
            return Utf8;

        return ReplaceTokens(static tr => tr.RawTagUtf8);
    }

    public byte[] DetokenizeShifted(int shiftMs)
    {
        if (Transforms.Count == 0)
            return Utf8;

        return ReplaceTokens(tr => tr.ToShiftedBytes(shiftMs, LineDurationMs));
    }

    public byte[] InterpolateAt(int shiftMs, int timeMs)
    {
        if (Transforms.Count == 0)
            return Utf8;

        byte[] text = Utf8;
        var writer = new ArrayBufferWriter<byte>(text.Length + Transforms.Count * 8);

        int pos = 0;
        while ((uint)pos < (uint)text.Length)
        {
            int d = text.AsSpan(pos).IndexOf(TokenDelimiterUtf8);
            if (d < 0)
            {
                writer.Write(text.AsSpan(pos));
                break;
            }

            d += pos;
            if (d > pos)
                writer.Write(text.AsSpan(pos, d - pos));

            if (TryParseTokenIndex(text, d, out int tokenIndex, out int next) && (uint)(tokenIndex - 1) < (uint)Transforms.Count)
            {
                var tr = Transforms[tokenIndex - 1];
                ReadOnlySpan<byte> before = writer.WrittenSpan;
                writer.Write(tr.InterpolateToTags(before, shiftMs, LineDurationMs, timeMs));
                pos = next;
                continue;
            }

            WriteByte(writer, TokenDelimiterUtf8);
            pos = d + 1;
        }

        return writer.WrittenSpan.ToArray();
    }

    private byte[] ReplaceTokens(Func<AssTransform, ReadOnlySpan<byte>> replacement)
    {
        byte[] text = Utf8;
        var writer = new ArrayBufferWriter<byte>(text.Length + Transforms.Count * 8);

        int pos = 0;
        while ((uint)pos < (uint)text.Length)
        {
            int d = text.AsSpan(pos).IndexOf(TokenDelimiterUtf8);
            if (d < 0)
            {
                writer.Write(text.AsSpan(pos));
                break;
            }

            d += pos;
            if (d > pos)
                writer.Write(text.AsSpan(pos, d - pos));

            if (TryParseTokenIndex(text, d, out int tokenIndex, out int next) && (uint)(tokenIndex - 1) < (uint)Transforms.Count)
            {
                writer.Write(replacement(Transforms[tokenIndex - 1]));
                pos = next;
                continue;
            }

            WriteByte(writer, TokenDelimiterUtf8);
            pos = d + 1;
        }

        return writer.WrittenSpan.ToArray();
    }

    private static bool TryParseTokenIndex(ReadOnlySpan<byte> text, int start, out int tokenIndex, out int nextIndex)
    {
        tokenIndex = 0;
        nextIndex = start;

        if ((uint)start >= (uint)text.Length || text[start] != TokenDelimiterUtf8)
            return false;

        int i = start + 1;
        if ((uint)i >= (uint)text.Length)
            return false;

        int v = 0;
        int digits = 0;
        while ((uint)i < (uint)text.Length)
        {
            byte c = text[i];
            int digit = c - (byte)'0';
            if ((uint)digit > 9)
                break;

            // Prevent overflow on pathological inputs; treat as non-token.
            if (v > (int.MaxValue - digit) / 10)
                return false;

            v = (v * 10) + digit;
            digits++;
            i++;
        }

        if (digits == 0)
            return false;

        if ((uint)i >= (uint)text.Length || text[i] != TokenDelimiterUtf8)
            return false;

        tokenIndex = v;
        nextIndex = i + 1;
        return true;
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        Span<byte> span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }
}

public sealed class AssTransform
{
    public ReadOnlySpan<byte> RawTagUtf8 => _rawTagUtf8;

    private readonly byte[] _rawTagUtf8;
    private readonly int _startMs;
    private readonly int _endMs;
    private readonly double _accel;
    private readonly byte[] _tagPayloadUtf8;

    private readonly bool _payloadParsedOk;
    private readonly PayloadTag[] _payloadTags;

    public AssTransform(ReadOnlySpan<byte> rawParenUtf8, int startMs, int endMs, double accel, ReadOnlySpan<byte> tagPayloadUtf8)
    {
        _startMs = startMs;
        _endMs = endMs;
        _accel = accel;
        _tagPayloadUtf8 = tagPayloadUtf8.IsEmpty ? Array.Empty<byte>() : tagPayloadUtf8.ToArray();

        _rawTagUtf8 = new byte[2 + rawParenUtf8.Length];
        _rawTagUtf8[0] = (byte)'\\';
        _rawTagUtf8[1] = (byte)'t';
        rawParenUtf8.CopyTo(_rawTagUtf8.AsSpan(2));

        _payloadParsedOk = TryParseTransformPayload(_tagPayloadUtf8, out _payloadTags);
    }

    public byte[] ToShiftedBytes(int shiftMs, int lineDurationMs)
    {
        int start = _startMs - shiftMs;
        int end = _endMs - shiftMs;

        ReadOnlySpan<byte> payload = _tagPayloadUtf8;
        if (payload.IsEmpty)
            return Array.Empty<byte>();

        // Match a-mo: if the transform ends before/at 0, just apply the effect.
        if (end <= 0)
            return payload.ToArray();

        if (end < start)
            return Array.Empty<byte>();

        // a-mo uses the original line duration as the bounds check.
        if (start > lineDurationMs)
            return Array.Empty<byte>();

        var writer = new ArrayBufferWriter<byte>(payload.Length + 32);
        writer.Write("\\t("u8);
        WriteInt(writer, start);
        WriteByte(writer, (byte)',');
        WriteInt(writer, end);
        WriteByte(writer, (byte)',');

        if (Math.Abs(_accel - 1.0) > 1e-9)
        {
            AssUtf8Number.WriteCompact3(writer, _accel);
            WriteByte(writer, (byte)',');
        }

        writer.Write(payload);
        WriteByte(writer, (byte)')');
        return writer.WrittenSpan.ToArray();
    }

    public byte[] InterpolateToTags(ReadOnlySpan<byte> textBeforeThisTransform, int shiftMs, int lineDurationMs, int timeMs)
    {
        int start = _startMs - shiftMs;
        int end = _endMs - shiftMs;

        ReadOnlySpan<byte> payload = _tagPayloadUtf8;
        if (payload.IsEmpty)
            return Array.Empty<byte>();

        // If it already completed, return its payload.
        if (end <= 0)
            return payload.ToArray();

        if (end <= start)
            return payload.ToArray();

        double linearProgress = (timeMs - start) / (double)(end - start);
        double p = Math.Pow(linearProgress, _accel);

        // Supported tags: numeric + alpha.
        // If the payload contains something we can't parse, fall back to a shifted \t.
        if (!_payloadParsedOk)
            return ToShiftedBytes(shiftMs, lineDurationMs);

        var writer = new ArrayBufferWriter<byte>(initialCapacity: 64);
        for (int i = 0; i < _payloadTags.Length; i++)
        {
            var tag = _payloadTags[i];
            if (tag.Kind == PayloadTagKind.Unknown)
                return ToShiftedBytes(shiftMs, lineDurationMs);

            double value;
            if (linearProgress <= 0)
            {
                value = GetPriorValue(textBeforeThisTransform, tag);
            }
            else if (linearProgress >= 1)
            {
                value = tag.EndValue;
            }
            else
            {
                double prior = GetPriorValue(textBeforeThisTransform, tag);
                value = (1.0 - p) * prior + p * tag.EndValue;
            }

            AppendTagValue(writer, tag, value);
        }

        return writer.WrittenSpan.ToArray();
    }

    private static double GetPriorValue(ReadOnlySpan<byte> text, PayloadTag tag)
    {
        if (TryFindLastNumericOrAlphaTag(text, tag.Tag, tag.Kind, out double v))
            return v;

        if (tag.Kind == PayloadTagKind.Alpha && tag.Tag is AssTag.AlphaPrimary or AssTag.AlphaSecondary or AssTag.AlphaBorder or AssTag.AlphaShadow)
        {
            if (TryFindLastNumericOrAlphaTag(text, AssTag.Alpha, PayloadTagKind.Alpha, out var a))
                return a;
        }

        return 0.0;
    }

    private static bool TryFindLastNumericOrAlphaTag(ReadOnlySpan<byte> text, AssTag tag, PayloadTagKind kind, out double value)
    {
        value = 0;

        ReadOnlySpan<byte> name = AssTagRegistry.GetNameBytes(tag);
        if (name.IsEmpty)
            return false;

        Span<byte> key = stackalloc byte[1 + name.Length];
        key[0] = (byte)'\\';
        name.CopyTo(key[1..]);

        int idx = text.LastIndexOf(key);
        if (idx < 0)
            return false;

        var rest = text.Slice(idx + key.Length);

        if (kind == PayloadTagKind.Alpha)
        {
            if (!AssColor32.TryParseAlphaByte(rest, out var a, out _))
                return false;
            value = a;
            return true;
        }

        if (!Utils.TryParseDoubleLoose(rest, out var v, out _))
            return false;

        value = v;
        return true;
    }

    private enum PayloadTagKind : byte
    {
        Unknown = 0,
        Numeric = 1,
        Alpha = 2,
    }

    private readonly record struct PayloadTag(PayloadTagKind Kind, AssTag Tag, double EndValue);

    private static bool TryParseTransformPayload(ReadOnlySpan<byte> payloadUtf8, out PayloadTag[] tags)
    {
        tags = Array.Empty<PayloadTag>();
        if (payloadUtf8.IsEmpty)
            return true;

        var list = new List<PayloadTag>(capacity: 8);
        var scanner = new AssOverrideTagScanner(payloadUtf8, payloadAbsoluteStartByte: 0, lineBytes: default);

        while (scanner.MoveNext(out var token))
        {
            if (!token.IsKnown)
                return false;

            var param = Utils.TrimSpaces(token.Param);
            if (param.IsEmpty)
                return false;

            if (AssTagRegistry.IsAlphaTag(token.Tag))
            {
                if (!AssColor32.TryParseAlphaByte(param, out var alpha, out _))
                    return false;
                list.Add(new PayloadTag(PayloadTagKind.Alpha, token.Tag, alpha));
                continue;
            }

            if (!Utils.TryParseDoubleLoose(param, out var dv, out _))
                return false;

            list.Add(new PayloadTag(PayloadTagKind.Numeric, token.Tag, dv));
        }

        tags = list.ToArray();
        return true;
    }

    private static void AppendTagValue(IBufferWriter<byte> writer, PayloadTag tag, double value)
    {
        ReadOnlySpan<byte> name = AssTagRegistry.GetNameBytes(tag.Tag);
        if (name.IsEmpty)
            return;

        Span<byte> key = stackalloc byte[1 + name.Length];
        key[0] = (byte)'\\';
        name.CopyTo(key[1..]);
        writer.Write(key);

        if (tag.Kind == PayloadTagKind.Alpha)
        {
            int a = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (a < 0) a = 0;
            if (a > 255) a = 255;

            writer.Write("&H"u8);
            Span<byte> hex = stackalloc byte[2];
            WriteHexByteUpper((byte)a, hex);
            writer.Write(hex);
            WriteByte(writer, (byte)'&');
            return;
        }

        AssUtf8Number.WriteCompact3(writer, value);
    }

    private static void WriteHexByteUpper(byte value, Span<byte> dest2)
    {
        if (dest2.Length != 2)
            return;

        const string digits = "0123456789ABCDEF";
        dest2[0] = (byte)digits[(value >> 4) & 0xF];
        dest2[1] = (byte)digits[value & 0xF];
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        Span<byte> span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }

    private static void WriteInt(IBufferWriter<byte> writer, int value)
    {
        Span<byte> tmp = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(value, tmp, out int written))
            return;
        writer.Write(tmp[..written]);
    }

}

public static class AssTransformTokenizer
{
    private const byte TokenDelimiterUtf8 = 0x03;

    public static AssTokenizedText Tokenize(ReadOnlySpan<byte> utf8, int lineDurationMs)
    {
        if (utf8.IsEmpty || utf8.IndexOf("\\t"u8) < 0)
            return new AssTokenizedText(utf8.ToArray(), Array.Empty<AssTransform>(), lineDurationMs);

        using var read = AssEventTextRead.Parse(utf8);
        return Tokenize(read, lineDurationMs);
    }

    public static AssTokenizedText Tokenize(AssEventTextRead read, int lineDurationMs)
    {
        ReadOnlySpan<byte> utf8 = read.Utf8.Span;
        ReadOnlySpan<AssEventSegment> segments = read.Segments;

        var spans = new List<(int Start, int End, AssTagFunctionValue Func)>(capacity: 8);

        for (int s = 0; s < segments.Length; s++)
        {
            ref readonly var seg = ref segments[s];
            if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                continue;

            var tags = seg.Tags.Value.Span;
            for (int i = 0; i < tags.Length; i++)
            {
                ref readonly var t = ref tags[i];
                if (t.Tag != AssTag.Transform)
                    continue;
                if (!t.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Transform)
                    continue;

                int start = t.LineRange.Start.GetOffset(utf8.Length);
                int end = t.LineRange.End.GetOffset(utf8.Length);
                if (end <= start)
                    continue;

                spans.Add((start, end, func));
            }
        }

        if (spans.Count == 0)
            return new AssTokenizedText(utf8.ToArray(), Array.Empty<AssTransform>(), lineDurationMs);

        spans.Sort(static (a, b) => a.Start.CompareTo(b.Start));

        var transforms = new List<AssTransform>(capacity: spans.Count);
        var writer = new ArrayBufferWriter<byte>(utf8.Length + spans.Count * 8);
        Span<byte> numBuf = stackalloc byte[16];

        int pos = 0;
        for (int i = 0; i < spans.Count; i++)
        {
            var (start, end, func) = spans[i];

            if (start < pos)
                continue; // overlaps; ignore

            if (start > pos)
                writer.Write(utf8.Slice(pos, start - pos));

            int tokenIndex = transforms.Count + 1;
            WriteByte(writer, TokenDelimiterUtf8);
            if (Utf8Formatter.TryFormat(tokenIndex, numBuf, out int written))
                writer.Write(numBuf[..written]);
            WriteByte(writer, TokenDelimiterUtf8);

            ReadOnlySpan<byte> tagSpanUtf8 = utf8.Slice(start, end - start);
            int paren = tagSpanUtf8.IndexOf((byte)'(');
            ReadOnlySpan<byte> rawParen = paren >= 0 ? tagSpanUtf8[paren..] : "() "u8[..2];

            int trStart = func.HasTimes ? func.T1 : 0;
            int trEnd = func.HasTimes ? func.T2 : 0;
            if (trEnd == 0)
                trEnd = lineDurationMs;

            double accel = func.HasAccel ? func.Accel : 1.0;
            ReadOnlySpan<byte> payload = func.TagPayload.Span;

            transforms.Add(new AssTransform(rawParen, trStart, trEnd, accel, payload));

            pos = end;
        }

        if (pos < utf8.Length)
            writer.Write(utf8[pos..]);

        byte[] tokenized = writer.WrittenSpan.ToArray();
        return new AssTokenizedText(tokenized, transforms, lineDurationMs);
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        Span<byte> span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }
}
