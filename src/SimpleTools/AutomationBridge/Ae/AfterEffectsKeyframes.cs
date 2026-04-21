using System.Globalization;

namespace Mobsub.AutomationBridge.Ae;

internal sealed class AfterEffectsKeyframes
{
    private const string TitleInfo = "Adobe After Effects 6.0 Keyframe Data";
    private const string TitlePosition = "Position";
    private const string TitleScale = "Scale";
    private const string TitleRotation = "Rotation";
    private const string TitleEffectsPrefix = "Effects";
    private const string FieldSourceWidth = "Source Width";
    private const string FieldSourceHeight = "Source Height";

    // CC Power Pin corner pins (common export):
    //   TopLeft=0002, TopRight=0003, BottomRight=0005, BottomLeft=0004
    private const string PowerPin0002 = "CC Power Pin-0002";
    private const string PowerPin0003 = "CC Power Pin-0003";
    private const string PowerPin0004 = "CC Power Pin-0004";
    private const string PowerPin0005 = "CC Power Pin-0005";

    // Alternative numbering seen in some templates/exports.
    private const string PowerPin0006 = "CC Power Pin-0006";
    private const string PowerPin0007 = "CC Power Pin-0007";

    public int SourceWidth { get; private set; }
    public int SourceHeight { get; private set; }

    public List<(int Frame, double X, double Y)> PositionFrames { get; } = new();
    public List<(int Frame, double X, double Y)> ScaleFrames { get; } = new();
    public List<(int Frame, double Degrees)> RotationFrames { get; } = new();

    // Key: "<effect group>\t<pin name>"
    private readonly Dictionary<string, List<(int Frame, double X, double Y)>> _effect2Frames = new(StringComparer.Ordinal);

    public static AfterEffectsKeyframes Parse(string text)
    {
        var parser = new AfterEffectsKeyframes();
        parser.ParseCore(text);
        return parser;
    }

    public bool TryGetPowerPinQuad(
        string? effectGroup,
        int? frame,
        out (double P1X, double P1Y, double P2X, double P2Y, double P3X, double P3Y, double P4X, double P4Y) quad)
    {
        quad = default;
        var group = FindEffectGroup(effectGroup);
        if (group is null)
            return false;

        // Prefer the common ordering used by arch.PerspectiveMotion.
        if (TryGetEffectPoint(group, PowerPin0002, frame, out var tl) &&
            TryGetEffectPoint(group, PowerPin0003, frame, out var tr) &&
            TryGetEffectPoint(group, PowerPin0005, frame, out var br) &&
            TryGetEffectPoint(group, PowerPin0004, frame, out var bl))
        {
            quad = (tl.X, tl.Y, tr.X, tr.Y, br.X, br.Y, bl.X, bl.Y);
            return true;
        }

        // Back-compat fallback: 0004..0007 as seen in some exports.
        if (!TryGetEffectPoint(group, PowerPin0004, frame, out var p1)) return false;
        if (!TryGetEffectPoint(group, PowerPin0005, frame, out var p2)) return false;
        if (!TryGetEffectPoint(group, PowerPin0006, frame, out var p3)) return false;
        if (!TryGetEffectPoint(group, PowerPin0007, frame, out var p4)) return false;

        quad = (p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y, p4.X, p4.Y);
        return true;
    }

    private bool TryGetEffectPoint(string effectHeader, string pinName, int? frame, out (double X, double Y) p)
    {
        p = default;
        string key = $"{effectHeader}\t{pinName}";
        if (!_effect2Frames.TryGetValue(key, out var frames) || frames.Count == 0)
            return false;

        if (frame is null)
        {
            p = (frames[0].X, frames[0].Y);
            return true;
        }

        int f = frame.Value;

        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].Frame == f)
            {
                p = (frames[i].X, frames[i].Y);
                return true;
            }
        }

        for (int i = frames.Count - 1; i >= 0; i--)
        {
            if (frames[i].Frame <= f)
            {
                p = (frames[i].X, frames[i].Y);
                return true;
            }
        }

        p = (frames[0].X, frames[0].Y);
        return true;
    }

    private string? FindEffectGroup(string? effectGroup)
    {
        if (!string.IsNullOrWhiteSpace(effectGroup))
            return effectGroup;

        // Pick the first seen effect header (caller can pass explicit one later).
        foreach (var k in _effect2Frames.Keys)
        {
            int tab = k.IndexOf('\t');
            if (tab > 0)
                return k.Substring(0, tab);
        }
        return null;
    }

    private enum Section
    {
        None = 0,
        Info = 1,
        Position = 2,
        Scale = 3,
        Rotation = 4,
        Effects = 5
    }

    private void ParseCore(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Section section = Section.None;
        string currentEffectGroup = string.Empty;
        string currentEffectPin = string.Empty;

        var input = text.AsSpan();
        int pos = 0;
        while (pos < input.Length)
        {
            var line = ReadLine(input, ref pos).TrimEnd();
            if (line.IsEmpty)
                continue;

            if (line[0] != '\t')
            {
                if (line.SequenceEqual(TitleInfo.AsSpan()))
                {
                    section = Section.Info;
                    continue;
                }

                if (line.SequenceEqual(TitlePosition.AsSpan()))
                {
                    section = Section.Position;
                    continue;
                }

                if (line.SequenceEqual(TitleScale.AsSpan()))
                {
                    section = Section.Scale;
                    continue;
                }

                if (line.SequenceEqual(TitleRotation.AsSpan()))
                {
                    section = Section.Rotation;
                    continue;
                }

                if (line.StartsWith(TitleEffectsPrefix.AsSpan(), StringComparison.Ordinal))
                {
                    section = Section.Effects;
                    ParseEffectHeader(line, out currentEffectGroup, out currentEffectPin);
                    continue;
                }

                section = Section.None;
                continue;
            }

            if (section == Section.Info)
            {
                ParseInfoLine(line);
                continue;
            }

            if (section == Section.Position)
            {
                if (line.StartsWith("\tFrame".AsSpan(), StringComparison.Ordinal))
                    continue;
                if (TryParseAePointLine(line, out var frame, out var x, out var y))
                    PositionFrames.Add((frame, x, y));
                continue;
            }

            if (section == Section.Scale)
            {
                if (line.StartsWith("\tFrame".AsSpan(), StringComparison.Ordinal))
                    continue;
                if (TryParseAePointLine(line, out var frame, out var x, out var y))
                    ScaleFrames.Add((frame, x, y));
                continue;
            }

            if (section == Section.Rotation)
            {
                if (line.StartsWith("\tFrame".AsSpan(), StringComparison.Ordinal))
                    continue;
                if (TryParseAeScalarLine(line, out var frame, out var deg))
                    RotationFrames.Add((frame, deg));
                continue;
            }

            if (section == Section.Effects)
            {
                if (line.StartsWith("\tFrame".AsSpan(), StringComparison.Ordinal))
                    continue;

                if (!TryParseAePointLine(line, out var frame, out var x, out var y))
                    continue;

                if (string.IsNullOrEmpty(currentEffectGroup) || string.IsNullOrEmpty(currentEffectPin))
                    continue;

                string key = $"{currentEffectGroup}\t{currentEffectPin}";
                if (!_effect2Frames.TryGetValue(key, out var list))
                {
                    list = new List<(int Frame, double X, double Y)>(capacity: 128);
                    _effect2Frames.Add(key, list);
                }
                list.Add((frame, x, y));
            }
        }

        PositionFrames.Sort((a, b) => a.Frame.CompareTo(b.Frame));
        ScaleFrames.Sort((a, b) => a.Frame.CompareTo(b.Frame));
        RotationFrames.Sort((a, b) => a.Frame.CompareTo(b.Frame));
        foreach (var kv in _effect2Frames)
            kv.Value.Sort((a, b) => a.Frame.CompareTo(b.Frame));
    }

    private static void ParseEffectHeader(ReadOnlySpan<char> header, out string effectGroup, out string pinName)
    {
        // Expected: "Effects\tCC Power Pin #1\tCC Power Pin-0004"
        if (TryGetTabField(header, 1, out var group) && TryGetTabField(header, 2, out var pin))
        {
            effectGroup = group.ToString();
            pinName = pin.ToString();
            return;
        }

        effectGroup = header.ToString();
        pinName = string.Empty;
    }

    private void ParseInfoLine(ReadOnlySpan<char> line)
    {
        // \tKey\tValue
        if (!TryGetTabField(line, 1, out var key))
            return;
        if (!TryGetTabField(line, 2, out var value))
            return;

        if (key.SequenceEqual(FieldSourceWidth.AsSpan()))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w))
                SourceWidth = w;
            return;
        }

        if (key.SequenceEqual(FieldSourceHeight.AsSpan()))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
                SourceHeight = h;
        }
    }

    private static bool TryParseAePointLine(ReadOnlySpan<char> line, out int frame, out double x, out double y)
    {
        frame = default;
        x = default;
        y = default;

        if (!TryGetTabField(line, 1, out var frameSpan))
            return false;
        if (!TryGetTabField(line, 2, out var xSpan))
            return false;
        if (!TryGetTabField(line, 3, out var ySpan))
            return false;

        if (!int.TryParse(frameSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
            return false;

        if (!double.TryParse(xSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out x))
            return false;

        if (!double.TryParse(ySpan, NumberStyles.Float, CultureInfo.InvariantCulture, out y))
            return false;

        return true;
    }

    private static bool TryParseAeScalarLine(ReadOnlySpan<char> line, out int frame, out double value)
    {
        frame = default;
        value = default;

        if (!TryGetTabField(line, 1, out var frameSpan))
            return false;
        if (!TryGetTabField(line, 2, out var valueSpan))
            return false;

        if (!int.TryParse(frameSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
            return false;

        if (!double.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return false;

        return true;
    }

    private static ReadOnlySpan<char> ReadLine(ReadOnlySpan<char> s, ref int pos)
    {
        if (pos >= s.Length)
            return ReadOnlySpan<char>.Empty;

        int start = pos;
        while (pos < s.Length && s[pos] != '\n' && s[pos] != '\r')
            pos++;

        var line = s.Slice(start, pos - start);
        if (pos < s.Length && s[pos] == '\r')
            pos++;
        if (pos < s.Length && s[pos] == '\n')
            pos++;
        return line;
    }

    private static bool TryGetTabField(ReadOnlySpan<char> s, int fieldIndex, out ReadOnlySpan<char> field)
    {
        // fieldIndex: 0-based, split by '\t'
        field = ReadOnlySpan<char>.Empty;
        if (fieldIndex < 0)
            return false;

        int currentIndex = 0;
        int start = 0;
        for (int i = 0; i <= s.Length; i++)
        {
            bool isEnd = i == s.Length;
            if (!isEnd && s[i] != '\t')
                continue;

            if (currentIndex == fieldIndex)
            {
                field = s.Slice(start, i - start);
                return true;
            }

            currentIndex++;
            start = i + 1;
        }
        return false;
    }
}
