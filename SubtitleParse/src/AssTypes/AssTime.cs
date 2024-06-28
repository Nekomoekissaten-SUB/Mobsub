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
}
