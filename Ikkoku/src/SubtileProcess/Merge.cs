using Mobsub.SubtitleParse.AssTypes;

namespace Mobsub.Ikkoku.SubtileProcess;

public partial class Merge
{
    public static void MergeAss(AssData baseData, AssData[] mergeData, string mergeType)
    {
        foreach (var md in mergeData)
        {
            if (mergeType == "style" || mergeType == "all")
            {
                if (!baseData.Styles.Formats.SequenceEqual(md.Styles.Formats))
                {
                    throw new Exception("Different styles formats.");
                }

                foreach (var st in md.Styles.Names)
                {
                    if (baseData.Styles.Names.Add(st))
                    {
                        baseData.Styles.Collection.Add(md.Styles.Collection.Where(x => x.Name == st).First());
                    }
                }
            }

            if (mergeType == "event" || mergeType == "all")
            {
                if (!baseData.Events.Formats.SequenceEqual(md.Events.Formats))
                {
                    throw new Exception("Different events formats.");
                }

                baseData.Events.Collection.AddRange(md.Events.Collection);
            }
        }
    }


}