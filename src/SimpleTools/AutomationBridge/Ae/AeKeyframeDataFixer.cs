using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Mobsub.AutomationBridge.Ae;

internal static class AeKeyframeDataFixer
{
    private const string TitlePosition = "Position";
    private const string TitleScale = "Scale";
    private const string TitleRotation = "Rotation";

    private enum Section
    {
        None = 0,
        Position = 1,
        Scale = 2,
        Rotation = 3
    }

    public static bool TryFixTsr(
        string aeText,
        double diffThreshold,
        int roundDecimals,
        out string fixedText,
        out string error)
    {
        fixedText = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(aeText))
        {
            error = "ae_text_empty";
            return false;
        }

        if (diffThreshold < 0)
        {
            error = "diff_threshold_negative";
            return false;
        }

        if (roundDecimals is < 0 or > 6)
        {
            error = "round_decimals_out_of_range";
            return false;
        }

        // 1) Parse arrays
        if (!TryParseTsr(aeText, out var posFrames, out var posX, out var posY, out var scaleFrames, out var scaleX, out var rotFrames, out var rotDeg, out error))
            return false;

        int n = posX.Count;
        if (n == 0)
        {
            error = "position_frames_empty";
            return false;
        }

        if (scaleX.Count == 0)
        {
            scaleFrames = new List<int>(posFrames);
            scaleX = new List<double>(capacity: n);
            for (int i = 0; i < n; i++)
                scaleX.Add(100.0);
        }
        else if (scaleX.Count != n)
        {
            error = "scale_frame_count_mismatch";
            return false;
        }

        if (rotDeg.Count == 0)
        {
            rotFrames = new List<int>(posFrames);
            rotDeg = new List<double>(capacity: n);
            for (int i = 0; i < n; i++)
                rotDeg.Add(0.0);
        }
        else if (rotDeg.Count != n)
        {
            error = "rotation_frame_count_mismatch";
            return false;
        }

        // 2) Fix values (ported from z_fix_motion.lua)
        FixSeriesInPlace(posX, diffThreshold, roundDecimals);
        FixSeriesInPlace(posY, diffThreshold, roundDecimals);
        FixSeriesInPlace(scaleX, diffThreshold, roundDecimals);
        FixSeriesInPlace(rotDeg, diffThreshold, roundDecimals);

        // 3) Rewrite only the numeric data lines, keep everything else.
        var sb = new StringBuilder(aeText.Length + 256);

        Section section = Section.None;
        int posIndex = 0;
        int scaleIndex = 0;
        int rotIndex = 0;

        var input = aeText.AsSpan();
        int p = 0;
        while (p < input.Length)
        {
            var line = ReadLine(input, ref p);
            var trimmedEnd = line.TrimEnd();

            bool indented = line.Length > 0 && (line[0] == '\t' || line[0] == ' ');
            if (!indented)
            {
                if (trimmedEnd.SequenceEqual(TitlePosition.AsSpan()))
                {
                    section = Section.Position;
                }
                else if (trimmedEnd.SequenceEqual(TitleScale.AsSpan()))
                {
                    section = Section.Scale;
                }
                else if (trimmedEnd.SequenceEqual(TitleRotation.AsSpan()))
                {
                    section = Section.Rotation;
                }
                else
                {
                    section = Section.None;
                }

                sb.Append(trimmedEnd);
                sb.Append('\n');
                continue;
            }

            // Preserve the column header row(s) verbatim.
            if (trimmedEnd.StartsWith("\tFrame".AsSpan(), StringComparison.Ordinal) ||
                trimmedEnd.StartsWith("Frame".AsSpan(), StringComparison.Ordinal))
            {
                sb.Append(trimmedEnd);
                sb.Append('\n');
                continue;
            }

            if (section == Section.Position)
            {
                if (posIndex >= n)
                {
                    sb.Append(trimmedEnd);
                    sb.Append('\n');
                    continue;
                }

                sb.Append('\t');
                sb.Append(posFrames[posIndex]);
                sb.Append('\t');
                sb.Append(FormatNumber(posX[posIndex], roundDecimals));
                sb.Append('\t');
                sb.Append(FormatNumber(posY[posIndex], roundDecimals));
                sb.Append('\t');
                sb.Append('0');
                sb.Append('\n');
                posIndex++;
                continue;
            }

            if (section == Section.Scale)
            {
                if (scaleIndex >= n)
                {
                    sb.Append(trimmedEnd);
                    sb.Append('\n');
                    continue;
                }

                sb.Append('\t');
                sb.Append(scaleFrames[scaleIndex]);
                sb.Append('\t');
                string s = FormatNumber(scaleX[scaleIndex], roundDecimals);
                sb.Append(s);
                sb.Append('\t');
                sb.Append(s);
                sb.Append('\t');
                sb.Append("100");
                sb.Append('\n');
                scaleIndex++;
                continue;
            }

            if (section == Section.Rotation)
            {
                if (rotIndex >= n)
                {
                    sb.Append(trimmedEnd);
                    sb.Append('\n');
                    continue;
                }

                sb.Append('\t');
                sb.Append(rotFrames[rotIndex]);
                sb.Append('\t');
                sb.Append(FormatNumber(rotDeg[rotIndex], roundDecimals));
                sb.Append('\n');
                rotIndex++;
                continue;
            }

            sb.Append(trimmedEnd);
            sb.Append('\n');
        }

        fixedText = sb.ToString();
        return true;
    }

    private static void FixSeriesInPlace(List<double> values, double diff, int decimals)
    {
        if (values.Count == 0)
            return;

        int start = 0;
        double sum = values[0];

        for (int i = 1; i < values.Count; i++)
        {
            if (Math.Abs(values[i] - values[i - 1]) > diff)
            {
                double avg = sum / (i - start);
                avg = Math.Round(avg, decimals, MidpointRounding.AwayFromZero);
                for (int j = start; j < i; j++)
                    values[j] = avg;

                start = i;
                sum = 0;
            }

            sum += values[i];
        }

        double finalAvg = sum / (values.Count - start);
        finalAvg = Math.Round(finalAvg, decimals, MidpointRounding.AwayFromZero);
        for (int j = start; j < values.Count; j++)
            values[j] = finalAvg;
    }

    private static string FormatNumber(double v, int decimals)
    {
        double rounded = Math.Round(v, decimals, MidpointRounding.AwayFromZero);
        if (decimals <= 0)
            return ((int)rounded).ToString(CultureInfo.InvariantCulture);

        return rounded.ToString("0." + new string('#', decimals), CultureInfo.InvariantCulture);
    }

    private static bool TryParseTsr(
        string text,
        out List<int> posFrames,
        out List<double> posX,
        out List<double> posY,
        out List<int> scaleFrames,
        out List<double> scaleX,
        out List<int> rotFrames,
        out List<double> rotDeg,
        out string error)
    {
        posFrames = new List<int>(capacity: 256);
        posX = new List<double>(capacity: 256);
        posY = new List<double>(capacity: 256);
        scaleFrames = new List<int>(capacity: 256);
        scaleX = new List<double>(capacity: 256);
        rotFrames = new List<int>(capacity: 256);
        rotDeg = new List<double>(capacity: 256);
        error = string.Empty;

        Section section = Section.None;

        var input = text.AsSpan();
        int p = 0;
        while (p < input.Length)
        {
            var line = ReadLine(input, ref p).TrimEnd();
            if (line.IsEmpty)
                continue;

            bool indented = line[0] == '\t' || line[0] == ' ';
            if (!indented)
            {
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

                section = Section.None;
                continue;
            }

            if (line.StartsWith("\tFrame".AsSpan(), StringComparison.Ordinal))
                continue;

            if (section == Section.Position)
            {
                if (!TryParsePointLine(line, out var frame, out var x, out var y))
                    continue;
                posFrames.Add(frame);
                posX.Add(x);
                posY.Add(y);
                continue;
            }

            if (section == Section.Scale)
            {
                if (!TryParsePointLine(line, out var frame, out var x, out _))
                    continue;
                scaleFrames.Add(frame);
                scaleX.Add(x);
                continue;
            }

            if (section == Section.Rotation)
            {
                if (!TryParseScalarLine(line, out var frame, out var d))
                    continue;
                rotFrames.Add(frame);
                rotDeg.Add(d);
            }
        }

        if (posX.Count == 0)
        {
            error = "position_not_found";
            return false;
        }

        return true;
    }

    private static bool TryParsePointLine(ReadOnlySpan<char> line, out int frame, out double x, out double y)
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

    private static bool TryParseScalarLine(ReadOnlySpan<char> line, out int frame, out double v)
    {
        frame = default;
        v = default;

        if (!TryGetTabField(line, 1, out var frameSpan))
            return false;
        if (!TryGetTabField(line, 2, out var valueSpan))
            return false;

        if (!int.TryParse(frameSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame))
            return false;
        if (!double.TryParse(valueSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
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
