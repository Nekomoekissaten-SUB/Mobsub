using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.SubtitleParse.Language;

public sealed class AssOverrideTextAnalyzerContext
{
    public AssOverrideTextAnalyzerContext(
        int? eventDurationMs = null,
        int? layoutResX = null,
        int? layoutResY = null,
        int? playResX = null,
        int? playResY = null)
    {
        EventDurationMs = eventDurationMs;
        LayoutResX = layoutResX;
        LayoutResY = layoutResY;
        PlayResX = playResX;
        PlayResY = playResY;
    }

    public AssOverrideTextAnalyzerContext(
        AssTime start,
        AssTime end,
        int? layoutResX = null,
        int? layoutResY = null,
        int? playResX = null,
        int? playResY = null)
        : this(eventDurationMs: ComputeDurationMs(start, end), layoutResX: layoutResX, layoutResY: layoutResY, playResX: playResX, playResY: playResY)
    {
    }

    /// <summary>
    /// Duration of the current event line in milliseconds.
    /// Used to validate relative time parameters like \move(t1,t2), \t(t1,t2), \fade(...,t1..t4), \fad(t1,t2).
    /// </summary>
    public int? EventDurationMs { get; }

    /// <summary>Script Info: LayoutResX (preferred over PlayResX for coordinate bounds).</summary>
    public int? LayoutResX { get; }

    /// <summary>Script Info: LayoutResY (preferred over PlayResY for coordinate bounds).</summary>
    public int? LayoutResY { get; }

    /// <summary>Optional coordinate bounds (ASS script resolution). Exceeding bounds is reported as a warning.</summary>
    public int? PlayResX { get; }

    /// <summary>Optional coordinate bounds (ASS script resolution). Exceeding bounds is reported as a warning.</summary>
    public int? PlayResY { get; }

    internal bool TryGetCoordinateBounds(out int x, out int y)
    {
        if (LayoutResX is int lx && LayoutResY is int ly)
        {
            x = lx;
            y = ly;
            return true;
        }

        if (PlayResX is int px && PlayResY is int py)
        {
            x = px;
            y = py;
            return true;
        }

        x = y = 0;
        return false;
    }

    private static int? ComputeDurationMs(AssTime start, AssTime end)
    {
        // If duration is negative, keep it null to avoid misleading warnings.
        long deltaTicks = end.Ticks - start.Ticks;
        if (deltaTicks <= 0)
            return null;

        long ms = deltaTicks / 10_000; // TimeSpan ticks -> ms
        if (ms > int.MaxValue)
            return int.MaxValue;
        return (int)ms;
    }
}
