using Mobsub.AutomationBridge.Ae;
using System.Buffers.Text;
using System.Globalization;

namespace Mobsub.AutomationBridge.Core.Motion.Amo;

internal sealed class AmoTsrData : AmoData
{
    public override AmoDataKind Kind => AmoDataKind.Tsr;

    // 1-based arrays (index 1..Length).
    public double[] XPos { get; }
    public double[] YPos { get; }
    public double[] Scale { get; }
    public double[] RotDeg { get; }

    public override int Length { get; }

    public int ReferenceFrame { get; private set; } = 1;
    public double XStartPos { get; private set; }
    public double YStartPos { get; private set; }
    public double StartScale { get; private set; } = 100.0;
    public double StartRotDeg { get; private set; }

    private AmoTsrData(int length, double[] xPos, double[] yPos, double[] scale, double[] rotDeg)
    {
        Length = length;
        XPos = xPos;
        YPos = yPos;
        Scale = scale;
        RotDeg = rotDeg;
    }

    public static AmoTsrData ParseAeTsr(string aeText, int scriptResX, int scriptResY)
    {
        var ae = AfterEffectsKeyframes.Parse(aeText);
        int n = ae.PositionFrames.Count;
        if (n <= 0)
            return new AmoTsrData(0, [0], [0], [0], [0]);

        if (ae.SourceWidth <= 0 || ae.SourceHeight <= 0)
            return new AmoTsrData(0, [0], [0], [0], [0]);

        double posScaleX = (double)scriptResX / ae.SourceWidth;
        double posScaleY = (double)scriptResY / ae.SourceHeight;

        // 1-based arrays.
        var xPos = new double[n + 1];
        var yPos = new double[n + 1];
        var scale = new double[n + 1];
        var rotDeg = new double[n + 1];

        for (int i = 1; i <= n; i++)
        {
            var p = ae.PositionFrames[i - 1];
            xPos[i] = p.X * posScaleX;
            yPos[i] = p.Y * posScaleY;

            // a-mo uses uniform scale; fall back to 100 if absent.
            scale[i] = ae.ScaleFrames.Count == n ? ae.ScaleFrames[i - 1].X : 100.0;

            // a-mo uses negative degrees (AE -> ASS).
            rotDeg[i] = ae.RotationFrames.Count == n ? -ae.RotationFrames[i - 1].Degrees : 0.0;
        }

        return new AmoTsrData(n, xPos, yPos, scale, rotDeg);
    }

    public static AmoTsrData ParseAeTsr(ReadOnlySpan<byte> aeTextUtf8, int scriptResX, int scriptResY, out string? error)
    {
        error = null;

        if (aeTextUtf8.IsEmpty || !aeTextUtf8.StartsWith("Adobe After Effects 6.0 Keyframe Data"u8))
        {
            error = "ae_title_missing";
            return new AmoTsrData(0, [0], [0], [0], [0]);
        }

        int sourceWidth = 0;
        int sourceHeight = 0;

        var xRaw = new List<double>(capacity: 1024);
        var yRaw = new List<double>(capacity: 1024);
        var scaleRaw = new List<double>(capacity: 1024);
        var rotRaw = new List<double>(capacity: 1024);

        AeSection section = AeSection.None;

        int pos = 0;
        while (pos < aeTextUtf8.Length)
        {
            var line = ReadLine(aeTextUtf8, ref pos);
            line = TrimEndAsciiWhitespace(line);
            if (line.IsEmpty)
                continue;

            bool indented = line[0] is (byte)'\t' or (byte)' ';
            if (!indented)
            {
                if (line.SequenceEqual("Adobe After Effects 6.0 Keyframe Data"u8))
                {
                    section = AeSection.Info;
                    continue;
                }

                if (line.SequenceEqual("Position"u8))
                {
                    section = AeSection.Position;
                    continue;
                }

                if (line.SequenceEqual("Scale"u8))
                {
                    section = AeSection.Scale;
                    continue;
                }

                if (line.SequenceEqual("Rotation"u8))
                {
                    section = AeSection.Rotation;
                    continue;
                }

                section = AeSection.None;
                continue;
            }

            // Preserve column header rows.
            if (line.StartsWith("\tFrame"u8) || line.StartsWith("Frame"u8))
                continue;

            if (section == AeSection.Info)
            {
                if (!TryGetTabField(line, 1, out var key))
                    continue;
                if (!TryGetTabField(line, 2, out var value))
                    continue;

                if (key.SequenceEqual("Source Width"u8))
                {
                    if (TryParseInt32(value, out int w))
                        sourceWidth = w;
                    continue;
                }

                if (key.SequenceEqual("Source Height"u8))
                {
                    if (TryParseInt32(value, out int h))
                        sourceHeight = h;
                }

                continue;
            }

            if (section == AeSection.Position)
            {
                if (TryParseAePointLine(line, out _, out var x, out var y))
                {
                    xRaw.Add(x);
                    yRaw.Add(y);
                }
                continue;
            }

            if (section == AeSection.Scale)
            {
                if (TryParseAePointLine(line, out _, out var x, out _))
                    scaleRaw.Add(x);
                continue;
            }

            if (section == AeSection.Rotation)
            {
                if (TryParseAeScalarLine(line, out _, out var deg))
                    rotRaw.Add(deg);
            }
        }

        int n = xRaw.Count;
        if (n <= 0 || yRaw.Count != n)
        {
            error = "position_frames_empty";
            return new AmoTsrData(0, [0], [0], [0], [0]);
        }

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            error = "source_width_height_missing";
            return new AmoTsrData(0, [0], [0], [0], [0]);
        }

        double posScaleX = (double)scriptResX / sourceWidth;
        double posScaleY = (double)scriptResY / sourceHeight;

        // 1-based arrays.
        var xPos = new double[n + 1];
        var yPos = new double[n + 1];
        var scale = new double[n + 1];
        var rotDeg = new double[n + 1];

        for (int i = 1; i <= n; i++)
        {
            xPos[i] = xRaw[i - 1] * posScaleX;
            yPos[i] = yRaw[i - 1] * posScaleY;
        }

        // a-mo uses uniform scale; fall back to 100 if absent/mismatched.
        if (scaleRaw.Count == n)
        {
            for (int i = 1; i <= n; i++)
                scale[i] = scaleRaw[i - 1];
        }
        else
        {
            for (int i = 1; i <= n; i++)
                scale[i] = 100.0;
        }

        // a-mo uses negative degrees (AE -> ASS); fall back to 0 if absent/mismatched.
        if (rotRaw.Count == n)
        {
            for (int i = 1; i <= n; i++)
                rotDeg[i] = -rotRaw[i - 1];
        }
        else
        {
            for (int i = 1; i <= n; i++)
                rotDeg[i] = 0.0;
        }

        return new AmoTsrData(n, xPos, yPos, scale, rotDeg);
    }

    public override void SetReferenceFrame(int frame)
    {
        if (Length <= 0)
            return;

        if (frame < 1) frame = 1;
        if (frame > Length) frame = Length;

        ReferenceFrame = frame;
        XStartPos = XPos[frame];
        YStartPos = YPos[frame];
        StartScale = Scale[frame];
        if (Math.Abs(StartScale) < 1e-9)
            StartScale = 100.0;
        StartRotDeg = RotDeg[frame];
    }

    public override MotionTsrMath.TsrState GetTsrState(
        int frame,
        bool applyX,
        bool applyY,
        bool applyScale,
        bool applyRotation)
    {
        if (Length <= 0)
            return default;

        if (frame < 1) frame = 1;
        if (frame > Length) frame = Length;

        double xCur = applyX ? XPos[frame] : XStartPos;
        double yCur = applyY ? YPos[frame] : YStartPos;

        double ratio = applyScale ? (Scale[frame] / StartScale) : 1.0;
        double rotDiff = applyRotation ? (RotDeg[frame] - StartRotDeg) : 0.0;

        return new MotionTsrMath.TsrState(xCur, yCur, RatioX: ratio, RatioY: ratio, RotDiffDeg: rotDiff);
    }

    private enum AeSection : byte
    {
        None = 0,
        Info = 1,
        Position = 2,
        Scale = 3,
        Rotation = 4,
    }

    private static bool TryParseInt32(ReadOnlySpan<byte> utf8, out int value)
    {
        value = 0;
        utf8 = TrimAsciiWhitespace(utf8);
        return !utf8.IsEmpty && Utf8Parser.TryParse(utf8, out value, out int consumed) && consumed == utf8.Length;
    }

    private static bool TryParseDouble(ReadOnlySpan<byte> utf8, out double value)
    {
        value = 0;
        utf8 = TrimAsciiWhitespace(utf8);
        return !utf8.IsEmpty && Utf8Parser.TryParse(utf8, out value, out int consumed) && consumed == utf8.Length;
    }

    private static bool TryParseAePointLine(ReadOnlySpan<byte> line, out int frame, out double x, out double y)
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

        if (!TryParseInt32(frameSpan, out frame))
            return false;
        if (!TryParseDouble(xSpan, out x))
            return false;
        if (!TryParseDouble(ySpan, out y))
            return false;

        return true;
    }

    private static bool TryParseAeScalarLine(ReadOnlySpan<byte> line, out int frame, out double value)
    {
        frame = default;
        value = default;

        if (!TryGetTabField(line, 1, out var frameSpan))
            return false;
        if (!TryGetTabField(line, 2, out var valueSpan))
            return false;

        if (!TryParseInt32(frameSpan, out frame))
            return false;
        if (!TryParseDouble(valueSpan, out value))
            return false;

        return true;
    }

    private static ReadOnlySpan<byte> ReadLine(ReadOnlySpan<byte> s, ref int pos)
    {
        if (pos >= s.Length)
            return ReadOnlySpan<byte>.Empty;

        int start = pos;
        while (pos < s.Length && s[pos] != (byte)'\n' && s[pos] != (byte)'\r')
            pos++;

        var line = s.Slice(start, pos - start);
        if (pos < s.Length && s[pos] == (byte)'\r')
            pos++;
        if (pos < s.Length && s[pos] == (byte)'\n')
            pos++;
        return line;
    }

    private static bool TryGetTabField(ReadOnlySpan<byte> s, int fieldIndex, out ReadOnlySpan<byte> field)
    {
        // fieldIndex: 0-based, split by '\t'
        field = ReadOnlySpan<byte>.Empty;
        if (fieldIndex < 0)
            return false;

        int currentIndex = 0;
        int start = 0;
        for (int i = 0; i <= s.Length; i++)
        {
            bool isEnd = i == s.Length;
            if (!isEnd && s[i] != (byte)'\t')
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

    private static ReadOnlySpan<byte> TrimEndAsciiWhitespace(ReadOnlySpan<byte> s)
    {
        int end = s.Length;
        while (end > 0 && IsAsciiWhitespace(s[end - 1]))
            end--;
        return end == s.Length ? s : s[..end];
    }

    private static ReadOnlySpan<byte> TrimAsciiWhitespace(ReadOnlySpan<byte> s)
    {
        int start = 0;
        while (start < s.Length && IsAsciiWhitespace(s[start]))
            start++;

        int end = s.Length;
        while (end > start && IsAsciiWhitespace(s[end - 1]))
            end--;

        return (start == 0 && end == s.Length) ? s : s.Slice(start, end - start);
    }

    private static bool IsAsciiWhitespace(byte b)
        => b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
}
