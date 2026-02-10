using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public sealed class AssTokenizedText
{
    private const char TokenDelimiter = '\u0003';

    public string Text { get; }
    public IReadOnlyList<AssTransform> Transforms { get; }
    public int LineDurationMs { get; }

    public AssTokenizedText(string text, IReadOnlyList<AssTransform> transforms, int lineDurationMs)
    {
        Text = text;
        Transforms = transforms;
        LineDurationMs = lineDurationMs;
    }

    public string DontTouchTransforms()
    {
        if (Transforms.Count == 0)
            return Text;

        return ReplaceTokens(static tr => "\\t" + tr.RawParenString);
    }

    public string DetokenizeShifted(int shiftMs)
    {
        if (Transforms.Count == 0)
            return Text;

        return ReplaceTokens(tr => tr.ToShiftedString(shiftMs, LineDurationMs));
    }

    public string InterpolateAt(int shiftMs, int timeMs)
    {
        if (Transforms.Count == 0)
            return Text;

        string text = Text;
        var sb = new StringBuilder(text.Length + Transforms.Count * 8);

        int pos = 0;
        while (pos < text.Length)
        {
            int d = text.IndexOf(TokenDelimiter, pos);
            if (d < 0)
            {
                sb.Append(text.AsSpan(pos));
                break;
            }

            if (d > pos)
                sb.Append(text.AsSpan(pos, d - pos));

            if (TryParseTokenIndex(text, d, out int tokenIndex, out int next) && (uint)(tokenIndex - 1) < (uint)Transforms.Count)
            {
                var tr = Transforms[tokenIndex - 1];
                string before = sb.Length == 0 ? string.Empty : sb.ToString();
                string rep = tr.InterpolateToTags(before, shiftMs, LineDurationMs, timeMs);
                sb.Append(rep);
                pos = next;
                continue;
            }

            sb.Append(TokenDelimiter);
            pos = d + 1;
        }

        return sb.ToString();
    }

    private string ReplaceTokens(Func<AssTransform, string> replacement)
    {
        string text = Text;
        var sb = new StringBuilder(text.Length + Transforms.Count * 8);

        int pos = 0;
        while (pos < text.Length)
        {
            int d = text.IndexOf(TokenDelimiter, pos);
            if (d < 0)
            {
                sb.Append(text.AsSpan(pos));
                break;
            }

            if (d > pos)
                sb.Append(text.AsSpan(pos, d - pos));

            if (TryParseTokenIndex(text, d, out int tokenIndex, out int next) && (uint)(tokenIndex - 1) < (uint)Transforms.Count)
            {
                sb.Append(replacement(Transforms[tokenIndex - 1]));
                pos = next;
                continue;
            }

            sb.Append(TokenDelimiter);
            pos = d + 1;
        }

        return sb.ToString();
    }

    private static bool TryParseTokenIndex(string text, int start, out int tokenIndex, out int nextIndex)
    {
        tokenIndex = 0;
        nextIndex = start;

        if ((uint)start >= (uint)text.Length || text[start] != TokenDelimiter)
            return false;

        int i = start + 1;
        if ((uint)i >= (uint)text.Length)
            return false;

        int v = 0;
        int digits = 0;
        while ((uint)i < (uint)text.Length)
        {
            char c = text[i];
            int digit = c - '0';
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

        if ((uint)i >= (uint)text.Length || text[i] != TokenDelimiter)
            return false;

        tokenIndex = v;
        nextIndex = i + 1;
        return true;
    }
}

public sealed class AssTransform
{
    public string Token { get; }
    public string RawParenString { get; }

    private readonly int _startMs;
    private readonly int _endMs;
    private readonly double _accel;
    private readonly string _tagPayload;

    private readonly bool _payloadParsedOk;
    private readonly PayloadTag[] _payloadTags;

    public AssTransform(string token, string rawParenString, int startMs, int endMs, double accel, string tagPayload)
    {
        Token = token;
        RawParenString = rawParenString;

        _startMs = startMs;
        _endMs = endMs;
        _accel = accel;
        _tagPayload = tagPayload;

        _payloadParsedOk = TryParseTransformPayload(_tagPayload.AsSpan(), out var tags);
        _payloadTags = tags.ToArray();
    }

    public string ToShiftedString(int shiftMs, int lineDurationMs)
    {
        int start = _startMs - shiftMs;
        int end = _endMs - shiftMs;
        if (string.IsNullOrEmpty(_tagPayload))
            return string.Empty;

        // Match a-mo: if the transform ends before/at 0, just apply the effect.
        if (end <= 0)
            return _tagPayload;

        if (end < start)
            return string.Empty;

        // a-mo uses the original line duration as the bounds check.
        if (start > lineDurationMs)
            return string.Empty;

        var sb = new StringBuilder(64);
        sb.Append("\\t(")
          .Append(start.ToString(CultureInfo.InvariantCulture))
          .Append(',')
          .Append(end.ToString(CultureInfo.InvariantCulture))
          .Append(',');

        if (Math.Abs(_accel - 1.0) > 1e-9)
        {
            sb.Append(_accel.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append(',');
        }

        sb.Append(_tagPayload);
        sb.Append(')');
        return sb.ToString();
    }

    public string InterpolateToTags(string textBeforeThisTransform, int shiftMs, int lineDurationMs, int timeMs)
    {
        int start = _startMs - shiftMs;
        int end = _endMs - shiftMs;
        if (string.IsNullOrEmpty(_tagPayload))
            return string.Empty;

        // If it already completed, return its payload.
        if (end <= 0)
            return _tagPayload;

        if (end <= start)
            return _tagPayload;

        double linearProgress = (timeMs - start) / (double)(end - start);
        double p = Math.Pow(linearProgress, _accel);

        // Supported tags: numeric + alpha.
        // If the payload contains something we can't parse, fall back to a shifted \t.
        if (!_payloadParsedOk)
            return ToShiftedString(shiftMs, lineDurationMs);

        var sb = new StringBuilder(capacity: 64);
        for (int i = 0; i < _payloadTags.Length; i++)
        {
            var tag = _payloadTags[i];
            if (tag.Kind == PayloadTagKind.Unknown)
                return ToShiftedString(shiftMs, lineDurationMs);

            if (linearProgress <= 0)
            {
                AppendTagValue(sb, tag, GetPriorValue(textBeforeThisTransform, tag));
                continue;
            }

            if (linearProgress >= 1)
            {
                AppendTagValue(sb, tag, tag.EndValue);
                continue;
            }

            double prior = GetPriorValue(textBeforeThisTransform, tag);
            double value = (1.0 - p) * prior + p * tag.EndValue;
            AppendTagValue(sb, tag, value);
        }

        return sb.ToString();
    }

    private static double GetPriorValue(string text, PayloadTag tag)
    {
        // Prior value is the last explicit tag value before this transform.
        // Fallback default: 0 (matches a-mo for non-style tags, and is correct for alpha).
        if (TryFindLastNumericTag(text, tag.Name, out double v))
            return v;

        // alpha1..4 fall back to \alpha if not present (a-mo affectedBy).
        if (tag.Kind == PayloadTagKind.Alpha && tag.Name.Length == 3 && tag.Name[1] is '1' or '2' or '3' or '4')
        {
            if (TryFindLastAlphaTag(text, "\\alpha", out var a))
                return a;
        }

        return 0.0;
    }

    private static bool TryFindLastNumericTag(string text, string tagName, out double value)
    {
        value = 0;

        int idx = text.LastIndexOf(tagName, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        int i = idx + tagName.Length;
        while (i < text.Length && text[i] == ' ')
            i++;

        int start = i;
        while (i < text.Length)
        {
            char c = text[i];
            if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
            {
                i++;
                continue;
            }
            break;
        }

        if (i <= start)
            return false;

        return double.TryParse(text.AsSpan(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryFindLastAlphaTag(string text, string tagName, out int alpha)
    {
        alpha = 0;
        int idx = text.LastIndexOf(tagName + "&H", StringComparison.Ordinal);
        if (idx < 0)
            return false;

        int i = idx + tagName.Length + 2;
        if (i + 2 > text.Length)
            return false;

        // Expect 2 hex digits.
        int hi = FromHex(text[i]);
        int lo = i + 1 < text.Length ? FromHex(text[i + 1]) : -1;
        if (hi < 0 || lo < 0)
            return false;

        alpha = (hi << 4) | lo;
        return true;
    }

    private static int FromHex(char c)
    {
        if (c is >= '0' and <= '9')
            return c - '0';
        if (c is >= 'a' and <= 'f')
            return 10 + (c - 'a');
        if (c is >= 'A' and <= 'F')
            return 10 + (c - 'A');
        return -1;
    }

    private enum PayloadTagKind : byte
    {
        Unknown = 0,
        Numeric = 1,
        Alpha = 2,
    }

    private readonly record struct PayloadTag(PayloadTagKind Kind, string Name, double EndValue);

    private static bool TryParseTransformPayload(ReadOnlySpan<char> payload, out List<PayloadTag> tags)
    {
        tags = new List<PayloadTag>(capacity: 8);
        int i = 0;
        while (i < payload.Length)
        {
            int slash = payload[i..].IndexOf('\\');
            if (slash < 0)
                break;
            i += slash + 1;
            if (i >= payload.Length)
                break;

            int j = i;
            while (j < payload.Length)
            {
                char c = payload[j];
                if (c == '\\' || c == '(' || c == ')' || c == ',' || c == '&' || c == '{' || c == '}' || c == ' ')
                    break;
                if (c >= '0' && c <= '9')
                {
                    j++;
                    continue;
                }
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                {
                    j++;
                    continue;
                }
                break;
            }

            string name = "\\" + payload.Slice(i, j - i).ToString();
            i = j;

            if (string.Equals(name, "\\alpha", StringComparison.Ordinal) ||
                string.Equals(name, "\\1a", StringComparison.Ordinal) ||
                string.Equals(name, "\\2a", StringComparison.Ordinal) ||
                string.Equals(name, "\\3a", StringComparison.Ordinal) ||
                string.Equals(name, "\\4a", StringComparison.Ordinal))
            {
                int hIdx = payload.Slice(i).IndexOf("&H", StringComparison.Ordinal);
                if (hIdx < 0 || i + hIdx + 4 > payload.Length)
                    return false;
                int hexStart = i + hIdx + 2;
                int hi = FromHex(payload[hexStart]);
                int lo = FromHex(payload[hexStart + 1]);
                if (hi < 0 || lo < 0)
                    return false;
                int a = (hi << 4) | lo;
                tags.Add(new PayloadTag(PayloadTagKind.Alpha, name, a));
                i = hexStart + 2;
                continue;
            }

            int numStart = i;
            while (numStart < payload.Length && payload[numStart] == ' ')
                numStart++;

            int numEnd = numStart;
            while (numEnd < payload.Length)
            {
                char c = payload[numEnd];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                {
                    numEnd++;
                    continue;
                }
                break;
            }

            if (numEnd > numStart && double.TryParse(payload.Slice(numStart, numEnd - numStart), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                tags.Add(new PayloadTag(PayloadTagKind.Numeric, name, d));
                i = numEnd;
                continue;
            }

            tags.Add(new PayloadTag(PayloadTagKind.Unknown, name, 0));
            return false;
        }

        return true;
    }

    private static void AppendTagValue(StringBuilder sb, PayloadTag tag, double value)
    {
        sb.Append(tag.Name);
        if (tag.Kind == PayloadTagKind.Alpha)
        {
            int a = (int)Math.Round(value, MidpointRounding.AwayFromZero);
            if (a < 0) a = 0;
            if (a > 255) a = 255;
            sb.Append("&H").Append(a.ToString("X2", CultureInfo.InvariantCulture)).Append('&');
            return;
        }

        sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }
}

public static class AssTransformTokenizer
{
    private const char TokenDelimiter = '\u0003';
    private const byte TokenDelimiterUtf8 = 0x03;

    public static AssTokenizedText Tokenize(string text, int lineDurationMs)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf("\\t", StringComparison.Ordinal) < 0)
            return new AssTokenizedText(text ?? string.Empty, Array.Empty<AssTransform>(), lineDurationMs);

        using var read = AssEventTextRead.Parse(text);
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
            return new AssTokenizedText(Encoding.UTF8.GetString(utf8), Array.Empty<AssTransform>(), lineDurationMs);

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
            string token = string.Concat(TokenDelimiter, tokenIndex.ToString(CultureInfo.InvariantCulture), TokenDelimiter);

            WriteByte(writer, TokenDelimiterUtf8);
            if (Utf8Formatter.TryFormat(tokenIndex, numBuf, out int written))
                writer.Write(numBuf[..written]);
            WriteByte(writer, TokenDelimiterUtf8);

            ReadOnlySpan<byte> tagSpanUtf8 = utf8.Slice(start, end - start);
            int paren = tagSpanUtf8.IndexOf((byte)'(');
            string rawParenString = paren >= 0 ? Encoding.UTF8.GetString(tagSpanUtf8[paren..]) : "()";

            int trStart = func.HasTimes ? func.T1 : 0;
            int trEnd = func.HasTimes ? func.T2 : 0;
            if (trEnd == 0)
                trEnd = lineDurationMs;

            double accel = func.HasAccel ? func.Accel : 1.0;
            string payload = func.TagPayload.IsEmpty ? string.Empty : Encoding.UTF8.GetString(func.TagPayload.Span);

            transforms.Add(new AssTransform(token, rawParenString, trStart, trEnd, accel, payload));

            pos = end;
        }

        if (pos < utf8.Length)
            writer.Write(utf8[pos..]);

        string tokenized = Encoding.UTF8.GetString(writer.WrittenSpan);
        return new AssTokenizedText(tokenized, transforms, lineDurationMs);
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        Span<byte> span = writer.GetSpan(1);
        span[0] = b;
        writer.Advance(1);
    }
}
