using System.Diagnostics;
using System.Globalization;

namespace Mobsub.SubtitleProcess.FormatData;

public class AVTimestamp
{
    public int Version;
    public float[] Mapping = [];    // millisecond precision

    public static int CheckVersion(StreamReader sr)
    {
        var line = sr.ReadLine();
        if (line is null)
        {
            throw new FileFormatException("Invalid timestamp first line");
        }

        var s = line.AsSpan().Trim();
        if (s.IsEmpty)
        {
            throw new FileFormatException("Invalid timestamp first line");
        }

        if (s[0] == '#')
        {
            s = s[1..].TrimStart();
        }

        if (!s.StartsWith("timestamp format".AsSpan(), StringComparison.OrdinalIgnoreCase)
            && !s.StartsWith("timecode format".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            throw new FileFormatException("Invalid timestamp first line");
        }

        var vIndex = s.LastIndexOf('v');
        if (vIndex < 0 || vIndex == s.Length - 1)
        {
            throw new FileFormatException("Invalid timestamp first line");
        }

        if (!int.TryParse(s[(vIndex + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
        {
            throw new FileFormatException("Invalid timestamp first line");
        }

        if (version is < 1 or > 4)
        {
            throw new FileFormatException($"Invalid timestamp version {version}");
        }

        return version;
    }

    public void Parse(StreamReader sr, int version)
    {
        Version = version;
        switch (version)
        {
            case 1:
                ParseVersion1(sr);
                break;
            case 2:
                ParseVersion2(sr, false);
                break;
            case 3:
                ParseVersion3(sr);
                break;
            case 4:
                ParseVersion2(sr, true);
                break;
            default:
                throw new FileFormatException($"Unsupported timestamp version {version}");
        }
    }

    public void ParseVersion2(StreamReader sr)
    {
        ParseVersion2(sr, false);
    }

    private void ParseVersion2(StreamReader sr, bool allowUnsorted)
    {
        var tsList = new List<float>();
        foreach (var line in ReadDataLines(sr))
        {
            if (!TryParseFloat(line.AsSpan(), out var value))
            {
                throw new FileFormatException("Invalid timestamp value");
            }
            tsList.Add(value);
        }

        if (allowUnsorted)
        {
            tsList.Sort();
        }

        Mapping = [.. tsList];
    }

    private void ParseVersion1(StreamReader sr)
    {
        float defaultFps = float.NaN;
        var ranges = new List<(int start, int end, float fps)>();

        foreach (var line in ReadDataLines(sr))
        {
            var span = line.AsSpan();
            if (span.StartsWith("assume".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                var fpsSpan = span["assume".Length..].Trim();
                if (!TryParseFloat(fpsSpan, out defaultFps))
                {
                    throw new FileFormatException("Invalid assume fps");
                }
                continue;
            }

            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
            {
                throw new FileFormatException("Invalid v1 timestamp range");
            }

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var end) ||
                !TryParseFloat(parts[2].AsSpan(), out var fps))
            {
                throw new FileFormatException("Invalid v1 timestamp range");
            }

            if (start < 0 || end < start)
            {
                throw new FileFormatException("Invalid v1 timestamp range");
            }

            ranges.Add((start, end, fps));
        }

        if (float.IsNaN(defaultFps))
        {
            throw new FileFormatException("Missing assume fps in v1 timestamp");
        }

        if (ranges.Count == 0)
        {
            throw new FileFormatException("No frame ranges in v1 timestamp");
        }

        var maxEnd = ranges.Max(r => r.end);
        var fpsMap = new float[maxEnd + 1];
        Array.Fill(fpsMap, defaultFps);
        foreach (var range in ranges)
        {
            for (var i = range.start; i <= range.end; i++)
            {
                fpsMap[i] = range.fps;
            }
        }

        var mapping = new float[maxEnd + 1];
        double timeMs = 0;
        for (var i = 0; i <= maxEnd; i++)
        {
            mapping[i] = (float)timeMs;
            timeMs += 1000.0 / fpsMap[i];
        }
        Mapping = mapping;
    }

    private void ParseVersion3(StreamReader sr)
    {
        float defaultFps = float.NaN;
        var mapping = new List<float>();
        double timeMs = 0;

        foreach (var line in ReadDataLines(sr))
        {
            var span = line.AsSpan();
            if (span.StartsWith("assume".AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                var fpsSpan = span["assume".Length..].Trim();
                if (!TryParseFloat(fpsSpan, out defaultFps))
                {
                    throw new FileFormatException("Invalid assume fps");
                }
                continue;
            }

            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            if (parts[0].Equals("gap", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length < 2 || !TryParseFloat(parts[1].AsSpan(), out var gapSeconds))
                {
                    throw new FileFormatException("Invalid gap entry");
                }
                timeMs += gapSeconds * 1000.0;
                continue;
            }

            if (!TryParseFloat(parts[0].AsSpan(), out var durationSeconds))
            {
                throw new FileFormatException("Invalid duration entry");
            }

            var fps = defaultFps;
            if (parts.Length > 1 && !TryParseFloat(parts[1].AsSpan(), out fps))
            {
                throw new FileFormatException("Invalid fps entry");
            }

            if (float.IsNaN(fps))
            {
                throw new FileFormatException("Missing assume fps in v3 timestamp");
            }

            if (fps <= 0)
            {
                timeMs += durationSeconds * 1000.0;
                continue;
            }

            var frameCount = (int)Math.Round(durationSeconds * fps);
            if (frameCount <= 0)
            {
                continue;
            }

            var frameDurationMs = 1000.0 / fps;
            for (var i = 0; i < frameCount; i++)
            {
                mapping.Add((float)timeMs);
                timeMs += frameDurationMs;
            }
        }

        Mapping = [.. mapping];
    }

    private static IEnumerable<string> ReadDataLines(StreamReader sr)
    {
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }
            yield return trimmed;
        }
    }

    private static bool TryParseFloat(ReadOnlySpan<char> span, out float value)
    {
        return float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
