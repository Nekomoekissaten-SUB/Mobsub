using Mobsub.AutomationBridge.Core.Models;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal readonly record struct AmoApplyInput(
    int ScriptResX,
    int ScriptResY,
    int SelectionStartFrame,
    int TotalFrames,
    int[] FrameMs,
    ReadOnlyMemory<byte> MainDataUtf8,
    ReadOnlyMemory<byte> ClipDataUtf8,
    AmoFixOptions Fix,
    AmoMainOptions Main,
    AmoClipOptions Clip
);

internal static class AmoApplyEngine
{
    private const string ClassDialogue = "dialogue";

    public static bool TryApply(
        AmoApplyInput input,
        IReadOnlyList<AutomationLine> lines,
        Dictionary<string, AmoStyleInfo> styles,
        List<string> logs,
        out AmoLinePatch[] patches,
        out string? error)
    {
        patches = Array.Empty<AmoLinePatch>();
        error = null;

        if (input.ScriptResX <= 0 || input.ScriptResY <= 0)
        {
            error = "context.script_resolution is required.";
            return false;
        }

        if (input.SelectionStartFrame < 0)
        {
            error = "args.selection_start_frame must be >= 0.";
            return false;
        }

        if (input.TotalFrames <= 0)
        {
            error = "args.total_frames must be > 0.";
            return false;
        }

        var frameMs = input.FrameMs ?? Array.Empty<int>();
        if (frameMs.Length != input.TotalFrames + 1)
        {
            error = $"args.frame_ms length mismatch: expected {input.TotalFrames + 1}, got {frameMs.Length}.";
            return false;
        }

        ReadOnlyMemory<byte> mainDataUtf8 = input.MainDataUtf8;
        ReadOnlyMemory<byte> clipDataUtf8 = input.ClipDataUtf8;

        var mainOpt = input.Main;
        var clipOpt = input.Clip;

        bool hasMainData = !AmoDataParser.IsNullOrWhiteSpace(mainDataUtf8.Span);
        bool hasClipData = !AmoDataParser.IsNullOrWhiteSpace(clipDataUtf8.Span);

        if (!hasMainData)
            mainOpt = AmoOptionsParser.Disable(mainOpt);
        if (!hasClipData)
            clipOpt = AmoOptionsParser.Disable(clipOpt);

        // Disable scale-dependent options when scale is off.
        if (!mainOpt.XScale)
            mainOpt = mainOpt with { Border = false, Shadow = false, Blur = false };

        // rcToVc implies rect+vect.
        if (mainOpt.RcToVc)
            mainOpt = mainOpt with { RectClip = true, VectClip = true };
        if (clipOpt.RcToVc)
            clipOpt = clipOpt with { RectClip = true, VectClip = true };

        // Normalize reference frames.
        if (!AmoOptionsParser.TryNormalizeStartFrame(mainOpt.StartFrame, mainOpt.Relative, input.SelectionStartFrame, input.TotalFrames, out int mainStartFrame, out var errMainFrame))
        {
            error = "main.start_frame: " + errMainFrame;
            return false;
        }

        int clipStartFrame = mainStartFrame;
        if (hasClipData)
        {
            if (!AmoOptionsParser.TryNormalizeStartFrame(clipOpt.StartFrame, mainOpt.Relative, input.SelectionStartFrame, input.TotalFrames, out clipStartFrame, out var errClipFrame))
            {
                error = "clip.start_frame: " + errClipFrame;
                return false;
            }
        }

        mainOpt = mainOpt with { StartFrame = mainStartFrame };
        clipOpt = clipOpt with { StartFrame = clipStartFrame };

        if (!AmoFixer.TryApplyFix(input.Fix, ref mainDataUtf8, ref clipDataUtf8, hasClipData, logs, out var fixApplyErr))
        {
            error = fixApplyErr ?? "fix failed.";
            return false;
        }

        // Parse data.
        AmoData mainData;
        if (hasMainData)
        {
            mainData = AmoDataParser.Parse(mainDataUtf8.Span, input.ScriptResX, input.ScriptResY, input.TotalFrames, out var mainErr);
            if (mainErr is not null)
            {
                error = "main_data: " + mainErr;
                return false;
            }
        }
        else
        {
            mainData = new AmoNullData();
        }

        AmoData clipData;
        if (hasClipData)
        {
            clipData = AmoDataParser.Parse(clipDataUtf8.Span, input.ScriptResX, input.ScriptResY, input.TotalFrames, out var clipErr);
            if (clipErr is not null)
            {
                error = "clip_data: " + clipErr;
                return false;
            }
        }
        else
        {
            clipData = new AmoNullData();
        }

        if (mainData.Kind == AmoDataKind.None && clipData.Kind == AmoDataKind.None)
        {
            error = "No motion data provided (main_data/clip_data are both empty).";
            return false;
        }

        mainData.SetReferenceFrame(mainOpt.StartFrame);
        clipData.SetReferenceFrame(clipOpt.StartFrame);

        // Choose clip data sources (clip overrides main).
        AmoData? rectClipData = null;
        AmoData? vectClipData = null;

        if (clipData.Kind != AmoDataKind.None)
        {
            if (clipOpt.RectClip && clipData.Kind == AmoDataKind.Tsr)
                rectClipData = clipData;
            if (clipOpt.VectClip)
                vectClipData = clipData;
        }

        if (mainData.Kind != AmoDataKind.None)
        {
            if (rectClipData is null && mainOpt.RectClip && mainData.Kind == AmoDataKind.Tsr)
                rectClipData = mainData;
            if (vectClipData is null && mainOpt.VectClip)
                vectClipData = mainData;
        }

        var amoOptions = new AmoApplyOptions(mainOpt, clipOpt);

        var ctx = new AmoApplyContext
        {
            ScriptResX = input.ScriptResX,
            ScriptResY = input.ScriptResY,
            SelectionStartFrame = input.SelectionStartFrame,
            TotalFrames = input.TotalFrames,
            FrameMs = frameMs,
            Options = amoOptions,
            MainData = mainData,
            RectClipData = rectClipData,
            VectClipData = vectClipData,
        };

        var hintsIndex = AmoPrepareHintsIndex.Create(ctx);

        // Generate patch ops, in descending index order (splice_template shifts indices).
        var ordered = new List<AutomationLine>(capacity: lines.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (!IsAmoEligibleLine(line))
                continue;
            ordered.Add(line);
        }
        ordered.Sort(static (a, b) => b.Index.CompareTo(a.Index));

        var outPatches = new List<AmoLinePatch>(capacity: ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            var line = ordered[i];

            var hints = hintsIndex.ForLine(line);
            AmoPreparedLine prepared = AmoLinePreprocessor.Prepare(line, amoOptions, styles, input.ScriptResX, input.ScriptResY, hints, out var prepErr);
            if (prepErr is not null)
            {
                error = $"Failed to prepare line {line.Index}: {prepErr}";
                return false;
            }

            var outLines = AmoMotionApplier.ApplyLine(prepared, ctx, logs);
            if (outLines is null || outLines.Length == 0)
                continue;

            bool canUseSetText = outLines.Length == 1
                && line.StartTime is not null
                && line.EndTime is not null
                && outLines[0].StartTime == line.StartTime.Value
                && outLines[0].EndTime == line.EndTime.Value;

            outPatches.Add(new AmoLinePatch(line.Index, canUseSetText, outLines));
        }

        logs.Add($"script_resolution: {input.ScriptResX}x{input.ScriptResY}");
        logs.Add($"total_frames: {input.TotalFrames}");
        logs.Add($"main_data: {mainData.Kind}");
        logs.Add($"clip_data: {clipData.Kind}");

        patches = outPatches.Count > 0 ? outPatches.ToArray() : Array.Empty<AmoLinePatch>();
        return true;
    }

    private static bool IsAmoEligibleLine(AutomationLine line)
    {
        if (!string.Equals(line.Class, ClassDialogue, StringComparison.OrdinalIgnoreCase))
            return false;
        if (line.Comment is true)
            return false;
        if (line.TextUtf8 is null)
            return false;
        if (line.StartTime is null || line.EndTime is null)
            return false;
        if (line.StartFrame is null || line.EndFrame is null)
            return false;
        return true;
    }
}

