using System.Numerics;
using System.Text;
using Mobsub.AutomationBridge.Ae;
using Mobsub.AutomationBridge.Core.Motion;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;
using Mobsub.SubtitleParse.AssText;
using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.AutomationBridge.Scripts.Perspective;

internal static class PerspectiveApplyTagsFromQuadHandler
{
    private const string ClassDialogue = "dialogue";

    public static BridgeHandlerResult Handle(PerspectiveApplyTagsFromQuadCall call, List<string> logs)
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
            return BadArgs("AE source width/height not found in ae_text.", logs);

        string? effectGroup = call.Args.EffectGroup;
        int? frame = call.Args.Frame;

        if (!ae.TryGetPowerPinQuad(effectGroup, frame, out var q))
            return BadArgs("AE CC Power Pin quad not found (need 0002-0005, or 0004-0007).", logs);

        double scaleX = (double)scriptRes.W / ae.SourceWidth;
        double scaleY = (double)scriptRes.H / ae.SourceHeight;

        var quad = new[]
        {
            new Vector2((float)(q.P1X * scaleX), (float)(q.P1Y * scaleY)),
            new Vector2((float)(q.P2X * scaleX), (float)(q.P2Y * scaleY)),
            new Vector2((float)(q.P3X * scaleX), (float)(q.P3Y * scaleY)),
            new Vector2((float)(q.P4X * scaleX), (float)(q.P4Y * scaleY)),
        };

        double? defaultWidth = call.Args.Width;
        double? defaultHeight = call.Args.Height;
        int defaultAlign = call.Args.Align ?? 7;
        int orgMode = call.Args.OrgMode;
        double layoutScale = call.Args.LayoutScale;
        int precision = call.Args.PrecisionDecimals;

        double originX = call.Args.OriginX;
        double originY = call.Args.OriginY;
        var origin = new Vector2((float)originX, (float)originY);

        logs.Add($"script_resolution: {scriptRes.W}x{scriptRes.H}");
        logs.Add($"ae_source: {ae.SourceWidth}x{ae.SourceHeight}");
        if (!string.IsNullOrWhiteSpace(effectGroup))
            logs.Add($"effect_group: {effectGroup}");
        if (frame is not null)
            logs.Add($"frame: {frame.Value}");
        logs.Add($"org_mode: {orgMode}");
        logs.Add($"layout_scale: {layoutScale:0.###}");
        logs.Add($"precision_decimals: {precision}");
        if (defaultWidth is not null && defaultHeight is not null)
            logs.Add($"default_size: {defaultWidth.Value:0.###}x{defaultHeight.Value:0.###}");

        int changed = 0;
        var ops = new List<IBridgePatchOp>(lines.Length);

        foreach (var line in lines)
        {
            if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
                continue;

            if (line.TextUtf8 is not { } textUtf8 || textUtf8.Length == 0)
                continue;

            double? w = line.Width ?? defaultWidth;
            double? h = line.Height ?? defaultHeight;
            int align = line.Align ?? defaultAlign;

            if (w is null || h is null || w.Value <= 0 || h.Value <= 0)
            {
                logs.Add($"missing_size line={line.Index}: need line.width/line.height or payload.args.width/height.");
                continue;
            }

            if (!PerspectiveTagsSolver.TrySolveFromQuad(
                    quad,
                    w.Value,
                    h.Value,
                    align,
                    orgMode,
                    origin,
                    layoutScale,
                    out var solved))
            {
                logs.Add($"solve_failed line={line.Index}");
                continue;
            }

            string tagBlock = PerspectiveTagsFormatter.FormatPerspectiveTags(solved, precision, includeAlign: true);
            byte[] tagBlockUtf8 = Encoding.UTF8.GetBytes(tagBlock);

            using var read = AssEventTextRead.Parse(textUtf8);
            byte[] newTextUtf8 = AssSubtitleParseTagEditor.InsertOrReplaceTagsInFirstOverrideBlockUtf8(
                read,
                insertTagsUtf8: tagBlockUtf8,
                shouldRemove: static t => t is AssTag.Alignment or AssTag.AlignmentLegacy
                    or AssTag.OriginRotation or AssTag.Position or AssTag.Movement
                    or AssTag.FontRotationX or AssTag.FontRotationY or AssTag.FontRotationZ or AssTag.FontRotationZSimple
                    or AssTag.FontScaleX or AssTag.FontScaleY or AssTag.FontScale
                    or AssTag.FontShiftX or AssTag.FontShiftY);

            if (!newTextUtf8.AsSpan().SequenceEqual(read.Utf8.Span))
            {
                ops.Add(new BridgeSetTextPatchOp(
                    Index: line.Index,
                    TextUtf8: newTextUtf8));
                changed++;
            }
        }

        logs.Add($"lines_changed: {changed}");
        BridgePatch? patch = ops.Count > 0 ? new BridgePatch(ops.ToArray()) : null;
        var resp = new BridgeResponse(true, null, logs.ToArray(), patch, Result: null, Methods: null);
        return new BridgeHandlerResult(BridgeErrorCodes.Ok, resp);
    }

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));
}
