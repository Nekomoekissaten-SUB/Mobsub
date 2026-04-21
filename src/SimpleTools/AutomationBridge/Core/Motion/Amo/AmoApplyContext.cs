namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal sealed class AmoApplyContext
{
    public required int ScriptResX { get; init; }
    public required int ScriptResY { get; init; }

    /// <summary>
    /// Absolute frame number of the first frame in the selection.
    /// </summary>
    public required int SelectionStartFrame { get; init; }

    /// <summary>
    /// Total frames in the selection (end - start).
    /// </summary>
    public required int TotalFrames { get; init; }

    /// <summary>
    /// Frame boundary times (ms) for [SelectionStartFrame .. SelectionStartFrame+TotalFrames].
    /// Length must be TotalFrames+1.
    /// </summary>
    public required int[] FrameMs { get; init; }

    public required AmoApplyOptions Options { get; init; }
    public required AmoData MainData { get; init; }
    public required AmoData? RectClipData { get; init; }
    public required AmoData? VectClipData { get; init; }
}

