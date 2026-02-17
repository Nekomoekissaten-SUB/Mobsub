namespace Mobsub.AutomationBridge.Core.Models;

internal static class AutomationLineComparer
{
    public static bool ContentEquals(AutomationLine a, AutomationLine b)
    {
        if (a.Index != b.Index)
            return false;
        if (!string.Equals(a.Class, b.Class, StringComparison.Ordinal))
            return false;

        if (!Utf8BytesEqual(a.TextUtf8, b.TextUtf8))
            return false;

        if (!string.Equals(a.Raw, b.Raw, StringComparison.Ordinal))
            return false;

        if (a.StartTime != b.StartTime || a.EndTime != b.EndTime)
            return false;
        if (a.StartFrame != b.StartFrame || a.EndFrame != b.EndFrame)
            return false;
        if (a.Layer != b.Layer)
            return false;
        if (a.Comment != b.Comment)
            return false;
        if (!string.Equals(a.Style, b.Style, StringComparison.Ordinal))
            return false;
        if (!string.Equals(a.Actor, b.Actor, StringComparison.Ordinal))
            return false;
        if (!string.Equals(a.Effect, b.Effect, StringComparison.Ordinal))
            return false;
        if (a.MarginL != b.MarginL || a.MarginR != b.MarginR || a.MarginT != b.MarginT)
            return false;
        if (!ExtraEquals(a.Extra, b.Extra))
            return false;
        if (a.Width != b.Width || a.Height != b.Height)
            return false;
        if (a.Align != b.Align)
            return false;

        return true;
    }

    private static bool Utf8BytesEqual(ReadOnlyMemory<byte>? a, ReadOnlyMemory<byte>? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return a.Value.Span.SequenceEqual(b.Value.Span);
    }

    private static bool ExtraEquals(Dictionary<string, string>? a, Dictionary<string, string>? b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a is null || b is null)
            return false;
        if (a.Count != b.Count)
            return false;

        foreach (var (k, v) in a)
        {
            if (!b.TryGetValue(k, out var bv))
                return false;
            if (!string.Equals(v, bv, StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}

