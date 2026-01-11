using System.Diagnostics;
using Mobsub.SubtitleParse.AssTypes;
using Mobsub.SubtitleProcess.FormatData;

namespace Mobsub.SubtitleProcess;

public class Tpp
{
    public static void ShiftAss(List<AssEvent> ets, TimeSpan time)
    {
        for (var i = 0; i < ets.Count; i++)
        {
            var evt = ets[i];
            evt.Start = evt.Start.Add(time);
            evt.End = evt.End.Add(time);
            ets[i] = evt;
        }
    }

    public static void ShiftAss(List<AssEvent> ets, TimeSpan time, string[] styles, bool negation)
    {
        for (var i = 0; i < ets.Count; i++)
        {
            var evt = ets[i];
            var had = styles.Contains(evt.Style);
            if (negation && !had)
            {
                evt.Start = evt.Start.Add(time);
                evt.End = evt.End.Add(time);
                ets[i] = evt;
            }
            else if (!negation && had)
            {
                evt.Start = evt.Start.Add(time);
                evt.End = evt.End.Add(time);
                ets[i] = evt;
            }
        }
    }

    public static void ShiftAss(List<AssEvent> ets, TimeSpan time, AssTime truncateStart)
    {
        AssTime s;
        for (var i = ets.Count - 1; i >= 0; i--)
        {
            var evt = ets[i];
            s = evt.Start.Add(time);
            if (s.CompareTo(truncateStart) >= 0)
            {
                evt.Start = s;
                evt.End = evt.End.Add(time);
                ets[i] = evt;
            }
            else
            {
                ets.RemoveAt(i);
            }
        }
    }

    public static void AssumeFPS(List<AssEvent> ets, AVTimestamp tcdata, string fps)
    {
        var fpsArray = fps.Split("/").Select(int.Parse).ToArray();
        for (var i = 0; i < ets.Count; i++)
        {
            var evt = ets[i];
            var start = evt.Start.Ticks / 10000;
            var end = evt.End.Ticks / 10000;
            
            int? vfrFrame1 = null;
            int? vfrFrame2 = null;

            for (var j = 0; j < tcdata.Mapping.Length; j++)
            {
                if (vfrFrame1 is null)
                {
                    var diff = start - tcdata.Mapping[j];
                    if (diff < 0)
                    {
                        vfrFrame1 = Math.Abs(diff) < (start - tcdata.Mapping[j - 1]) ? j : j - 1;
                    }
                    else if (diff == 0)
                    {
                        vfrFrame1 = j;
                    }
                }
                else
                {
                    if (start < end)
                    {
                        var diff = end - tcdata.Mapping[j];
                        if (diff < 0)
                        {
                            vfrFrame2 = Math.Abs(diff) < (end - tcdata.Mapping[j - 1]) ? j : j - 1;
                            break;
                        }
                        else if (diff == 0)
                        {
                            vfrFrame2 = j;
                            break;
                        }
                    }
                    else
                    {
                        vfrFrame2 = vfrFrame1;
                        break;
                    }
                }
            }
            
            Debug.Assert(vfrFrame1 != null && vfrFrame2 != null);
            evt.Start = new AssTime((long)((double)vfrFrame1 * fpsArray[1] / fpsArray[0] * 10000000));
            evt.End = new AssTime((long)((double)vfrFrame2 * fpsArray[1] / fpsArray[0] * 10000000));
            ets[i] = evt;
        }
    }

}
