using System.Buffers;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Hydra;

internal static class HydraPayloadRewriter
{
    private static readonly AssTextOptions TagOptions = new(Dialect: AssTextDialect.VsFilterMod);

    private const byte Backslash = (byte)'\\';
    private const byte OpenParen = (byte)'(';
    private const byte CloseParen = (byte)')';
    private const byte Comma = (byte)',';

    public static byte[]? RemoveTags(ReadOnlySpan<byte> payload, HydraTagSet remove, int depth, int maxDepth)
    {
        if (payload.IsEmpty || !remove.Any)
            return null;

        var scanner = new AssOverrideTagScanner(payload, payloadAbsoluteStartByte: 0, lineBytes: default, TagOptions);

        ArrayBufferWriter<byte>? writer = null;
        int pos = 0;

        while (scanner.MoveNext(out var token))
        {
            int tagStart = token.TagStart;
            int tagEnd = token.TagEnd;
            if ((uint)tagEnd > (uint)payload.Length || tagEnd <= tagStart)
                continue;

            bool keep = true;
            ReadOnlySpan<byte> replacement = default;

            if (token.IsKnown)
            {
                AssTag tag = token.Tag;

                if (tag == AssTag.Transform && depth < maxDepth &&
                    TryGetTransformPayloadRange(payload[tagStart..tagEnd], out int payloadStartInTag, out int payloadEndInTag))
                {
                    var tagBytes = payload[tagStart..tagEnd];
                    ReadOnlySpan<byte> inner = tagBytes.Slice(payloadStartInTag, payloadEndInTag - payloadStartInTag);

                    byte[]? newInner = RemoveTags(inner, remove, depth + 1, maxDepth);
                    if (newInner is not null)
                    {
                        if (newInner.Length == 0 || newInner.AsSpan().IndexOf(Backslash) < 0)
                        {
                            keep = false; // drop invalid empty transform
                        }
                        else
                        {
                            replacement = BuildTransformReplacement(tagBytes, payloadStartInTag, payloadEndInTag, newInner);
                        }
                    }
                }
                else if (remove.Contains(tag))
                {
                    keep = false;
                }
            }

            if (keep && replacement.IsEmpty)
            {
                if (writer is not null)
                {
                    if (tagStart > pos)
                        writer.Write(payload.Slice(pos, tagStart - pos));
                    writer.Write(payload.Slice(tagStart, tagEnd - tagStart));
                    pos = tagEnd;
                }
            }
            else if (keep)
            {
                writer ??= new ArrayBufferWriter<byte>(payload.Length);
                if (tagStart > pos)
                    writer.Write(payload.Slice(pos, tagStart - pos));
                writer.Write(replacement);
                pos = tagEnd;
            }
            else
            {
                writer ??= new ArrayBufferWriter<byte>(payload.Length);
                if (tagStart > pos)
                    writer.Write(payload.Slice(pos, tagStart - pos));
                pos = tagEnd;
            }
        }

        if (writer is null)
            return null;

        if (pos < payload.Length)
            writer.Write(payload[pos..]);

        return writer.WrittenSpan.ToArray();
    }

    public static byte[]? ToggleClip(ReadOnlySpan<byte> payload, int depth, int maxDepth)
    {
        if (payload.IsEmpty || payload.IndexOf(Backslash) < 0)
            return null;

        var scanner = new AssOverrideTagScanner(payload, payloadAbsoluteStartByte: 0, lineBytes: default, TagOptions);

        ArrayBufferWriter<byte>? writer = null;
        int pos = 0;

        while (scanner.MoveNext(out var token))
        {
            int tagStart = token.TagStart;
            int tagEnd = token.TagEnd;
            if ((uint)tagEnd > (uint)payload.Length || tagEnd <= tagStart)
                continue;

            ReadOnlySpan<byte> tagBytes = payload.Slice(tagStart, tagEnd - tagStart);

            ReadOnlySpan<byte> repl = default;

            if (token.IsKnown)
            {
                AssTag tag = token.Tag;

                if (tag == AssTag.Transform && depth < maxDepth &&
                    TryGetTransformPayloadRange(tagBytes, out int payloadStartInTag, out int payloadEndInTag))
                {
                    ReadOnlySpan<byte> inner = tagBytes.Slice(payloadStartInTag, payloadEndInTag - payloadStartInTag);
                    byte[]? newInner = ToggleClip(inner, depth + 1, maxDepth);
                    if (newInner is not null)
                    {
                        repl = BuildTransformReplacement(tagBytes, payloadStartInTag, payloadEndInTag, newInner);
                    }
                }
                else if (tag == AssTag.Clip && tagBytes.StartsWith("\\clip"u8))
                {
                    repl = BuildTagRename(tagBytes, newName: "\\iclip"u8, oldNameLen: 5);
                }
                else if (tag == AssTag.InverseClip && tagBytes.StartsWith("\\iclip"u8))
                {
                    repl = BuildTagRename(tagBytes, newName: "\\clip"u8, oldNameLen: 6);
                }
            }

            if (repl.IsEmpty)
            {
                if (writer is not null)
                {
                    if (tagStart > pos)
                        writer.Write(payload.Slice(pos, tagStart - pos));
                    writer.Write(tagBytes);
                    pos = tagEnd;
                }
            }
            else
            {
                writer ??= new ArrayBufferWriter<byte>(payload.Length + 8);
                if (tagStart > pos)
                    writer.Write(payload.Slice(pos, tagStart - pos));
                writer.Write(repl);
                pos = tagEnd;
            }
        }

        if (writer is null)
            return null;

        if (pos < payload.Length)
            writer.Write(payload[pos..]);

        return writer.WrittenSpan.ToArray();
    }

    public static byte[]? AppendTagsToTransforms(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> tagsToAppend, int depth, int maxDepth)
    {
        if (payload.IsEmpty || tagsToAppend.IsEmpty || payload.IndexOf(Backslash) < 0)
            return null;

        var scanner = new AssOverrideTagScanner(payload, payloadAbsoluteStartByte: 0, lineBytes: default, TagOptions);

        ArrayBufferWriter<byte>? writer = null;
        int pos = 0;

        while (scanner.MoveNext(out var token))
        {
            if (!token.IsKnown || token.Tag != AssTag.Transform)
                continue;

            int tagStart = token.TagStart;
            int tagEnd = token.TagEnd;
            if ((uint)tagEnd > (uint)payload.Length || tagEnd <= tagStart)
                continue;

            ReadOnlySpan<byte> tagBytes = payload.Slice(tagStart, tagEnd - tagStart);

            if (!TryGetTransformPayloadRange(tagBytes, out int payloadStartInTag, out int payloadEndInTag))
                continue;

            ReadOnlySpan<byte> inner = tagBytes.Slice(payloadStartInTag, payloadEndInTag - payloadStartInTag);

            byte[]? rewrittenInner = depth < maxDepth
                ? AppendTagsToTransforms(inner, tagsToAppend, depth + 1, maxDepth)
                : null;

            ReadOnlySpan<byte> innerForThis = rewrittenInner is not null ? rewrittenInner : inner;

            byte[] appended = new byte[innerForThis.Length + tagsToAppend.Length];
            innerForThis.CopyTo(appended);
            tagsToAppend.CopyTo(appended.AsSpan(innerForThis.Length));

            ReadOnlySpan<byte> repl = BuildTransformReplacement(tagBytes, payloadStartInTag, payloadEndInTag, appended);

            writer ??= new ArrayBufferWriter<byte>(payload.Length + tagsToAppend.Length + 16);
            if (tagStart > pos)
                writer.Write(payload.Slice(pos, tagStart - pos));
            writer.Write(repl);
            pos = tagEnd;
        }

        if (writer is null)
            return null;

        if (pos < payload.Length)
            writer.Write(payload[pos..]);

        return writer.WrittenSpan.ToArray();
    }

    private static bool TryGetTransformPayloadRange(ReadOnlySpan<byte> transformTagBytes, out int payloadStartInTag, out int payloadEndInTag)
    {
        payloadStartInTag = 0;
        payloadEndInTag = 0;

        int open = transformTagBytes.IndexOf(OpenParen);
        if (open < 0)
            return false;

        int close = transformTagBytes.LastIndexOf(CloseParen);
        if (close < 0 || close <= open)
            return false;

        var inner = transformTagBytes.Slice(open + 1, close - open - 1);

        int payloadStart = inner.IndexOf(Backslash);
        if (payloadStart < 0)
        {
            // Transform without tags: allow appending (caller may insert comma if needed).
            payloadStartInTag = close;
            payloadEndInTag = close;
            return true;
        }

        payloadStartInTag = open + 1 + payloadStart;
        payloadEndInTag = close;
        return true;
    }

    private static ReadOnlySpan<byte> BuildTransformReplacement(ReadOnlySpan<byte> originalTagBytes, int payloadStartInTag, int payloadEndInTag, ReadOnlySpan<byte> newPayload)
    {
        if (payloadStartInTag < 0 || payloadEndInTag < payloadStartInTag || payloadEndInTag > originalTagBytes.Length)
            return originalTagBytes;

        // If payloadStart==payloadEnd, we are appending to a transform without any existing payload tags.
        if (payloadStartInTag == payloadEndInTag)
        {
            // Insert comma before the payload when there are numeric params (t1/t2/accel).
            // In \t(\tags) form (no numeric params), no comma is needed.
            int open = originalTagBytes.IndexOf(OpenParen);
            int close = originalTagBytes.LastIndexOf(CloseParen);
            if (open >= 0 && close >= 0 && close > open)
            {
                var between = originalTagBytes.Slice(open + 1, close - open - 1);
                int firstSlash = between.IndexOf(Backslash);
                if (firstSlash < 0)
                {
                    int i = between.Length - 1;
                    while (i >= 0 && between[i] == (byte)' ')
                        i--;
                    bool needsComma = i >= 0 && between[i] != Comma;

                    byte[] repl = new byte[originalTagBytes.Length + (needsComma ? 1 : 0) + newPayload.Length];
                    originalTagBytes[..close].CopyTo(repl);
                    int p = close;
                    if (needsComma)
                        repl[p++] = Comma;
                    newPayload.CopyTo(repl.AsSpan(p));
                    originalTagBytes[close..].CopyTo(repl.AsSpan(p + newPayload.Length));
                    return repl;
                }
            }
        }

        byte[] replacement = new byte[originalTagBytes.Length - (payloadEndInTag - payloadStartInTag) + newPayload.Length];
        originalTagBytes[..payloadStartInTag].CopyTo(replacement);
        newPayload.CopyTo(replacement.AsSpan(payloadStartInTag));
        originalTagBytes[payloadEndInTag..].CopyTo(replacement.AsSpan(payloadStartInTag + newPayload.Length));
        return replacement;
    }

    private static ReadOnlySpan<byte> BuildTagRename(ReadOnlySpan<byte> originalTagBytes, ReadOnlySpan<byte> newName, int oldNameLen)
    {
        if (originalTagBytes.Length < oldNameLen)
            return originalTagBytes;

        byte[] repl = new byte[originalTagBytes.Length - oldNameLen + newName.Length];
        newName.CopyTo(repl);
        originalTagBytes.Slice(oldNameLen).CopyTo(repl.AsSpan(newName.Length));
        return repl;
    }
}
