namespace Mobsub.AssTypes;

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

}
