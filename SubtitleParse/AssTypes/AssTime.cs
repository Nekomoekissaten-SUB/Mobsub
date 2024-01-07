using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace Mobsub.AssTypes;

public readonly struct AssTime
{
    private readonly long _ticks;
    private const int MinMilliSeconds = 0;
    private const int MaxMilliSeconds = int.MaxValue;
    public static AssTime MinValue => new AssTime(MinMilliSeconds);
    public static AssTime MaxValue => new AssTime(MaxMilliSeconds);

    public int Hour => new TimeSpan(_ticks).Hours;
    public int Minute => new TimeSpan(_ticks).Minutes;
    public int Second => new TimeSpan(_ticks).Seconds;
    public int Millisecond => new TimeSpan(_ticks).Milliseconds;
    public int Microsecond => new TimeSpan(_ticks).Microseconds;
    public int Nanosecond => new TimeSpan(_ticks).Nanoseconds;
    public long Ticks => _ticks;

    public AssTime(long ticks) => _ticks = ticks;
    public AssTime(int ms) => _ticks = (long)ms * 10000;


    private AssTime AddTicks(long ticks) => new AssTime((_ticks + TimeSpan.TicksPerDay + (ticks % TimeSpan.TicksPerDay)) % TimeSpan.TicksPerDay);

    public AssTime Add(TimeSpan value) => AddTicks(value.Ticks);

}
