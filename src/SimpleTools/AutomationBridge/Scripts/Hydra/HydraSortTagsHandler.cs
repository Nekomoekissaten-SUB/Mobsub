using System.Buffers;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Hydra;

internal static class HydraSortTagsHandler
{
    private const string ClassDialogue = "dialogue";
    private static readonly AssTextOptions TagOptions = new(Dialect: AssTextDialect.VsFilterMod);

    private readonly struct TagOrderSpec
    {
        public readonly short[] SlotByTag;
        public readonly int SlotCount;

        public TagOrderSpec(short[] slotByTag, int slotCount)
        {
            SlotByTag = slotByTag;
            SlotCount = slotCount;
        }
    }

    private readonly struct TagRange
    {
        public readonly int Start;
        public readonly int End;

        public TagRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Length => End - Start;
    }

    private sealed class TagRangeStartComparer : IComparer<TagRange>
    {
        public static TagRangeStartComparer Instance { get; } = new();
        public int Compare(TagRange x, TagRange y) => x.Start.CompareTo(y.Start);
    }

    public static BridgeHandlerResult Handle(HydraSortTagsCall call, List<string> logs)
    {
        var lines = call.Lines;
        if (lines is null || lines.Length == 0)
            return BadArgs("lines is required and must be non-empty.", logs);

        ReadOnlyMemory<byte> orderUtf8 = call.Args.OrderUtf8 ?? ReadOnlyMemory<byte>.Empty;
        ReadOnlySpan<byte> normalizedOrder = NormalizeOrderPayload(orderUtf8.Span);
        TagOrderSpec? order = TryBuildOrder(normalizedOrder);

        var ops = new List<IBridgePatchOp>(capacity: Math.Min(256, lines.Length));

        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;

            // Fast path: no override blocks => nothing to sort.
            if (textUtf8.Span.IndexOf((byte)'{') < 0)
                continue;

            using var edit = AssEventTextEdit.Parse(textUtf8);
            ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;

            var segments = edit.Segments;
            for (int s = 0; s < segments.Length; s++)
            {
                ref readonly var seg = ref segments[s];
                if (seg.SegmentKind != AssEventSegmentKind.TagBlock)
                    continue;

                var (segStart, segEnd) = GetRangeOffsets(seg.LineRange, utf8.Length);
                if (segEnd - segStart < 2)
                    continue;

                int contentStart = segStart + 1;
                int contentEnd = segEnd - 1;
                if (contentEnd < contentStart)
                    contentEnd = contentStart;

                ReadOnlySpan<byte> payload = utf8.Slice(contentStart, contentEnd - contentStart);

                byte[]? newPayload = SortTagBlockPayload(payload, order);
                if (newPayload is null)
                    continue;

                byte[] repl = new byte[newPayload.Length + 2];
                repl[0] = (byte)'{';
                newPayload.CopyTo(repl.AsSpan(1));
                repl[^1] = (byte)'}';

                // Skip no-op edits to avoid pointless set_text patches.
                if (utf8.Slice(segStart, segEnd - segStart).SequenceEqual(repl))
                    continue;

                edit.Replace(seg.LineRange, repl);
            }

            if (!edit.HasEdits)
                continue;

            byte[] newTextUtf8 = edit.ApplyToUtf8Bytes();
            if (!newTextUtf8.AsSpan().SequenceEqual(textUtf8.Span))
            {
                ops.Add(new BridgeSetTextPatchOp(
                    Index: line.Index,
                    TextUtf8: newTextUtf8));
            }
        }

        BridgePatch? patch = ops.Count > 0 ? new BridgePatch(ops.ToArray()) : null;
        var resp = new BridgeResponse(true, null, logs.ToArray(), patch, Result: null, Methods: null);
        return new BridgeHandlerResult(BridgeErrorCodes.Ok, resp);
    }

    private static byte[]? SortTagBlockPayload(ReadOnlySpan<byte> payload, TagOrderSpec? order)
    {
        int firstSlash = payload.IndexOf((byte)'\\');
        if (firstSlash < 0)
            return null;

        int slotCount = order?.SlotCount ?? HydraTagOrder.SlotCount;
        Span<TagRange> selected = slotCount <= 256 ? stackalloc TagRange[slotCount] : new TagRange[slotCount];
        for (int i = 0; i < selected.Length; i++)
            selected[i] = new TagRange(-1, -1);

        TagRange[] transforms = ArrayPool<TagRange>.Shared.Rent(8);
        int transformCount = 0;

        try
        {
            var scanner = new AssOverrideTagScanner(payload, payloadAbsoluteStartByte: 0, lineBytes: default, TagOptions);
            while (scanner.MoveNext(out var token))
            {
                if (!token.IsKnown)
                    continue;

                int start = token.TagStart;
                int end = token.TagEnd;
                if ((uint)end > (uint)payload.Length || end <= start)
                    continue;

                AssTag tag = token.Tag;

                if (tag == AssTag.Transform)
                {
                    if (transformCount >= transforms.Length)
                    {
                        var next = ArrayPool<TagRange>.Shared.Rent(transforms.Length * 2);
                        Array.Copy(transforms, next, transforms.Length);
                        ArrayPool<TagRange>.Shared.Return(transforms, clearArray: false);
                        transforms = next;
                    }

                    transforms[transformCount++] = new TagRange(start, end);
                    continue;
                }

                int slot = GetSlot(order, tag);
                if ((uint)slot < (uint)selected.Length && selected[slot].Start < 0)
                {
                    selected[slot] = new TagRange(start, end);
                }
            }

            bool hasAny = transformCount > 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i].Start >= 0)
                {
                    hasAny = true;
                    break;
                }
            }
            if (!hasAny)
                return null;

            int skipCount = transformCount;
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i].Start >= 0)
                    skipCount++;
            }

            TagRange[] skip = ArrayPool<TagRange>.Shared.Rent(skipCount);
            try
            {
                int si = 0;
                for (int i = 0; i < selected.Length; i++)
                {
                    var r = selected[i];
                    if (r.Start >= 0)
                        skip[si++] = r;
                }
                for (int i = 0; i < transformCount; i++)
                    skip[si++] = transforms[i];

                Array.Sort(skip, 0, skipCount, TagRangeStartComparer.Instance);

                var writer = new ArrayBufferWriter<byte>(payload.Length + 16);

                // Prefix before the first \tag (e.g. '*' / '>' markers).
                if (firstSlash > 0)
                    writer.Write(payload[..firstSlash]);

                // Sorted head: first occurrence of each tag in Hydra order.
                for (int i = 0; i < selected.Length; i++)
                {
                    var r = selected[i];
                    if (r.Start >= 0 && r.Length > 0)
                        writer.Write(payload.Slice(r.Start, r.Length));
                }

                // Leftovers: everything except the sorted head and transforms.
                int pos = firstSlash;
                for (int i = 0; i < skipCount; i++)
                {
                    var r = skip[i];
                    if (r.End <= pos)
                        continue;

                    int rs = r.Start < firstSlash ? firstSlash : r.Start;
                    if (rs > pos)
                        writer.Write(payload.Slice(pos, rs - pos));
                    pos = Math.Max(pos, r.End);
                }
                if (pos < payload.Length)
                    writer.Write(payload[pos..]);

                // Transforms: keep their original order, but move to the end of the block.
                for (int i = 0; i < transformCount; i++)
                {
                    var tr = transforms[i];
                    if (tr.Start >= 0 && tr.Length > 0)
                        writer.Write(payload.Slice(tr.Start, tr.Length));
                }

                return writer.WrittenSpan.ToArray();
            }
            finally
            {
                ArrayPool<TagRange>.Shared.Return(skip, clearArray: false);
            }
        }
        finally
        {
            ArrayPool<TagRange>.Shared.Return(transforms, clearArray: false);
        }
    }

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));

    private static int GetSlot(TagOrderSpec? order, AssTag tag)
    {
        if (order is null)
            return HydraTagOrder.GetSlot(tag);

        int i = (int)tag;
        short[] map = order.Value.SlotByTag;
        return (uint)i < (uint)map.Length ? map[i] : (short)-1;
    }

    private static TagOrderSpec? TryBuildOrder(ReadOnlySpan<byte> orderPayload)
    {
        if (orderPayload.IsEmpty || orderPayload.IndexOf((byte)'\\') < 0)
            return null;

        int tagCount = AssTagRegistry.TagCount;
        var slotByTag = new short[tagCount];
        Array.Fill(slotByTag, (short)-1);

        int slot = 0;
        var scanner = new AssOverrideTagScanner(orderPayload, payloadAbsoluteStartByte: 0, lineBytes: default, TagOptions);
        while (scanner.MoveNext(out var token))
        {
            if (!token.IsKnown)
                continue;

            AssTag tag = token.Tag;
            if (tag == AssTag.Transform)
                continue;

            if (TrySetSlot(slotByTag, tag, (short)slot))
                slot++;
        }

        return slot > 0 ? new TagOrderSpec(slotByTag, slot) : null;
    }

    private static bool TrySetSlot(short[] slotByTag, AssTag tag, short slot)
    {
        int tagCount = slotByTag.Length;

        static bool TrySet(short[] map, int count, AssTag t, short s)
        {
            int i = (int)t;
            if ((uint)i >= (uint)count)
                return false;
            if (map[i] >= 0)
                return false;
            map[i] = s;
            return true;
        }

        bool any = TrySet(slotByTag, tagCount, tag, slot);

        // Keep behavior consistent with Hydra's historical order string.
        switch (tag)
        {
            case AssTag.ColorPrimary:
            case AssTag.ColorPrimaryAbbreviation:
                any |= TrySet(slotByTag, tagCount, AssTag.ColorPrimary, slot);
                any |= TrySet(slotByTag, tagCount, AssTag.ColorPrimaryAbbreviation, slot);
                break;

            case AssTag.Alignment:
            case AssTag.AlignmentLegacy:
                any |= TrySet(slotByTag, tagCount, AssTag.Alignment, slot);
                any |= TrySet(slotByTag, tagCount, AssTag.AlignmentLegacy, slot);
                break;

            case AssTag.FontRotationZ:
            case AssTag.FontRotationZSimple:
                any |= TrySet(slotByTag, tagCount, AssTag.FontRotationZ, slot);
                any |= TrySet(slotByTag, tagCount, AssTag.FontRotationZSimple, slot);
                break;
        }

        return any;
    }

    private static ReadOnlySpan<byte> NormalizeOrderPayload(ReadOnlySpan<byte> orderUtf8)
    {
        int start = 0;
        int end = orderUtf8.Length;

        while (start < end && IsAsciiWhitespace(orderUtf8[start])) start++;
        while (end > start && IsAsciiWhitespace(orderUtf8[end - 1])) end--;

        if (end - start >= 2 && orderUtf8[start] == (byte)'{' && orderUtf8[end - 1] == (byte)'}')
        {
            start++;
            end--;
            while (start < end && IsAsciiWhitespace(orderUtf8[start])) start++;
            while (end > start && IsAsciiWhitespace(orderUtf8[end - 1])) end--;
        }

        return orderUtf8.Slice(start, end - start);
    }

    private static bool IsAsciiWhitespace(byte b)
        => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));
}
