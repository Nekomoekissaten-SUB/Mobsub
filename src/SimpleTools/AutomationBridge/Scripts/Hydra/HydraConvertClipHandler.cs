using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;

namespace Mobsub.AutomationBridge.Scripts.Hydra;

internal static class HydraConvertClipHandler
{
    private const string ClassDialogue = "dialogue";
    private const int MaxTransformDepth = 8;

    public static BridgeHandlerResult Handle(HydraConvertClipCall call, List<string> logs)
    {
        var lines = call.Lines;
        if (lines is null || lines.Length == 0)
            return BadArgs("lines is required and must be non-empty.", logs);

        var ops = new List<IBridgePatchOp>(capacity: Math.Min(256, lines.Length));

        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;

            // Fast path: likely no clip tags.
            if (textUtf8.Span.IndexOf("clip"u8) < 0)
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
                byte[]? newPayload = HydraPayloadRewriter.ToggleClip(payload, depth: 0, maxDepth: MaxTransformDepth);
                if (newPayload is null)
                    continue;

                byte[] repl = new byte[newPayload.Length + 2];
                repl[0] = (byte)'{';
                newPayload.CopyTo(repl.AsSpan(1));
                repl[^1] = (byte)'}';

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

    private static (int Start, int End) GetRangeOffsets(Range range, int length)
        => (range.Start.GetOffset(length), range.End.GetOffset(length));

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));
}

