using System.Diagnostics;

namespace Mobsub.SubtitleProcess.FormatData;

public class AVTimestamp
{
    public int Version;
    public float[] Mapping = [];    // millisecond precision

    public static int CheckVersion(StreamReader sr)
    {
        var s = sr.ReadLine().AsSpan();
        if ((!s.StartsWith("# timestamp format")) || s[^2..][0] != 'v')
        {
            throw new FileFormatException("Invalid timestamp first line");
        }
        var v = s[^1..][0];
        if ((v - '0') is not 1 or 2 or 3 or 4)
        {
            throw new FileFormatException($"Invalid timestamp version {v}");
        }
        return v - '0';
    }
    public void ParseVersion2(StreamReader sr)
    {
        // sr.ReadLine();
        Version = 2;

        int nextChar;
        float value = 0;
        int decimalplaces = -1;
        var tsList = new List<float>();
        while ((nextChar = sr.Peek()) >= 0)
        {
            switch (nextChar)
            {
                case '\n':
                    value /= (float)Math.Pow(10, decimalplaces);
                    tsList.Add(value);
                    value = 0;
                    decimalplaces = -1;
                    sr.Read();
                    break;
                case '\r':
                    sr.Read();
                    break;
                case '.':
                    decimalplaces += 1;
                    sr.Read();
                    break;
                case '-':
                    throw new FileFormatException("Invalid timestamp, why “-” ?");
                default:
                    Debug.Assert(nextChar >= '0' && nextChar <= '9');
                    value = value * 10 + (nextChar - '0');
                    if (decimalplaces >= 0)
                    {
                        decimalplaces += 1;
                    }
                    sr.Read();
                    break;
            }
        }
        Mapping = [.. tsList];
    }
}