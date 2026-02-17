using System.Text;
using Mobsub.AutomationBridge.Ae;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Perspective;

internal static class PerspectiveApplyClipQuadHandler
{
    private const string ClassDialogue = "dialogue";

    public static BridgeHandlerResult Handle(PerspectiveApplyClipQuadCall call, List<string> logs)
    {
        var scriptRes = call.Context.ScriptResolution;
        if (scriptRes is null || scriptRes.W <= 0 || scriptRes.H <= 0)
            return BadArgs("context.script_resolution is required.", logs);

        var lines = call.Lines;
        if (lines is null || lines.Length == 0)
            return BadArgs("lines is required and must be non-empty.", logs);

        string aeText = call.Args.AeText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(aeText))
            return BadArgs("args.ae_text is required.", logs);

        var ae = AfterEffectsKeyframes.Parse(aeText);
        if (ae.SourceWidth <= 0 || ae.SourceHeight <= 0)
            return BadArgs("AE source width/height not found in args.ae_text.", logs);

        string? effectGroup = call.Args.EffectGroup;
        int? frame = call.Args.Frame;

        if (!ae.TryGetPowerPinQuad(effectGroup, frame, out var q))
            return BadArgs("AE CC Power Pin quad not found (need 0002-0005, or 0004-0007).", logs);

        double scaleX = (double)scriptRes.W / ae.SourceWidth;
        double scaleY = (double)scriptRes.H / ae.SourceHeight;

        var p1 = (X: q.P1X * scaleX, Y: q.P1Y * scaleY);
        var p2 = (X: q.P2X * scaleX, Y: q.P2Y * scaleY);
        var p3 = (X: q.P3X * scaleX, Y: q.P3Y * scaleY);
        var p4 = (X: q.P4X * scaleX, Y: q.P4Y * scaleY);

        logs.Add($"script_resolution: {scriptRes.W}x{scriptRes.H}");
        logs.Add($"ae_source: {ae.SourceWidth}x{ae.SourceHeight}");
        if (!string.IsNullOrWhiteSpace(effectGroup))
            logs.Add($"effect_group: {effectGroup}");
        if (frame is not null)
            logs.Add($"frame: {frame.Value}");

        string clipTag = AssTagFormatter.FormatClipQuad(p1, p2, p3, p4);
        byte[] clipTagUtf8 = Encoding.UTF8.GetBytes(clipTag);

        var ops = new List<IBridgePatchOp>(lines.Length);
        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;

            using var read = AssEventTextRead.Parse(textUtf8);
            byte[] newTextUtf8 = AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlockUtf8(
                read,
                insertTagsUtf8: clipTagUtf8,
                shouldRemove: static t => t is AssTag.Clip or AssTag.InverseClip);

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

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));
}
