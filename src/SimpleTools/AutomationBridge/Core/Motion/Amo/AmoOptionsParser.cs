namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal static class AmoOptionsParser
{
    public static AmoMainOptions Disable(AmoMainOptions opt)
        => opt with
        {
            XPosition = false,
            YPosition = false,
            Origin = false,
            AbsPos = false,
            XScale = false,
            Border = false,
            Shadow = false,
            Blur = false,
            ZRotation = false,
            ClipOnly = false,
            RectClip = false,
            VectClip = false,
            RcToVc = false,
            LinearMode = AmoLinearMode.ForceNonlinear,
            SegmentPosEps = 0,
            PosErrorMode = AmoPosErrorMode.Full,
        };

    public static AmoClipOptions Disable(AmoClipOptions opt)
        => opt with
        {
            XPosition = false,
            YPosition = false,
            XScale = false,
            ZRotation = false,
            RectClip = false,
            VectClip = false,
            RcToVc = false,
        };

    public static bool TryNormalizeStartFrame(int startFrame, bool relative, int selectionStartFrame, int totalFrames, out int normalized, out string error)
    {
        error = string.Empty;
        normalized = startFrame;

        if (relative)
        {
            if (normalized == 0)
                normalized = 1;
            else if (normalized < 0)
                normalized = totalFrames + normalized + 1; // -1 => last
        }
        else
        {
            normalized = startFrame - selectionStartFrame + 1;
            if (normalized <= 0)
            {
                error = $"Out-of-range absolute start_frame (before selection): start_frame={startFrame}, selection_start_frame={selectionStartFrame}.";
                return false;
            }
        }

        if (normalized < 1 || normalized > totalFrames)
        {
            error = $"Out of range: {normalized} (1..{totalFrames}, -1=last in relative mode).";
            return false;
        }

        return true;
    }
}
