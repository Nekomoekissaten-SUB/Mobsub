using Mobsub.AutomationBridge.Common;
using Mobsub.AutomationBridge.Core.Models;
using Mobsub.AutomationBridge.Core.Motion.Amo;
using Mobsub.AutomationBridge.Dispatch;
using Mobsub.AutomationBridge.Protocol;

namespace Mobsub.AutomationBridge.Scripts.Motion;

internal static class MotionAmoApplyHandler
{
    public static BridgeHandlerResult Handle(MotionAmoApplyCall call, List<string> logs)
    {
        var scriptRes = call.Context.ScriptResolution;
        if (scriptRes is null || scriptRes.W <= 0 || scriptRes.H <= 0)
            return BadArgs("context.script_resolution is required.", logs);

        var lines = call.Lines;
        if (lines is null || lines.Length == 0)
            return BadArgs("lines is required and must be non-empty.", logs);

        var args = call.Args;

        int selectionStartFrame = args.SelectionStartFrame;
        int totalFrames = args.TotalFrames;
        if (selectionStartFrame < 0)
            return BadArgs("args.selection_start_frame must be >= 0.", logs);
        if (totalFrames <= 0)
            return BadArgs("args.total_frames must be > 0.", logs);

        int[] frameMs = args.FrameMs ?? Array.Empty<int>();
        if (frameMs.Length != totalFrames + 1)
            return BadArgs($"args.frame_ms length mismatch: expected {totalFrames + 1}, got {frameMs.Length}.", logs);

        ReadOnlyMemory<byte> mainDataUtf8 = args.MainDataUtf8 ?? ReadOnlyMemory<byte>.Empty;
        ReadOnlyMemory<byte> clipDataUtf8 = args.ClipDataUtf8 ?? ReadOnlyMemory<byte>.Empty;

        var styles = ToAmoStyles(args.Styles);

        var coreLines = new AutomationLine[lines.Length];
        for (int i = 0; i < lines.Length; i++)
            coreLines[i] = ToCoreLine(lines[i]);

        var input = new AmoApplyInput(
            ScriptResX: scriptRes.W,
            ScriptResY: scriptRes.H,
            SelectionStartFrame: selectionStartFrame,
            TotalFrames: totalFrames,
            FrameMs: frameMs,
            MainDataUtf8: mainDataUtf8,
            ClipDataUtf8: clipDataUtf8,
            Fix: ToCoreFixOptions(args.Fix),
            Main: ToCoreMainOptions(args.Main),
            Clip: ToCoreClipOptions(args.Clip));

        if (!AmoApplyEngine.TryApply(input, coreLines, styles, logs, out var linePatches, out var applyError))
            return BadArgs(applyError ?? "motion apply failed.", logs);

        var ops = new List<IBridgePatchOp>(linePatches.Length);
        for (int i = 0; i < linePatches.Length; i++)
        {
            var p = linePatches[i];

            if (p.CanUseSetText && p.OutputLines.Length == 1)
            {
                ops.Add(new BridgeSetTextPatchOp(
                    Index: p.Index,
                    TextUtf8: p.OutputLines[0].TextUtf8));
                continue;
            }

            var inserts = new BridgeLineInsert[p.OutputLines.Length];
            for (int j = 0; j < p.OutputLines.Length; j++)
            {
                var l = p.OutputLines[j];
                inserts[j] = new BridgeLineInsert(
                    TemplateId: 0,
                    StartTime: l.StartTime,
                    EndTime: l.EndTime,
                    TextUtf8: l.TextUtf8);
            }

            ops.Add(new BridgeSpliceTemplatePatchOp(
                Index: p.Index,
                DeleteCount: 1,
                Templates: null,
                Inserts: inserts));
        }

        BridgePatch? patch = ops.Count > 0 ? new BridgePatch(ops.ToArray()) : null;
        var resp = new BridgeResponse(true, null, logs.ToArray(), patch, Result: null, Methods: null);
        return new BridgeHandlerResult(BridgeErrorCodes.Ok, resp);
    }

    private static AutomationLine ToCoreLine(BridgeLine line)
        => new(
            Index: line.Index,
            Class: line.Class,
            TextUtf8: line.TextUtf8,
            Raw: line.Raw,
            StartTime: line.StartTime,
            EndTime: line.EndTime,
            StartFrame: line.StartFrame,
            EndFrame: line.EndFrame,
            Layer: line.Layer,
            Comment: line.Comment,
            Style: line.Style,
            Actor: line.Actor,
            Effect: line.Effect,
            MarginL: line.MarginL,
            MarginR: line.MarginR,
            MarginT: line.MarginT,
            Extra: line.Extra,
            Width: line.Width,
            Height: line.Height,
            Align: line.Align);

    private static Dictionary<string, AmoStyleInfo> ToAmoStyles(Dictionary<string, StyleInfo>? styles)
    {
        if (styles is null || styles.Count == 0)
            return new Dictionary<string, AmoStyleInfo>(StringComparer.Ordinal);

        var dict = new Dictionary<string, AmoStyleInfo>(styles.Count, StringComparer.Ordinal);
        foreach (var (name, st) in styles)
        {
            dict[name] = new AmoStyleInfo(
                Align: st.Align,
                MarginL: st.MarginL,
                MarginR: st.MarginR,
                MarginT: st.MarginT,
                ScaleX: st.ScaleX,
                ScaleY: st.ScaleY,
                Outline: st.Outline,
                Shadow: st.Shadow,
                Angle: st.Angle);
        }

        return dict;
    }

    private static BridgeHandlerResult BadArgs(string message, List<string> logs)
        => new(BridgeErrorCodes.ErrBadArgs, new BridgeResponse(false, message, logs.ToArray(), Patch: null, Result: null, Methods: null));

    private static AmoFixOptions ToCoreFixOptions(BridgeAmoFixOptions opt)
        => new(
            Enabled: opt.Enabled,
            ApplyMain: opt.ApplyMain,
            ApplyClip: opt.ApplyClip,
            Diff: opt.Diff,
            RoundDecimals: opt.RoundDecimals);

    private static AmoMainOptions ToCoreMainOptions(BridgeAmoMainOptions opt)
        => new(
            XPosition: opt.XPosition,
            YPosition: opt.YPosition,
            Origin: opt.Origin,
            AbsPos: opt.AbsPos,
            XScale: opt.XScale,
            Border: opt.Border,
            Shadow: opt.Shadow,
            Blur: opt.Blur,
            BlurScale: opt.BlurScale,
            ZRotation: opt.ZRotation,
            ClipOnly: opt.ClipOnly,
            RectClip: opt.RectClip,
            VectClip: opt.VectClip,
            RcToVc: opt.RcToVc,
            KillTrans: opt.KillTrans,
            Relative: opt.Relative,
            StartFrame: opt.StartFrame,
            LinearMode: ToCoreLinearMode(opt),
            SegmentPosEps: opt.SegmentPosEps,
            PosErrorMode: ToCorePosErrorMode(opt.PosErrorMode));

    private static AmoLinearMode ToCoreLinearMode(BridgeAmoMainOptions opt)
    {
        return opt.LinearMode switch
        {
            BridgeAmoLinearMode.ForceNonlinear => AmoLinearMode.ForceNonlinear,
            BridgeAmoLinearMode.ForceLinear => AmoLinearMode.ForceLinear,
            BridgeAmoLinearMode.AutoLinearPos => AmoLinearMode.AutoLinearPos,
            BridgeAmoLinearMode.AutoSegmentPos => AmoLinearMode.AutoSegmentPos,
            _ => AmoLinearMode.ForceNonlinear,
        };
    }

    private static AmoPosErrorMode ToCorePosErrorMode(BridgeAmoPosErrorMode mode)
        => mode switch
        {
            BridgeAmoPosErrorMode.Full => AmoPosErrorMode.Full,
            BridgeAmoPosErrorMode.IgnoreScaleRot => AmoPosErrorMode.IgnoreScaleRot,
            _ => AmoPosErrorMode.Full,
        };

    private static AmoClipOptions ToCoreClipOptions(BridgeAmoClipOptions opt)
        => new(
            XPosition: opt.XPosition,
            YPosition: opt.YPosition,
            XScale: opt.XScale,
            ZRotation: opt.ZRotation,
            RectClip: opt.RectClip,
            VectClip: opt.VectClip,
            RcToVc: opt.RcToVc,
            StartFrame: opt.StartFrame);
}
