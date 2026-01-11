using Mobsub.SubtitleParse;
using System.Text;

namespace Mobsub.SubtitleParse.AssTypes;

public readonly struct AssTime
{
    private readonly long _ticks;
    private const int MinMilliSeconds = 0;
    private const int MaxMilliSeconds = int.MaxValue;
    private const int MinTimeTicks = 0;
    private const long MaxTimeTicks = (long)int.MaxValue* 10000;

    public static AssTime MinValue => new AssTime(MinMilliSeconds);
    public static AssTime MaxValue => new AssTime(MaxMilliSeconds);

    public int Hour => (int)new TimeSpan(_ticks).TotalHours;    // Truncate
    public int Minute => new TimeSpan(_ticks).Minutes;
    public int Second => new TimeSpan(_ticks).Seconds;
    public int Millisecond => new TimeSpan(_ticks).Milliseconds;
    public int Microsecond => new TimeSpan(_ticks).Microseconds;
    public int Nanosecond => new TimeSpan(_ticks).Nanoseconds;
    public long Ticks => _ticks;

    public AssTime(long ticks) => _ticks = Math.Clamp(ticks, MinTimeTicks, MaxTimeTicks);
    public AssTime(int ms) => _ticks = Math.Clamp((long)ms * 10000, MinTimeTicks, MaxTimeTicks);

    private AssTime AddTicks(long ticks) => new AssTime(_ticks + ticks);

    public AssTime Add(TimeSpan value) => AddTicks(value.Ticks);

    public int CompareTo(AssTime value) => _ticks.CompareTo(value._ticks);

    /// <summary>
    /// Parse from ass file, format is hours:minutes:seconds:centi-seconds
    /// e.g. 0:00:00.00, number of digits of hours is variable
    /// </summary>
    /// <param name="sp"></param>
    /// <returns></returns>
    public static AssTime ParseFromAss(ReadOnlySpan<char> sp)
    {
        var ms = 0;
        var sepPosFirst = 1;

        for (int i = 0; i < sp.Length; i++)
        {
            if (sp[i] == ':')
            {
                sepPosFirst = i;
                break;
            }
        }

        int h = 0;
        for (var i = sepPosFirst - 1; i > -1; i--)
        {
            h += (sp[i] - '0') * (int)Math.Pow(10, i);
        }
        ms += h * 1000 * 60 * 60;

        for (int i = sepPosFirst + 1; i < sp.Length; i++)
        {
            var c = sp[i];
            var n = c - '0';

            if (i == sepPosFirst + 1)
            {
                ms += n * 1000 * 60 * 10;
            }
            else if (i == sepPosFirst + 2)
            {
                ms += n * 1000 * 60;
            }
            else if (i == sepPosFirst + 4)
            {
                ms += n * 1000 * 10;
            }
            else if (i == sepPosFirst + 5)
            {
                ms += n * 1000;
            }
            else if (i == sepPosFirst + 6)
            {
                if (c != '.')
                {
                    throw new Exception($"Wrong timestamp in ass: {sp.ToString()}");
                }
            }
            else if (i == sepPosFirst + 7)
            {
                ms += n * 100;
            }
            else if (i == sepPosFirst + 8)
            {
                ms += n * 10;
            }
            else
            {
                if (c != ':')
                {
                    throw new Exception($"Wrong timestamp in ass: {sp.ToString()}");
                }
            }
        }

        return new AssTime(ms);
    }

    public static AssTime ParseFromAss(ReadOnlySpan<byte> sp)
    {
        int firstColon = -1;
        for (int i = 0; i < sp.Length; i++)
        {
            if (sp[i] == (byte)':')
            {
                firstColon = i;
                break;
            }
        }
        if (firstColon == -1) throw new FormatException($"Invalid time format (missing colon): {Encoding.ASCII.GetString(sp)}");

        int hours = 0;
        for (int i = 0; i < firstColon; i++)
        {
            if (sp[i] < (byte)'0' || sp[i] > (byte)'9')
                throw new FormatException($"Invalid hour digit: {(char)sp[i]}");
            hours = hours * 10 + (sp[i] - (byte)'0');
        }

        if (sp.Length != firstColon + 9)
            throw new FormatException($"Invalid time length: Expected {firstColon + 9}, got {sp.Length}");

        if (sp[firstColon + 3] != (byte)':')
            throw new FormatException($"Expected ':' at position {firstColon + 3}");
        if (sp[firstColon + 6] != (byte)'.')
            throw new FormatException($"Expected '.' at position {firstColon + 6}");

        int minutes = ParseDigitPair(sp, firstColon + 1);
        int seconds = ParseDigitPair(sp, firstColon + 4);
        int centiseconds = ParseDigitPair(sp, firstColon + 7);

        int totalMs = hours * 3600000
                    + minutes * 60000
                    + seconds * 1000
                    + centiseconds * 10;

        return new AssTime(totalMs);
    }

    public string ToString(SubtitleType st, bool ctsRounding)
    {
        var sb = new StringBuilder();

        switch (st)
        {
            case SubtitleType.Ass:
                WriteAssTime(sb, this, ctsRounding);
                break;
            default:
                WriteAssTime(sb, this, false);
                break;
        }

        return sb.ToString();
    }

    public static void WriteAssTime(StringBuilder sb, AssTime time, bool ctsRounding)
    {
        sb.Append(time.Hour);
        sb.Append(':');
        WriteChar(sb, time.Minute, 2);
        sb.Append(':');
        WriteChar(sb, time.Second, 2);
        sb.Append('.');

        WriteChar(sb, ctsRounding ? DigitRounding(time.Millisecond) : time.Millisecond, 3);
    }
    public static void WriteAssTime(TextWriter writer, AssTime time, bool ctsRounding)
    {
        writer.Write(time.Hour);
        writer.Write(':');
        WriteChar(writer, time.Minute, 2);
        writer.Write(':');
        WriteChar(writer, time.Second, 2);
        writer.Write('.');

        WriteChar(writer, ctsRounding ? DigitRounding(time.Millisecond) : time.Millisecond, 3);
    }

    private static char[] GetCharArray(int val, int length)
    {
        var ca = new char[length];
        
        var divisor = 1;
        for (var i = 1; i < length; i++)
        {
            divisor *= 10;
        }

        for (var i = 0; i < length; i++)
        {
            ca[i] = (char)(val / divisor + '0');
            val %= divisor;
            divisor /= 10;
        }
        return ca;
    }
    private static void WriteChar(StringBuilder sb, int val, int length)
    {
        var ca = GetCharArray(val, length);
        sb.Append(ca[0..Math.Min(length, 2)]);
    }
    private static void WriteChar(TextWriter writer, int val, int length)
    {
        var ca = GetCharArray(val, length);
        writer.Write(ca[0..Math.Min(length, 2)]);
    }

    private static int DigitRounding(int i)
    {
        var last = i % 10;
        return (i > 994) ? 990 : last >= 0 && last <= 4 ? i - last : i + (10 - last);
    }

    public double SubtractionMilliseconds(AssTime time)
    {
        return new TimeSpan(_ticks - time.Ticks).TotalMilliseconds;
    }

    private static int ParseDigitPair(ReadOnlySpan<byte> sp, int start)
    {
        int high = sp[start] - (byte)'0';
        int low = sp[start + 1] - (byte)'0';
        if (high < 0 || high > 9 || low < 0 || low > 9)
            throw new FormatException($"Invalid digits: {(char)sp[start]}{(char)sp[start + 1]}");
        return high * 10 + low;
    }
}
