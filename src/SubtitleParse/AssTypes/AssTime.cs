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
        int firstColon = -1;
        for (int i = 0; i < sp.Length; i++)
        {
            if (sp[i] == ':')
            {
                firstColon = i;
                break;
            }
        }
        if (firstColon == -1) throw new FormatException($"Invalid time format (missing colon): {sp.ToString()}");

        int hours = 0;
        for (int i = 0; i < firstColon; i++)
        {
            if (!AsciiDigits.TryParseDigit(sp[i], out int digit))
                throw new FormatException($"Invalid hour digit: {sp[i]}");

            if (hours > (int.MaxValue - digit) / 10)
                hours = int.MaxValue;
            else
                hours = (hours * 10) + digit;
        }

        if (sp.Length != firstColon + 9)
            throw new FormatException($"Invalid time length: Expected {firstColon + 9}, got {sp.Length}");

        if (sp[firstColon + 3] != ':')
            throw new FormatException($"Expected ':' at position {firstColon + 3}");
        if (sp[firstColon + 6] != '.')
            throw new FormatException($"Expected '.' at position {firstColon + 6}");

        if (!AsciiDigits.TryParseDigitPair(sp, firstColon + 1, out int minutes))
            throw new FormatException($"Invalid digits: {sp[firstColon + 1]}{sp[firstColon + 2]}");
        if (!AsciiDigits.TryParseDigitPair(sp, firstColon + 4, out int seconds))
            throw new FormatException($"Invalid digits: {sp[firstColon + 4]}{sp[firstColon + 5]}");
        if (!AsciiDigits.TryParseDigitPair(sp, firstColon + 7, out int centiseconds))
            throw new FormatException($"Invalid digits: {sp[firstColon + 7]}{sp[firstColon + 8]}");

        long totalMs = (long)hours * 3600000
                     + (long)minutes * 60000
                     + (long)seconds * 1000
                     + (long)centiseconds * 10;

        if (totalMs > int.MaxValue)
            totalMs = int.MaxValue;
        if (totalMs < 0)
            totalMs = 0;

        return new AssTime((int)totalMs);
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
            if (!AsciiDigits.TryParseDigit(sp[i], out int digit))
                throw new FormatException($"Invalid hour digit: {(char)sp[i]}");

            if (hours > (int.MaxValue - digit) / 10)
                hours = int.MaxValue;
            else
                hours = (hours * 10) + digit;
        }

        if (sp.Length != firstColon + 9)
            throw new FormatException($"Invalid time length: Expected {firstColon + 9}, got {sp.Length}");

        if (sp[firstColon + 3] != (byte)':')
            throw new FormatException($"Expected ':' at position {firstColon + 3}");
        if (sp[firstColon + 6] != (byte)'.')
            throw new FormatException($"Expected '.' at position {firstColon + 6}");

        if (!AsciiDigits.TryParseDigitPair(sp, firstColon + 1, out int minutes))
            throw new FormatException($"Invalid digits: {(char)sp[firstColon + 1]}{(char)sp[firstColon + 2]}");
        if (!AsciiDigits.TryParseDigitPair(sp, firstColon + 4, out int seconds))
            throw new FormatException($"Invalid digits: {(char)sp[firstColon + 4]}{(char)sp[firstColon + 5]}");
        if (!AsciiDigits.TryParseDigitPair(sp, firstColon + 7, out int centiseconds))
            throw new FormatException($"Invalid digits: {(char)sp[firstColon + 7]}{(char)sp[firstColon + 8]}");

        long totalMs = (long)hours * 3600000
                     + (long)minutes * 60000
                     + (long)seconds * 1000
                     + (long)centiseconds * 10;

        if (totalMs > int.MaxValue)
            totalMs = int.MaxValue;
        if (totalMs < 0)
            totalMs = 0;

        return new AssTime((int)totalMs);
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
        Span<char> tmp = stackalloc char[32];
        if (!TryFormatAssTime(tmp, time, ctsRounding, out int written))
            return;

        sb.Append(tmp[..written]);
    }
    public static void WriteAssTime(TextWriter writer, AssTime time, bool ctsRounding)
    {
        Span<char> tmp = stackalloc char[32];
        if (!TryFormatAssTime(tmp, time, ctsRounding, out int written))
            return;

        writer.Write(tmp[..written]);
    }

    private static bool TryFormatAssTime(Span<char> dest, AssTime time, bool ctsRounding, out int written)
    {
        written = 0;

        long totalMsLong = time._ticks / 10000;
        if (totalMsLong < 0)
            totalMsLong = 0;
        if (totalMsLong > int.MaxValue)
            totalMsLong = int.MaxValue;

        int totalMs = (int)totalMsLong;
        int hours = totalMs / 3600000;
        int rem = totalMs - (hours * 3600000);
        int minutes = rem / 60000;
        rem -= minutes * 60000;
        int seconds = rem / 1000;
        int ms = rem - (seconds * 1000);

        if (!hours.TryFormat(dest, out int hw))
            return false;
        int pos = hw;

        if ((uint)(pos + 9) > (uint)dest.Length)
            return false;

        dest[pos++] = ':';
        AsciiDigits.Write2Digits(dest.Slice(pos, 2), minutes);
        pos += 2;
        dest[pos++] = ':';
        AsciiDigits.Write2Digits(dest.Slice(pos, 2), seconds);
        pos += 2;
        dest[pos++] = '.';

        int roundedMs = ctsRounding ? DigitRounding(ms) : ms;
        int centiseconds = roundedMs / 10;
        AsciiDigits.Write2Digits(dest.Slice(pos, 2), centiseconds);
        pos += 2;

        written = pos;
        return true;
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
}
