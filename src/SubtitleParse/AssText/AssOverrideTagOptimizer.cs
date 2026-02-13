using System;
using System.Buffers;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.AssText;

public static class AssOverrideTagOptimizer
{
    private const double DoubleEps = 1e-9;

    /// <summary>
    /// Removes redundant override tags (duplicate/overwritten/no-op assignments) while preserving semantics.
    /// This is a best-effort optimizer intended for tooling output and batch scripts.
    /// </summary>
    public static byte[] Optimize(byte[] textUtf8, int maxTransformDepth = 16)
    {
        if (textUtf8 == null)
            throw new ArgumentNullException(nameof(textUtf8));

        if (maxTransformDepth < 0)
            maxTransformDepth = 0;

        return OptimizeCore(textUtf8, maxTransformDepth, depth: 0);
    }

    private static byte[] OptimizeCore(byte[] textUtf8, int maxTransformDepth, int depth)
    {
        if (textUtf8.Length == 0)
            return textUtf8;

        // Fast path: no '{' => no override blocks.
        if (textUtf8.AsSpan().IndexOf((byte)'{') < 0)
            return textUtf8;

        using var edit = AssEventTextEdit.Parse(textUtf8);
        ReadOnlySpan<byte> utf8 = edit.Utf8Bytes.Span;
        ReadOnlySpan<AssEventSegment> segments = edit.Segments;

        int tagCount = AssTagRegistry.TagCount;

        int[]? lastLineLatestStartArr = null;
        byte[]? lineFirstSeenArr = null;
        byte[]? stateHasArr = null;
        AssTagValue[]? stateValueArr = null;
        int[]? runLastSeenIndexArr = null;
        AssTagSpan[]? runLastSeenSpanArr = null;
        int[]? runUsedTagsArr = null;
        int[]? stateUsedTagsArr = null;

        try
        {
            lastLineLatestStartArr = ArrayPool<int>.Shared.Rent(tagCount);
            lineFirstSeenArr = ArrayPool<byte>.Shared.Rent(tagCount);
            stateHasArr = ArrayPool<byte>.Shared.Rent(tagCount);
            stateValueArr = ArrayPool<AssTagValue>.Shared.Rent(tagCount);
            runLastSeenIndexArr = ArrayPool<int>.Shared.Rent(tagCount);
            runLastSeenSpanArr = ArrayPool<AssTagSpan>.Shared.Rent(tagCount);
            runUsedTagsArr = ArrayPool<int>.Shared.Rent(tagCount);
            stateUsedTagsArr = ArrayPool<int>.Shared.Rent(tagCount);

            Array.Fill(lastLineLatestStartArr, -1, 0, tagCount);
            Array.Clear(lineFirstSeenArr, 0, tagCount);
            Array.Clear(stateHasArr, 0, tagCount);
            Array.Clear(stateValueArr, 0, tagCount);
            Array.Fill(runLastSeenIndexArr, -1, 0, tagCount);

            // Pre-scan: record the last start offset for LineOnlyRenderLatest tags.
            for (int s = 0; s < segments.Length; s++)
            {
                ref readonly var seg = ref segments[s];
                if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                    continue;

                var tags = seg.Tags.Value.Span;
                for (int i = 0; i < tags.Length; i++)
                {
                    ref readonly var t = ref tags[i];
                    if (!AssTagRegistry.TryGetTagKind(t.Tag, out var kind))
                        continue;
                    if ((kind & AssTagKind.LineOnlyRenderLatest) == 0)
                        continue;

                    int tagIndex = (int)t.Tag;
                    if ((uint)tagIndex >= (uint)tagCount)
                        continue;

                    int start = t.LineRange.Start.GetOffset(utf8.Length);
                    lastLineLatestStartArr[tagIndex] = start;
                }
            }

            int runUsedCount = 0;
            int stateUsedCount = 0;

            void ClearState()
            {
                for (int i = 0; i < stateUsedCount; i++)
                {
                    int tagIndex = stateUsedTagsArr![i];
                    stateHasArr![tagIndex] = 0;
                    stateValueArr![tagIndex] = default;
                }
                stateUsedCount = 0;
            }

            void FlushRun()
            {
                for (int i = 0; i < runUsedCount; i++)
                {
                    int tagIndex = runUsedTagsArr![i];
                    int seen = runLastSeenIndexArr![tagIndex];
                    if (seen < 0)
                        continue;

                    var lastSpan = runLastSeenSpanArr![tagIndex];

                    bool noOp = stateHasArr![tagIndex] != 0 &&
                        AreEquivalent(stateValueArr![tagIndex], lastSpan.Value);

                    if (noOp)
                    {
                        edit.Delete(lastSpan.LineRange);
                        runLastSeenIndexArr[tagIndex] = -1;
                        continue;
                    }

                    if (stateHasArr[tagIndex] == 0)
                    {
                        stateHasArr[tagIndex] = 1;
                        stateUsedTagsArr![stateUsedCount++] = tagIndex;
                    }

                    stateValueArr[tagIndex] = lastSpan.Value;
                    runLastSeenIndexArr[tagIndex] = -1;
                }

                runUsedCount = 0;
            }

            byte[]? OptimizePayload(ReadOnlySpan<byte> payloadUtf8)
            {
                if (depth >= maxTransformDepth)
                    return null;
                if (payloadUtf8.IsEmpty || payloadUtf8.IndexOf((byte)'\\') < 0)
                    return null;

                byte[] wrapped = AssOverrideTagRewriter.WrapSingleOverrideBlock(payloadUtf8);
                byte[] optimizedWrapped = OptimizeCore(wrapped, maxTransformDepth, depth + 1);
                if (!AssOverrideTagRewriter.TryUnwrapSingleOverrideBlock(optimizedWrapped, out var inner))
                    return null;

                if (inner.SequenceEqual(payloadUtf8))
                    return null;

                // Avoid producing an empty transform payload; keep the original in that case.
                // (An empty payload would result in an invalid/meaningless \t(...) for most renderers.)
                if (inner.IsEmpty || inner.IndexOf((byte)'\\') < 0)
                    return null;

                return inner.ToArray();
            }

            for (int s = 0; s < segments.Length; s++)
            {
                ref readonly var seg = ref segments[s];
                if (seg.SegmentKind != AssEventSegmentKind.TagBlock || seg.Tags == null)
                    continue;

                var tags = seg.Tags.Value.Span;
                runUsedCount = 0;

                for (int i = 0; i < tags.Length; i++)
                {
                    ref readonly var t = ref tags[i];

                    if (t.Tag == AssTag.Transform)
                    {
                        AssOverrideTagRewriter.TryRewriteTransformTagPayload(edit, utf8, t, OptimizePayload);
                        continue;
                    }

                    if (!AssTagRegistry.TryGetTagKind(t.Tag, out var kind))
                        continue;

                    int tagIndex = (int)t.Tag;
                    if ((uint)tagIndex >= (uint)tagCount)
                        continue;

                    if (t.Tag == AssTag.Reset)
                    {
                        FlushRun();
                        ClearState();
                        continue;
                    }

                    if ((kind & AssTagKind.LineOnlyRenderFirst) != 0)
                    {
                        if (lineFirstSeenArr[tagIndex] != 0)
                            edit.Delete(t.LineRange);
                        else
                            lineFirstSeenArr[tagIndex] = 1;
                        continue;
                    }

                    if ((kind & AssTagKind.LineOnlyRenderLatest) != 0)
                    {
                        int start = t.LineRange.Start.GetOffset(utf8.Length);
                        if (start != lastLineLatestStartArr[tagIndex])
                            edit.Delete(t.LineRange);
                        continue;
                    }

                    // BlockOnlyRenderLatest (default): keep only the last occurrence within the block.
                    if (runLastSeenIndexArr[tagIndex] >= 0)
                    {
                        edit.Delete(runLastSeenSpanArr[tagIndex].LineRange);
                    }
                    else
                    {
                        runUsedTagsArr[runUsedCount++] = tagIndex;
                    }

                    runLastSeenIndexArr[tagIndex] = 1;
                    runLastSeenSpanArr[tagIndex] = t;
                }

                FlushRun();
            }

            if (!edit.HasEdits)
                return textUtf8;

            byte[] optimized = edit.ApplyToUtf8Bytes();
            return AssOverrideTagRewriter.RemoveEmptyOverrideBlocks(optimized);
        }
        finally
        {
            if (lastLineLatestStartArr != null)
                ArrayPool<int>.Shared.Return(lastLineLatestStartArr, clearArray: false);
            if (lineFirstSeenArr != null)
                ArrayPool<byte>.Shared.Return(lineFirstSeenArr, clearArray: true);
            if (stateHasArr != null)
                ArrayPool<byte>.Shared.Return(stateHasArr, clearArray: true);
            if (stateValueArr != null)
                ArrayPool<AssTagValue>.Shared.Return(stateValueArr, clearArray: true);
            if (runLastSeenIndexArr != null)
                ArrayPool<int>.Shared.Return(runLastSeenIndexArr, clearArray: false);
            if (runLastSeenSpanArr != null)
                ArrayPool<AssTagSpan>.Shared.Return(runLastSeenSpanArr, clearArray: true);
            if (runUsedTagsArr != null)
                ArrayPool<int>.Shared.Return(runUsedTagsArr, clearArray: false);
            if (stateUsedTagsArr != null)
                ArrayPool<int>.Shared.Return(stateUsedTagsArr, clearArray: false);
        }
    }

    private static bool AreEquivalent(AssTagValue a, AssTagValue b)
    {
        if (a.Kind != b.Kind)
            return false;

        return a.Kind switch
        {
            AssTagValueKind.None => true,
            AssTagValueKind.Int => a.IntValue == b.IntValue,
            AssTagValueKind.Double => Math.Abs(a.DoubleValue - b.DoubleValue) <= DoubleEps,
            AssTagValueKind.Bool => a.BoolValue == b.BoolValue,
            AssTagValueKind.Byte => a.ByteValue == b.ByteValue,
            AssTagValueKind.Color => a.ColorValue.Equals(b.ColorValue),
            AssTagValueKind.Bytes => a.BytesValue.Span.SequenceEqual(b.BytesValue.Span),
            AssTagValueKind.Function => AreEquivalentFunction(a.FunctionValue, b.FunctionValue),
            _ => false,
        };
    }

    private static bool AreEquivalentFunction(AssTagFunctionValue a, AssTagFunctionValue b)
    {
        if (a.Kind != b.Kind)
            return false;

        static bool Eq(double x, double y) => Math.Abs(x - y) <= DoubleEps;

        switch (a.Kind)
        {
            case AssTagFunctionKind.Pos:
            case AssTagFunctionKind.Org:
                return Eq(a.X1, b.X1) && Eq(a.Y1, b.Y1);

            case AssTagFunctionKind.Move:
                return Eq(a.X1, b.X1) && Eq(a.Y1, b.Y1)
                    && Eq(a.X2, b.X2) && Eq(a.Y2, b.Y2)
                    && a.HasTimes == b.HasTimes
                    && a.T1 == b.T1 && a.T2 == b.T2;

            case AssTagFunctionKind.Fade:
                return a.A1 == b.A1 && a.A2 == b.A2 && a.A3 == b.A3
                    && a.T1 == b.T1 && a.T2 == b.T2 && a.T3 == b.T3 && a.T4 == b.T4;

            case AssTagFunctionKind.Fad:
                return a.T1 == b.T1 && a.T2 == b.T2;

            case AssTagFunctionKind.ClipRect:
                return Eq(a.X1, b.X1) && Eq(a.Y1, b.Y1) && Eq(a.X2, b.X2) && Eq(a.Y2, b.Y2);

            case AssTagFunctionKind.ClipDrawing:
                return a.Scale == b.Scale && a.Drawing.Span.SequenceEqual(b.Drawing.Span);

            case AssTagFunctionKind.Transform:
                return a.HasTimes == b.HasTimes
                    && a.HasAccel == b.HasAccel
                    && a.T1 == b.T1 && a.T2 == b.T2
                    && (!a.HasAccel || Eq(a.Accel, b.Accel))
                    && a.TagPayload.Span.SequenceEqual(b.TagPayload.Span);
        }

        // Conservative fallback for other/extended function tags.
        return a.HasTimes == b.HasTimes
            && a.HasAccel == b.HasAccel
            && a.Scale == b.Scale
            && Eq(a.X1, b.X1) && Eq(a.Y1, b.Y1) && Eq(a.X2, b.X2) && Eq(a.Y2, b.Y2)
            && a.A1 == b.A1 && a.A2 == b.A2 && a.A3 == b.A3
            && a.T1 == b.T1 && a.T2 == b.T2 && a.T3 == b.T3 && a.T4 == b.T4
            && (!a.HasAccel || Eq(a.Accel, b.Accel))
            && a.Drawing.Span.SequenceEqual(b.Drawing.Span)
            && a.TagPayload.Span.SequenceEqual(b.TagPayload.Span);
    }
}
