using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Hydra;

internal static class HydraAddTagsHandler
{
    private const string ClassDialogue = "dialogue";
    private static readonly AssTextOptions TagOptions = new(Dialect: AssTextDialect.VsFilterMod);

    public static BridgeHandlerResult Handle(HydraAddTagsCall call, List<string> logs)
    {
        var lines = call.Lines;
        if (lines is null || lines.Length == 0)
            return BadArgs("lines is required and must be non-empty.", logs);

        ReadOnlyMemory<byte> tagsUtf8 = call.Args.TagsUtf8 ?? ReadOnlyMemory<byte>.Empty;
        var normalizedTags = NormalizeTagsPayload(tagsUtf8.Span);
        if (normalizedTags.IsEmpty || normalizedTags.IndexOf((byte)'\\') < 0)
            return BadArgs("args.tags is required and must contain at least one \\\\tag.", logs);

        using var tagSet = HydraTagSet.FromTagsPayload(normalizedTags);
        if (!tagSet.Any)
            return BadArgs("args.tags did not contain any known ASS tags.", logs);

        var ops = new List<IBridgePatchOp>(capacity: Math.Min(256, lines.Length));

        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;

            using var read = AssEventTextRead.Parse(textUtf8, TagOptions);

            // Insert tags into the first override block and remove any tags of the same kind from that block,
            // so the new tags take effect even if the line already had them.
            byte[] newTextUtf8 = AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlockUtf8(
                read,
                insertTagsUtf8: normalizedTags,
                shouldRemove: tagSet.Contains);

            if (!newTextUtf8.AsSpan().SequenceEqual(read.Utf8.Span))
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

    private static ReadOnlySpan<byte> NormalizeTagsPayload(ReadOnlySpan<byte> tagsUtf8)
    {
        // Trim ASCII whitespace; allow callers to paste "{\\tags}" as well.
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

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));
}
