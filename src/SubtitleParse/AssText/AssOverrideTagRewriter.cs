using System;
using System.Buffers;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssOverrideTagRewriter
{
    public delegate byte[]? AssKnownTagRewriter(in AssTagSpan tag, ReadOnlySpan<byte> tagBytes);

    public static byte[] RewriteKnownTags(byte[] textUtf8, AssKnownTagRewriter rewriteTag, int maxTransformDepth = 16)
    {
        if (rewriteTag == null)
            throw new ArgumentNullException(nameof(rewriteTag));
        if (textUtf8 == null)
            throw new ArgumentNullException(nameof(textUtf8));

        return RewriteKnownTagsCore(textUtf8, rewriteTag, maxTransformDepth, depth: 0);
    }

    private static byte[] RewriteKnownTagsCore(byte[] textUtf8, AssKnownTagRewriter rewriteTag, int maxTransformDepth, int depth)
    {
        if (textUtf8.Length == 0)
            return textUtf8;

        // Fast path: no '{' => no override blocks.
        if (textUtf8.AsSpan().IndexOf((byte)'{') < 0)
            return textUtf8;

        using var edit = AssEventTextEdit.Parse(textUtf8);
        ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;

        Func<ReadOnlySpan<byte>, byte[]?>? rewritePayload = null;
        if (depth < maxTransformDepth)
        {
            rewritePayload = payloadUtf8 =>
            {
                if (payloadUtf8.IsEmpty || payloadUtf8.IndexOf((byte)'\\') < 0)
                    return null;

                byte[] wrapped = WrapSingleOverrideBlock(payloadUtf8);
                byte[] rewrittenWrapped = RewriteKnownTagsCore(wrapped, rewriteTag, maxTransformDepth, depth + 1);
                if (!TryUnwrapSingleOverrideBlock(rewrittenWrapped, out var inner))
                    return null;
                if (inner.SequenceEqual(payloadUtf8))
                    return null;
                return inner.ToArray();
            };
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

                if (t.Tag == AssTag.Transform && rewritePayload != null)
                {
                    TryRewriteTransformTagPayload(edit, utf8, t, rewritePayload);
                    continue;
                }

                int start = t.LineRange.Start.GetOffset(utf8.Length);
                int end = t.LineRange.End.GetOffset(utf8.Length);
                if (end <= start)
                    continue;

                ReadOnlySpan<byte> tagBytes = utf8.Slice(start, end - start);
                byte[]? repl = rewriteTag(in t, tagBytes);
                if (repl is null)
                    continue;
                if (tagBytes.SequenceEqual(repl))
                    continue;

                edit.Replace(t.LineRange, repl);
            }
        }

        return edit.HasEdits ? edit.ApplyToUtf8Bytes() : textUtf8;
    }

    public static byte[] WrapSingleOverrideBlock(ReadOnlySpan<byte> payloadUtf8)
    {
        byte[] wrapped = new byte[payloadUtf8.Length + 2];
        wrapped[0] = (byte)'{';
        payloadUtf8.CopyTo(wrapped.AsSpan(1));
        wrapped[^1] = (byte)'}';
        return wrapped;
    }

    public static bool TryUnwrapSingleOverrideBlock(ReadOnlySpan<byte> wrappedUtf8, out ReadOnlySpan<byte> innerUtf8)
    {
        innerUtf8 = default;
        if (wrappedUtf8.Length < 2)
            return false;
        if (wrappedUtf8[0] != (byte)'{' || wrappedUtf8[^1] != (byte)'}')
            return false;
        innerUtf8 = wrappedUtf8[1..^1];
        return true;
    }

    public static byte[]? RewritePayloadAsSingleOverrideBlock(ReadOnlySpan<byte> payloadUtf8, Func<byte[], byte[]> rewriteWrapped)
    {
        if (payloadUtf8.IsEmpty)
            return null;
        if (rewriteWrapped == null)
            throw new ArgumentNullException(nameof(rewriteWrapped));

        byte[] wrapped = WrapSingleOverrideBlock(payloadUtf8);
        byte[] rewrittenWrapped = rewriteWrapped(wrapped);
        if (!TryUnwrapSingleOverrideBlock(rewrittenWrapped, out var inner))
            return null;

        if (inner.SequenceEqual(payloadUtf8))
            return null;

        return inner.ToArray();
    }

    public static bool TryRewriteTransformTagPayload(
        AssEventTextEdit edit,
        ReadOnlySpan<byte> lineUtf8,
        in AssTagSpan transformTag,
        Func<ReadOnlySpan<byte>, byte[]?> rewritePayload)
    {
        if (transformTag.Tag != AssTag.Transform)
            return false;
        if (!transformTag.TryGet<AssTagFunctionValue>(out var func) || func.Kind != AssTagFunctionKind.Transform)
            return false;
        if (func.TagPayload.IsEmpty)
            return false;
        if (rewritePayload == null)
            throw new ArgumentNullException(nameof(rewritePayload));

        ReadOnlySpan<byte> payloadUtf8 = func.TagPayload.Span;
        if (payloadUtf8.IsEmpty)
            return false;

        byte[]? newPayload = rewritePayload(payloadUtf8);
        if (newPayload is null || newPayload.AsSpan().SequenceEqual(payloadUtf8))
            return false;

        int start = transformTag.LineRange.Start.GetOffset(lineUtf8.Length);
        int end = transformTag.LineRange.End.GetOffset(lineUtf8.Length);
        if (end <= start)
            return false;

        ReadOnlySpan<byte> originalTag = lineUtf8.Slice(start, end - start);

        int open = originalTag.IndexOf((byte)'(');
        if (open < 0)
            return false;

        int close = originalTag.LastIndexOf((byte)')');
        if (close < 0 || close <= open)
            return false;

        int payloadStart = originalTag[(open + 1)..close].IndexOf((byte)'\\');
        if (payloadStart < 0)
            return false;
        payloadStart += open + 1;

        var prefix = originalTag[..payloadStart];
        var suffix = originalTag[close..]; // include ')'

        byte[] replacement = new byte[prefix.Length + newPayload.Length + suffix.Length];
        prefix.CopyTo(replacement);
        newPayload.CopyTo(replacement.AsSpan(prefix.Length));
        suffix.CopyTo(replacement.AsSpan(prefix.Length + newPayload.Length));

        edit.Replace(transformTag.LineRange, replacement);
        return true;
    }

    public static byte[] ReplaceFunctionTag(
        byte[] textUtf8,
        AssTag targetTag,
        byte[] requiredRawTagStartUtf8,
        Func<AssTagFunctionValue, byte[]?> replacer,
        Action? onMalformedTag = null,
        int maxTransformDepth = 16)
    {
        if (textUtf8 == null)
            throw new ArgumentNullException(nameof(textUtf8));
        if (requiredRawTagStartUtf8 == null)
            throw new ArgumentNullException(nameof(requiredRawTagStartUtf8));
        if (replacer == null)
            throw new ArgumentNullException(nameof(replacer));

        if (maxTransformDepth < 0)
            maxTransformDepth = 0;

        return ReplaceFunctionTagCore(textUtf8, targetTag, requiredRawTagStartUtf8, replacer, onMalformedTag, maxTransformDepth, depth: 0);
    }

    private static byte[] ReplaceFunctionTagCore(
        byte[] textUtf8,
        AssTag targetTag,
        byte[] requiredRawTagStartUtf8,
        Func<AssTagFunctionValue, byte[]?> replacer,
        Action? onMalformedTag,
        int maxTransformDepth,
        int depth)
    {
        if (textUtf8.Length == 0)
            return textUtf8;

        ReadOnlySpan<byte> textSpan = textUtf8;

        // Fast path: match legacy behavior (requires the exact rawTagStart).
        if (requiredRawTagStartUtf8.Length != 0 &&
            textSpan.IndexOf(requiredRawTagStartUtf8) < 0 &&
            textSpan.IndexOf("\\t("u8) < 0)
        {
            return textUtf8;
        }

        byte[]? RewritePayload(ReadOnlySpan<byte> payloadUtf8)
        {
            if (payloadUtf8.IsEmpty)
                return null;
            if (requiredRawTagStartUtf8.Length != 0 && payloadUtf8.IndexOf(requiredRawTagStartUtf8) < 0)
                return null;
            if (depth >= maxTransformDepth)
                return null;

            return RewritePayloadAsSingleOverrideBlock(
                payloadUtf8,
                wrapped => ReplaceFunctionTagCore(wrapped, targetTag, requiredRawTagStartUtf8, replacer, onMalformedTag, maxTransformDepth, depth + 1));
        }

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

                if (t.Tag == AssTag.Transform)
                {
                    TryRewriteTransformTagPayload(edit, utf8, t, RewritePayload);
                    continue;
                }

                if (t.Tag != targetTag)
                    continue;

                int start = t.LineRange.Start.GetOffset(utf8.Length);
                int end = t.LineRange.End.GetOffset(utf8.Length);
                if (end <= start)
                    continue;

                // Preserve legacy behavior: only rewrite if the tag starts exactly with requiredRawTagStartUtf8.
                if (requiredRawTagStartUtf8.Length != 0 && !utf8[start..].StartsWith(requiredRawTagStartUtf8))
                    continue;

                if (!t.TryGet<AssTagFunctionValue>(out var func))
                {
                    onMalformedTag?.Invoke();
                    continue;
                }

                byte[]? repl = replacer(func);
                if (repl is null || repl.Length == 0)
                    continue;

                if (utf8.Slice(start, end - start).SequenceEqual(repl))
                    continue;

                edit.Replace(t.LineRange, repl);
            }
        }

        return edit.HasEdits ? edit.ApplyToUtf8Bytes() : textUtf8;
    }

    public static byte[] RemoveEmptyOverrideBlocks(byte[] textUtf8)
    {
        if (textUtf8 == null)
            throw new ArgumentNullException(nameof(textUtf8));

        ReadOnlySpan<byte> span = textUtf8;
        int first = span.IndexOf("{}"u8);
        if (first < 0)
            return textUtf8;

        var writer = new ArrayBufferWriter<byte>(span.Length);
        int pos = 0;
        while (true)
        {
            int idx = span.Slice(pos).IndexOf("{}"u8);
            if (idx < 0)
            {
                writer.Write(span[pos..]);
                break;
            }

            idx += pos;
            if (idx > pos)
                writer.Write(span.Slice(pos, idx - pos));
            pos = idx + 2;
            if (pos >= span.Length)
                break;
        }

        return writer.WrittenSpan.ToArray();
    }
}
