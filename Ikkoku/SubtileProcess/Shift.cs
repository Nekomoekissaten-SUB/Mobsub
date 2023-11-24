using Mobsub.AssTypes;

namespace Mobsub.Ikkoku;

public partial class SubtileProcess
{
    public static void ShiftAss(List<AssEvent> ets, TimeSpan time)
    {
        for (var i = 0; i < ets.Count; i++)
        {
            ets[i].Start = ets[i].Start.Add(time);
            ets[i].End = ets[i].End.Add(time);
        }
    }
}